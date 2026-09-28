$ErrorActionPreference='Stop'
function Assert-PeakClosed {
    if(Get-Process PEAK -ErrorAction SilentlyContinue) { throw 'PEAK is running. Close it before changing plugins.' }
}
function Safe-GamePath([string]$Root,[string]$Relative) {
    $rootFull=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    if([IO.Path]::IsPathRooted($Relative)) { throw 'Expected a relative installation path.' }
    $full=[IO.Path]::GetFullPath((Join-Path $rootFull $Relative))
    if(-not $full.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes the game folder.' }
    $current=$full
    while($current -and $current.Length -ge $rootFull.Length) {
        if(Test-Path -LiteralPath $current) {
            if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked path requires manual installation: $current" }
        }
        $current=Split-Path $current -Parent
    }
    return $full
}
function Plugin-Ids([string]$Path) {
    $assembly=$null
    try {
        $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
        foreach($type in $assembly.MainModule.GetTypes()) {
            foreach($attribute in $type.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'}) {
                [PSCustomObject]@{Guid=[string]$attribute.ConstructorArguments[0].Value;Name=[string]$attribute.ConstructorArguments[1].Value;Version=[string]$attribute.ConstructorArguments[2].Value}
            }
        }
    } catch {
        if($_.Exception.ToString() -notmatch 'BadImageFormatException') { throw }
    } finally { if($assembly){$assembly.Dispose()} }
}
function Find-InstalledMod([string]$GamePath) {
    $plugins=Safe-GamePath $GamePath 'BepInEx\plugins'
    if(-not(Test-Path -LiteralPath $plugins)){return}
    foreach($file in Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -Recurse -File) {
        $ids=@(Plugin-Ids $file.FullName)
        if(@($ids | Where-Object Guid -eq '13dastardlyascents').Count) {
            if($ids.Count -ne 1) { throw "Mod is bundled with other plugins in $($file.FullName); manual separation required." }
            $file.FullName
        }
    }
}
function Assert-Hash([string]$Path,[string]$Expected) {
    if(-not(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) { throw "Hash mismatch: $Path" }
}

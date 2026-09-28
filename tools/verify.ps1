param([string]$GamePath='D:\Steam\steamapps\common\PEAK')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Add-Type -LiteralPath (Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll')
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GamePath 'PEAK_Data\Managed'))
$resolver.AddSearchDirectory((Join-Path $GamePath 'BepInEx\core'))
$parameters=[Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver=$resolver
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $projectRoot 'bin\Release\netstandard2.1\13dda.dll'),$parameters)
$errors=[Collections.Generic.List[string]]::new()
$patches=[Collections.Generic.List[object]]::new()
function Base-Field($type, $name) {
    while($type) {
        $field=$type.Fields | Where-Object Name -eq $name | Select-Object -First 1
        if($field) { return $field }
        $type=if($type.BaseType) {$type.BaseType.Resolve()} else {$null}
    }
}
function Bare-Type($type) { if($type.IsByReference) {$type.ElementType.FullName} else {$type.FullName} }
foreach($reference in $assembly.MainModule.GetMemberReferences()) {
    if($reference.DeclaringType.Scope.Name -match '^(Assembly-CSharp|Unity|BepInEx|0Harmony|Photon|DOTween|pworld|Zorro)') {
        try { if(-not $reference.Resolve()) { $errors.Add('Unresolved member: '+$reference.FullName) } }
        catch { $errors.Add('Resolution failed: '+$reference.FullName+' '+$_.Exception.Message) }
    }
}
foreach($type in $assembly.MainModule.GetTypes()) {
    foreach($attribute in $type.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}) {
        $args=@($attribute.ConstructorArguments | ForEach-Object Value)
        if($args.Count -lt 2) { $errors.Add('Incomplete patch target: '+$type.FullName); continue }
        $targetType=$args[0].Resolve()
        $targets=@($targetType.Methods | Where-Object Name -eq $args[1])
        if($targets.Count -ne 1) { $errors.Add('Nonunique/missing target: '+$type.FullName); continue }
        $target=$targets[0]
        $patches.Add([PSCustomObject]@{Patch=$type.FullName;Target=$target.FullName})
        foreach($method in $type.Methods | Where-Object {$_.Name -in @('Prefix','Postfix','Finalizer')}) {
            foreach($parameter in $method.Parameters) {
                $name=$parameter.Name
                if($name -in @('__state','__exception','__originalMethod','__runOriginal','__args')) {continue}
                if($name -eq '__instance') {
                    if($target.IsStatic) {$errors.Add('Static target with __instance: '+$method.FullName)}
                    continue
                }
                if($name -eq '__result') {
                    if((Bare-Type $parameter.ParameterType) -ne $target.ReturnType.FullName) {$errors.Add('Result mismatch: '+$method.FullName)}
                    continue
                }
                if($name.StartsWith('___')) {
                    $field=Base-Field $targetType $name.Substring(3)
                    if(-not $field) {$errors.Add('Missing injected field '+$name+': '+$method.FullName)}
                    elseif((Bare-Type $parameter.ParameterType) -ne $field.FieldType.FullName) {$errors.Add('Field type mismatch '+$name+': '+$method.FullName)}
                    continue
                }
                $original=$target.Parameters | Where-Object Name -eq $name | Select-Object -First 1
                if(-not $original) {$errors.Add('Missing argument '+$name+': '+$method.FullName)}
                elseif((Bare-Type $parameter.ParameterType) -ne (Bare-Type $original.ParameterType)) {$errors.Add('Argument type mismatch '+$name+': '+$method.FullName)}
            }
        }
    }
}
$forbidden=@('dda.WaitingFix','dda.TimeToMoveFix','dda.uberFog','dda.sharderHot','dda.nonStop','dda.dontStop','dda.Ascent7TurnOff','dda.doNotUnlock','dda.setAscents')
foreach($type in $assembly.MainModule.GetTypes()) {
    if($type.FullName -in $forbidden) {$errors.Add('Forbidden legacy override retained: '+$type.FullName)}
}
$report=[PSCustomObject]@{
    Status=if($errors.Count -eq 0){'PASSED_STATIC'}else{'FAILED'}
    PluginVersion=$assembly.Name.Version.ToString()
    GameVersion=(Get-Content -LiteralPath (Join-Path $GamePath 'version.txt') -TotalCount 1)
    GameAssemblySHA256=(Get-FileHash (Join-Path $GamePath 'PEAK_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash
    PluginSHA256=(Get-FileHash (Join-Path $projectRoot 'bin\Release\netstandard2.1\13dda.dll') -Algorithm SHA256).Hash
    Patches=$patches.ToArray(); Errors=$errors.ToArray(); GamePlayVerified=$false; MultiplayerVerified=$false
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\static-verification.json') -Encoding UTF8
$assembly.Dispose()
Write-Output ("Verified {0} patch targets; {1} static errors." -f $patches.Count,$errors.Count)
$errors | ForEach-Object {Write-Output $_}
if($errors.Count) {throw 'Static verification failed.'}

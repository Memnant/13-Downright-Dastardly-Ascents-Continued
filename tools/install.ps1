param([string]$GamePath='D:\Steam\steamapps\common\PEAK',[string]$DllPath,[switch]$Apply,
    [string]$NativeSavePath=(Join-Path $env:USERPROFILE 'AppData\LocalLow\LandCrab\PEAK\quicksave.peak'))
. (Join-Path $PSScriptRoot 'install-common.ps1')
$projectRoot=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath).TrimEnd('\')
if(-not $DllPath) {
    $DllPath=Join-Path $projectRoot 'BepInEx\plugins\PeakAscentsContinued\13dda.dll'
    if(-not(Test-Path -LiteralPath $DllPath)) { $DllPath=Join-Path $projectRoot 'artifacts\BepInEx\plugins\PeakAscentsContinued\13dda.dll' }
}
if((Get-Content -LiteralPath (Join-Path $GamePath 'version.txt') -TotalCount 1).Trim() -ne '2.5.a') {throw 'This candidate requires PEAK 2.5.a.'}
Add-Type -LiteralPath (Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll')
$sourceIds=@(Plugin-Ids $DllPath)
if($sourceIds.Count -ne 1 -or $sourceIds[0].Guid -ne '13dastardlyascents' -or $sourceIds[0].Version -ne '1.5.19') {throw 'Unexpected source plugin identity/version.'}
$targetRelative='BepInEx\plugins\PeakAscentsContinued\13dda.dll'
$target=Safe-GamePath $GamePath $targetRelative
$configRelative='BepInEx\config\13dastardlyascents.cfg'
$config=Safe-GamePath $GamePath $configRelative
$sidecarRoot=Safe-GamePath $GamePath 'BepInEx\config\13dda-continued\runs'
$sidecars=@(Get-ChildItem -LiteralPath $sidecarRoot -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -match '^[0-9a-f]{32}\.json(\.bak)?$'})
$NativeSavePath=[IO.Path]::GetFullPath($NativeSavePath)
$old=@(Find-InstalledMod $GamePath)
if((Test-Path -LiteralPath $target) -and $target -notin $old){throw 'Target path contains another plugin; refusing to overwrite it.'}
$sourceHash=(Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash
[PSCustomObject]@{Action=if($Apply){'INSTALL'}else{'PREVIEW_ONLY'};Target=$target;ExistingSameGuid=$old;ConfigBackup=Test-Path -LiteralPath $config;SidecarBackups=$sidecars.Count;NativeSaveBackup=(Test-Path -LiteralPath $NativeSavePath);SourceSHA256=$sourceHash} | Format-List
if(-not $Apply){Write-Output 'Preview only. Add -Apply to back up and install.';return}
Assert-PeakClosed
$backup=Join-Path $projectRoot ('backups\install-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $backup | Out-Null
$candidateSnapshot=Join-Path $backup 'candidate-13dda.dll'
Copy-Item -LiteralPath $DllPath -Destination $candidateSnapshot
Assert-Hash $candidateSnapshot $sourceHash
$entries=@()
$files=@($old)
if(Test-Path -LiteralPath $config){$files+=$config}
$files+=@($sidecars.FullName)
foreach($file in $files) {
    $relative=$file.Substring($GamePath.Length+1)
    $validated=Safe-GamePath $GamePath $relative
    $hash=(Get-FileHash -LiteralPath $validated -Algorithm SHA256).Hash
    $backupFile=Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path $backupFile -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $validated -Destination $backupFile
    Assert-Hash $backupFile $hash
    $entries += [PSCustomObject]@{RelativePath=$relative;SHA256=$hash;SaveMetadata=($relative -like 'BepInEx\config\13dda-continued\runs\*')}
}
$nativeExists=Test-Path -LiteralPath $NativeSavePath
$nativeHash=$null
if($nativeExists) {
    $nativeHash=(Get-FileHash -LiteralPath $NativeSavePath -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $NativeSavePath -Destination (Join-Path $backup 'native-quicksave.peak')
    Assert-Hash (Join-Path $backup 'native-quicksave.peak') $nativeHash
}
$manifest=[PSCustomObject]@{Schema=2;GamePath=$GamePath;TargetRelative=$targetRelative;InstalledSHA256=$sourceHash;OriginalConfigExisted=(Test-Path -LiteralPath $config);Entries=@($entries);NativeSavePath=$NativeSavePath;NativeSaveExisted=$nativeExists;NativeSaveSHA256=$nativeHash;Completed=$false}
$manifestPath=Join-Path $backup 'manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
# Every original file has a verified backup before the first replacement.
foreach($file in $old) {
    $entry=$entries | Where-Object RelativePath -eq $file.Substring($GamePath.Length+1)
    Assert-Hash $file $entry.SHA256
    Remove-Item -LiteralPath $file
}
New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
Copy-Item -LiteralPath $candidateSnapshot -Destination $target
Assert-Hash $target $sourceHash
$installed=@(Find-InstalledMod $GamePath)
if($installed.Count -ne 1 -or $installed[0] -ne $target){throw 'Unexpected duplicate plugin after installation; see backup manifest.'}
$manifest.Completed=$true
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Output "Installed Continued 1.5.19. Verified backup (including saved-run metadata): $backup"
Write-Output "Restore with tools/restore.ps1 -BackupDirectory `"$backup`" -Apply"

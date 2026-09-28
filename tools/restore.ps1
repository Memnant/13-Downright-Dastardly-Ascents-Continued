param([Parameter(Mandatory=$true)][string]$BackupDirectory,[switch]$Apply,[switch]$RestoreSaves,
    [string]$NativeSavePath=(Join-Path $env:USERPROFILE 'AppData\LocalLow\LandCrab\PEAK\quicksave.peak'))
. (Join-Path $PSScriptRoot 'install-common.ps1')
$BackupDirectory=[IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
$manifest=Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($manifest.Schema -notin @(1,2) -or $manifest.TargetRelative -ne 'BepInEx\plugins\PeakAscentsContinued\13dda.dll') {throw 'Unexpected backup manifest.'}
$NativeSavePath=[IO.Path]::GetFullPath($NativeSavePath)
if($RestoreSaves) {
    if($manifest.Schema -ne 2 -or $NativeSavePath -ne $manifest.NativeSavePath) {throw 'Save restore requires a matching native save path and schema-2 backup.'}
    if($manifest.NativeSaveExisted) { Assert-Hash (Join-Path $BackupDirectory 'native-quicksave.peak') $manifest.NativeSaveSHA256 }
}
$target=Safe-GamePath $manifest.GamePath $manifest.TargetRelative
foreach($entry in $manifest.Entries) {
    if($entry.RelativePath -ne 'BepInEx\config\13dastardlyascents.cfg' -and
        ($entry.RelativePath -notlike 'BepInEx\plugins\*.dll') -and
        ($entry.RelativePath -notmatch '^BepInEx\\config\\13dda-continued\\runs\\[0-9a-f]{32}\.json(\.bak)?$')) {throw 'Backup entry is outside the mod/config scope.'}
    $destination=Safe-GamePath $manifest.GamePath $entry.RelativePath
    if($entry.RelativePath -like '*.dll' -and $destination -ne $target -and (Test-Path -LiteralPath $destination)) {
        Assert-Hash $destination $entry.SHA256
    }
    $backupFile=Safe-GamePath $BackupDirectory $entry.RelativePath
    Assert-Hash $backupFile $entry.SHA256
}
if(Test-Path -LiteralPath $target){Assert-Hash $target $manifest.InstalledSHA256}
[PSCustomObject]@{Action=if($Apply){'RESTORE'}else{'PREVIEW_ONLY'};GamePath=$manifest.GamePath;RemoveInstalledDll=$target;RestoreSaves=[bool]$RestoreSaves;RestoreEntries=@($manifest.Entries | Where-Object { -not $_.SaveMetadata -or $RestoreSaves } | ForEach-Object RelativePath)} | Format-List
if(-not $Apply){Write-Output 'Preview only. Add -Apply to restore.';return}
Assert-PeakClosed
# Preserve the current configuration as well, including any user edits made after installation.
$config=Safe-GamePath $manifest.GamePath 'BepInEx\config\13dastardlyascents.cfg'
if(Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination (Join-Path $BackupDirectory ('config-before-restore-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.cfg')) }
if($RestoreSaves) {
    $recovery=Join-Path $BackupDirectory ('saves-before-restore-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $recovery | Out-Null
    $runDirectory=Safe-GamePath $manifest.GamePath 'BepInEx\config\13dda-continued\runs'
    $currentRecords=@(Get-ChildItem -LiteralPath $runDirectory -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -match '^[0-9a-f]{32}\.json(\.bak)?$'})
    foreach($record in $currentRecords) { Copy-Item -LiteralPath $record.FullName -Destination (Join-Path $recovery $record.Name); Assert-Hash (Join-Path $recovery $record.Name) (Get-FileHash -LiteralPath $record.FullName).Hash }
    if(Test-Path -LiteralPath $NativeSavePath) { Copy-Item -LiteralPath $NativeSavePath -Destination (Join-Path $recovery 'quicksave.peak'); Assert-Hash (Join-Path $recovery 'quicksave.peak') (Get-FileHash -LiteralPath $NativeSavePath).Hash }
    # Exact files only, all resolved beneath the audited game directory. No recursive deletes.
    foreach($record in $currentRecords) { $checked=Safe-GamePath $manifest.GamePath $record.FullName.Substring($manifest.GamePath.Length+1); Remove-Item -LiteralPath $checked }
    if($manifest.NativeSaveExisted) {
        New-Item -ItemType Directory -Path (Split-Path $NativeSavePath -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $BackupDirectory 'native-quicksave.peak') -Destination $NativeSavePath -Force
        Assert-Hash $NativeSavePath $manifest.NativeSaveSHA256
    } elseif(Test-Path -LiteralPath $NativeSavePath) { Remove-Item -LiteralPath $NativeSavePath }
    Write-Output "Current saves preserved at: $recovery"
}
if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
foreach($entry in $manifest.Entries) {
    if($entry.SaveMetadata -and -not $RestoreSaves) {continue}
    $destination=Safe-GamePath $manifest.GamePath $entry.RelativePath
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $BackupDirectory $entry.RelativePath) -Destination $destination -Force
    Assert-Hash $destination $entry.SHA256
}
if(-not $manifest.OriginalConfigExisted -and (Test-Path -LiteralPath $config)){Remove-Item -LiteralPath $config}
Write-Output 'Original DLL/config files restored. Saves change only when -RestoreSaves is specified; that option restores the native save and metadata together.'

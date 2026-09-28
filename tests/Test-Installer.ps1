param([string]$GamePath='D:\Steam\steamapps\common\PEAK')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $projectRoot ('artifacts\installer-fixture-'+[Guid]::NewGuid().ToString('N'))
$testGame=Join-Path $fixture 'PEAK'
$old=Join-Path $testGame 'BepInEx\plugins\old renamed mod\legacy.dll'
$core=Join-Path $testGame 'BepInEx\core'
$config=Join-Path $testGame 'BepInEx\config\13dastardlyascents.cfg'
$native=Join-Path $fixture 'userdata\quicksave.peak'
$record=Join-Path $testGame 'BepInEx\config\13dda-continued\runs\e354b651cbba4c0aaa49765432c2e50a.json'
New-Item -ItemType Directory -Path (Split-Path $native -Parent),(Split-Path $record -Parent) -Force | Out-Null
'native checkpoint before upgrade' | Set-Content -LiteralPath $native -Encoding UTF8
'legacy sidecar before upgrade' | Set-Content -LiteralPath $record -Encoding UTF8
$nativeHash=(Get-FileHash -LiteralPath $native).Hash
$recordHash=(Get-FileHash -LiteralPath $record).Hash
New-Item -ItemType Directory -Path (Split-Path $old -Parent),$core,(Split-Path $config -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'original\13dda.dll') -Destination $old
Copy-Item -LiteralPath (Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll') -Destination $core
'2.4.c' | Set-Content -LiteralPath (Join-Path $testGame 'version.txt') -Encoding UTF8
"[Ascents]`nAscent 9 = true`nAscent 8 = true" | Set-Content -LiteralPath $config -Encoding UTF8
$oldHash=(Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash
$configHash=(Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash
$candidate=Join-Path $projectRoot 'artifacts\BepInEx\plugins\PeakAscentsContinued\13dda.dll'
$target=Join-Path $testGame 'BepInEx\plugins\PeakAscentsContinued\13dda.dll'
& (Join-Path $projectRoot 'tools\install.ps1') -GamePath $testGame -DllPath $candidate -NativeSavePath $native
if(-not(Test-Path -LiteralPath $old) -or (Test-Path -LiteralPath $target)){throw 'Preview mutated plugins.'}
& (Join-Path $projectRoot 'tools\install.ps1') -GamePath $testGame -DllPath $candidate -NativeSavePath $native -Apply
if(Test-Path -LiteralPath $old){throw 'Old renamed DLL would still load.'}
if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash){throw 'Candidate differs after install.'}
$backup=Get-ChildItem -LiteralPath (Join-Path $projectRoot 'backups') -Filter manifest.json -Recurse -File |
    Where-Object { (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).GamePath -eq $testGame } | Select-Object -Last 1
if(-not $backup){throw 'Missing backup manifest.'}
'edited config after install' | Set-Content -LiteralPath $config -Encoding UTF8
'native checkpoint after upgrade' | Set-Content -LiteralPath $native -Encoding UTF8
'schema 2 sidecar after upgrade' | Set-Content -LiteralPath $record -Encoding UTF8
& (Join-Path $projectRoot 'tools\restore.ps1') -BackupDirectory $backup.DirectoryName -RestoreSaves -NativeSavePath $native -Apply
if((Get-FileHash -LiteralPath $native).Hash -ne $nativeHash -or (Get-FileHash -LiteralPath $record).Hash -ne $recordHash) {throw 'Native save and sidecar pair failed to restore.'}
if((Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash -ne $oldHash){throw 'Old DLL failed to restore.'}
if((Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash -ne $configHash){throw 'Original config failed to restore.'}
if(Test-Path -LiteralPath $target){throw 'Continued DLL remained after restore.'}
if(-not(Get-ChildItem -LiteralPath $backup.DirectoryName -Filter 'config-before-restore-*.cfg')){throw 'User config edit was not preserved.'}
# A normal rollback must not rewind progress.
& (Join-Path $projectRoot 'tools\install.ps1') -GamePath $testGame -DllPath $candidate -NativeSavePath $native -Apply
$secondBackup=Get-ChildItem -LiteralPath (Join-Path $projectRoot 'backups') -Filter manifest.json -Recurse -File |
    Where-Object { (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).GamePath -eq $testGame -and $_.FullName -ne $backup.FullName } | Select-Object -Last 1
'progress to preserve' | Set-Content -LiteralPath $native -Encoding UTF8
'metadata to preserve' | Set-Content -LiteralPath $record -Encoding UTF8
$latestNative=(Get-FileHash -LiteralPath $native).Hash
$latestRecord=(Get-FileHash -LiteralPath $record).Hash
& (Join-Path $projectRoot 'tools\restore.ps1') -BackupDirectory $secondBackup.DirectoryName -Apply
if((Get-FileHash -LiteralPath $native).Hash -ne $latestNative -or (Get-FileHash -LiteralPath $record).Hash -ne $latestRecord) {throw 'Default rollback changed saved progress.'}
# Manifest traversal must be rejected before any file mutation.
. (Join-Path $projectRoot 'tools\install-common.ps1')
$rejected=$false
try { $null=Safe-GamePath $testGame '..\outside.dll' } catch { $rejected=$true }
if(-not $rejected){throw 'Traversal not rejected.'}
[PSCustomObject]@{Status='PASSED_INSTALL_RESTORE_FIXTURE';PluginSHA256=(Get-FileHash -LiteralPath $candidate).Hash;Fixture=$fixture;RealGameModified=$false;RenamedDllFound=$true;PreviewReadOnly=$true;HashesRestored=$true;NewConfigEditPreserved=$true;NativeAndSidecarPairRestored=$true;DefaultRollbackPreservesProgress=$true;PathTraversalRejected=$true} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\installer-tests.json') -Encoding UTF8
Write-Output 'Installer/restore fixture passed; the actual game was not modified.'

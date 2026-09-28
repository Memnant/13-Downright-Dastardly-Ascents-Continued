param([string]$GamePath='D:\Steam\steamapps\common\PEAK')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if (Get-Process PEAK -ErrorAction SilentlyContinue) { throw 'Close PEAK before starting the isolated load test.' }
& (Join-Path $PSScriptRoot 'build.ps1') -GamePath $GamePath
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build (Join-Path $projectRoot 'tests\SmokeHarness\SmokeHarness.csproj') -c Release "-p:GamePath=$GamePath" --nologo
if ($LASTEXITCODE) { throw 'Smoke harness build failed.' }
$testRoot=Join-Path $projectRoot ('artifacts\smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$bepRoot=Join-Path $testRoot 'BepInEx'
$core=Join-Path $bepRoot 'core'
$plugins=Join-Path $bepRoot 'plugins'
$config=Join-Path $bepRoot 'config'
New-Item -ItemType Directory -Path $core,$plugins,$config -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $GamePath 'BepInEx\core') -File | Copy-Item -Destination $core
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\netstandard2.1\13dda.dll') -Destination $plugins
Copy-Item -LiteralPath (Join-Path $projectRoot 'tests\SmokeHarness\bin\Release\netstandard2.1\SmokeHarness.dll') -Destination $plugins
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'override-members.json') -Destination $bepRoot
@'
[Logging.Console]
Enabled = false
[Logging.Disk]
Enabled = true
InstantFlushing = true
[Chainloader]
HideManagerGameObject = false
'@ | Set-Content -LiteralPath (Join-Path $config 'BepInEx.cfg') -Encoding UTF8
$assemblyBefore=(Get-FileHash -LiteralPath (Join-Path $GamePath 'PEAK_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash
$gameLog=Join-Path $testRoot 'unity-player.log'
$targetAssembly=Join-Path $core 'BepInEx.Preloader.dll'
$launchArgs=@('--doorstop-enabled','true','--doorstop-target-assembly',('"'+$targetAssembly+'"'),'-batchmode','-nographics','-logFile',('"'+$gameLog+'"'))
$nativeSave=Join-Path $env:USERPROFILE 'AppData\LocalLow\LandCrab\PEAK\quicksave.peak'
$saveBefore=if(Test-Path -LiteralPath $nativeSave){(Get-FileHash -LiteralPath $nativeSave -Algorithm SHA256).Hash}else{$null}
$oldSteamAppId=$env:SteamAppId
$oldSteamGameId=$env:SteamGameId
try {
    # Test-child environment only; avoids Steam's automatic relaunch discarding isolation arguments.
    $env:SteamAppId='3527290'
    $env:SteamGameId='3527290'
    $process=Start-Process -FilePath (Join-Path $GamePath 'PEAK.exe') -ArgumentList $launchArgs -WorkingDirectory $GamePath -WindowStyle Hidden -PassThru
} finally { $env:SteamAppId=$oldSteamAppId; $env:SteamGameId=$oldSteamGameId }
$deadline=(Get-Date).AddSeconds(55)
while (-not $process.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500; $process.Refresh() }
$timedOut=-not $process.HasExited
if ($timedOut) {
    # Only terminate the exact test process we just launched, never an existing game process.
    Stop-Process -Id $process.Id
    $process.WaitForExit()
}
$assemblyAfter=(Get-FileHash -LiteralPath (Join-Path $GamePath 'PEAK_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash
$resultFile=Join-Path $bepRoot 'smoke-result.txt'
$result=if(Test-Path -LiteralPath $resultFile){Get-Content -LiteralPath $resultFile -Raw}else{'NO_HARNESS_RESULT'}
$saveAfter=if(Test-Path -LiteralPath $nativeSave){(Get-FileHash -LiteralPath $nativeSave -Algorithm SHA256).Hash}else{$null}
$report=[PSCustomObject]@{Status=if($result.StartsWith('PASSED_LOAD') -and $assemblyBefore -eq $assemblyAfter -and $saveBefore -eq $saveAfter){'PASSED_LOAD'}else{'UNVERIFIED_OR_FAILED'};TimedOut=$timedOut;TestDirectory=$testRoot;GameAssemblyUnchanged=$assemblyBefore -eq $assemblyAfter;NativeSaveUnchanged=$saveBefore -eq $saveAfter;PluginSHA256=(Get-FileHash -LiteralPath (Join-Path $plugins '13dda.dll') -Algorithm SHA256).Hash;GamePlayVerified=$false;MultiplayerVerified=$false;HarnessResult=$result}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\smoke-verification.json') -Encoding UTF8
$report | Format-List
if (Test-Path -LiteralPath (Join-Path $bepRoot 'LogOutput.log')) { Get-Content -LiteralPath (Join-Path $bepRoot 'LogOutput.log') -Tail 70 }
if ($report.Status -ne 'PASSED_LOAD') { throw 'Isolated load was not verified; inspect the retained logs.' }

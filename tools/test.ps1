param([string]$GamePath='D:\Steam\steamapps\common\PEAK',[string]$BaselineDll)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -GamePath $GamePath
& (Join-Path $PSScriptRoot 'verify.ps1') -GamePath $GamePath
$testArguments=@((Join-Path $projectRoot 'artifacts\policy-tests.json'))
if($BaselineDll) { $testArguments += $BaselineDll }
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') run --project (Join-Path $projectRoot 'tests\PolicyTests\PolicyTests.csproj') --configuration Release -- @testArguments
if ($LASTEXITCODE) { throw 'Policy tests failed.' }

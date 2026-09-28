param([string]$GamePath = 'D:\Steam\steamapps\common\PEAK')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'src\Resources\NativeAlpineSnow.bundle'))) {
    throw 'Native snow resource missing. Install requirements-dev.txt, then run python tools/extract-native-snow.py --game <your PEAK path>. See docs/BUILD.md.'
}
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools\dotnet'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
if (-not (Test-Path -LiteralPath (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) { & (Join-Path $PSScriptRoot 'bootstrap.ps1') -GamePath $GamePath }
Push-Location $projectRoot
try {
    & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build 'PeakAscentsContinued.csproj' --configuration Release "-p:GamePath=$GamePath" --nologo
    if ($LASTEXITCODE) { throw 'Build failed.' }
    New-Item -ItemType Directory -Path 'artifacts\BepInEx\plugins\PeakAscentsContinued' -Force | Out-Null
    Copy-Item -LiteralPath 'bin\Release\netstandard2.1\13dda.dll' -Destination 'artifacts\BepInEx\plugins\PeakAscentsContinued\13dda.dll' -Force
    Get-FileHash -LiteralPath 'artifacts\BepInEx\plugins\PeakAscentsContinued\13dda.dll' -Algorithm SHA256
} finally { Pop-Location }

param([string]$GamePath = 'D:\Steam\steamapps\common\PEAK', [switch]$DecompileOriginal)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$toolRoot = Join-Path $projectRoot '.tools'
$sdkRoot = Join-Path $toolRoot 'dotnet'
$env:DOTNET_ROOT = $sdkRoot
$env:DOTNET_CLI_HOME = Join-Path $toolRoot 'cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $toolRoot 'nuget'
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$download = Get-Content (Join-Path $PSScriptRoot 'sdk-download.json') -Raw | ConvertFrom-Json
if (-not $download.Url -or -not $download.Sha512) { throw 'Missing verified SDK download metadata.' }
if (-not (Test-Path (Join-Path $sdkRoot 'dotnet.exe'))) {
    $archive = Join-Path $toolRoot 'dotnet-sdk.zip'
    if (-not (Test-Path $archive)) {
        Write-Output "Downloading Microsoft .NET SDK $($download.Version)..."
        $webOptions = @{ Uri=$download.Url; OutFile=$archive; TimeoutSec=600 }
        if ($env:HTTPS_PROXY) { $webOptions.Proxy=$env:HTTPS_PROXY }
        Invoke-WebRequest @webOptions
    }
    if ((Get-FileHash $archive -Algorithm SHA512).Hash -ne $download.Sha512) { throw 'SDK checksum mismatch.' }
    Write-Output 'SDK checksum verified. Extracting into project tools directory...'
    Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot -Force
}
$dotnet = Join-Path $sdkRoot 'dotnet.exe'
& $dotnet --version
if ($LASTEXITCODE) { throw 'SDK failed.' }
if (-not $DecompileOriginal) { Write-Output 'Local SDK is ready; run tools/build.ps1.'; return }
$ilspy = Join-Path $toolRoot 'bin\ilspycmd.exe'
if (-not (Test-Path $ilspy)) {
    & $dotnet tool install ilspycmd --version 11.0.0.9375 --tool-path (Join-Path $toolRoot 'bin') --source 'https://api.nuget.org/v3/index.json'
    if ($LASTEXITCODE) { throw 'ILSpy install failed.' }
}
& $ilspy --version
if ($LASTEXITCODE) { throw 'ILSpy failed.' }
& $ilspy --project --outputdir (Join-Path $projectRoot 'original\decompiled') --referencepath (Join-Path $GamePath 'PEAK_Data\Managed') (Join-Path $projectRoot 'original\13dda.dll')
if ($LASTEXITCODE) { throw 'Decompilation failed.' }
Write-Output 'Original DLL decompiled successfully.'

$ErrorActionPreference = "Stop"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet -and (Test-Path "C:\Program Files\dotnet\dotnet.exe")) {
	$dotnet = "C:\Program Files\dotnet\dotnet.exe"
}
if (-not $dotnet -and (Test-Path "C:\Users\Z\.dotnet\dotnet.exe")) {
	$dotnet = "C:\Users\Z\.dotnet\dotnet.exe"
}

if (-not $dotnet) {
	Write-Host ".NET SDK not found."
	Write-Host "Install .NET 8 SDK, close this terminal, open a new one, and run this script again."
	exit 1
}

$publishDir = Join-Path $PSScriptRoot ".publish_tmp"
$rootExe = Join-Path $PSScriptRoot "Prisac.exe"
$dotnetHome = Join-Path $PSScriptRoot ".dotnet_home"
$nugetConfig = Join-Path $PSScriptRoot "NuGet.Config"
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot ".nuget_packages"
$env:APPDATA = Join-Path $dotnetHome "AppData\Roaming"
$env:LOCALAPPDATA = Join-Path $dotnetHome "AppData\Local"

New-Item -ItemType Directory -Force $dotnetHome | Out-Null
New-Item -ItemType Directory -Force $env:APPDATA | Out-Null
New-Item -ItemType Directory -Force $env:LOCALAPPDATA | Out-Null

if (Test-Path $publishDir) {
	Remove-Item $publishDir -Recurse -Force
}

& $dotnet restore $PSScriptRoot --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet publish $PSScriptRoot -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --no-restore -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $publishDir "Prisac.exe") $rootExe -Force
Copy-Item (Join-Path $publishDir "SDL2.dll") $PSScriptRoot -Force
Copy-Item (Join-Path $publishDir "soft_oal.dll") $PSScriptRoot -Force
Remove-Item $publishDir -Recurse -Force

Write-Host "Built $rootExe"
Write-Host "Keep SDL2.dll, soft_oal.dll, and the Content folder next to Prisac.exe."

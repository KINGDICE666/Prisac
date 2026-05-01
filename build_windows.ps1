$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	Write-Host ".NET SDK not found."
	Write-Host "Install .NET 8 SDK, close this terminal, open a new one, and run this script again."
	exit 1
}

$publishDir = Join-Path $PSScriptRoot "publish"

dotnet restore $PSScriptRoot
dotnet publish $PSScriptRoot -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir

Write-Host "Built $publishDir\Prisac.exe"

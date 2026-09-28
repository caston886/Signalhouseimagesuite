# Scaffolds the .sln with the .NET CLI (safer than a hand-written .sln file)
# and restores packages. Run this once after cloning, from a machine with the
# .NET 8 SDK installed.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK was not found on PATH. Install .NET 8 SDK from https://dotnet.microsoft.com/download first."
    exit 1
}

dotnet new sln -n SignalHouseImagingSuite --force
dotnet sln add src/SignalHouse.Core/SignalHouse.Core.csproj
dotnet sln add src/SignalHouse.App/SignalHouse.App.csproj
dotnet restore

Write-Host ""
Write-Host "Solution created. Build with:  dotnet build"
Write-Host "Run with:                      dotnet run --project src/SignalHouse.App"

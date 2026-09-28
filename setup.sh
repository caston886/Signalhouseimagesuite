#!/usr/bin/env bash
# Scaffolds the .sln with the .NET CLI (safer than a hand-written .sln file)
# and restores packages. Run this once after cloning, from a machine with the
# .NET 8 SDK installed.
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET SDK was not found on PATH. Install .NET 8 SDK from https://dotnet.microsoft.com/download first." >&2
  exit 1
fi

dotnet new sln -n SignalHouseImagingSuite --force
dotnet sln add src/SignalHouse.Core/SignalHouse.Core.csproj
dotnet sln add src/SignalHouse.App/SignalHouse.App.csproj
dotnet restore

echo
echo "Solution created. Build with:  dotnet build"
echo "Run with:                      dotnet run --project src/SignalHouse.App"

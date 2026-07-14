#!/usr/bin/env bash
# Switch the repo to build the LibreWPF portable leg with the public LibreWPF.Sdk
# preview from nuget.org (no locally built ~/wpf checkout required).
#
# Usage: eng/use-public-librewpf.sh
# Afterwards build with: dotnet build ... -p:LibreWpfTargetFramework=net10.0-windows
set -euo pipefail
cd "$(dirname "$0")/.."
cp eng/librewpf-public/global.json global.json
cp eng/librewpf-public/NuGet.config source/NuGet.config
echo "Switched to public LibreWPF.Sdk ($(grep -o '0\.1\.0-preview\.[0-9]*' global.json)). Do not commit these changes."

#!/usr/bin/env bash
# Switch the repo to build against a locally built LibreWPF (~/wpf checkout,
# SDK/packages version 11.0.0-dev) instead of the default public LibreWPF.Sdk
# preview from nuget.org.
#
# Usage: eng/use-local-librewpf.sh
# Afterwards build with: dotnet build ... -p:LibreWpfTargetFramework=net11.0-windows
# Don't commit the swapped global.json / source/NuGet.config.
set -euo pipefail
cd "$(dirname "$0")/.."
cp eng/librewpf-local/global.json global.json
cp eng/librewpf-local/NuGet.config source/NuGet.config
echo "Switched to the locally built LibreWPF SDK (11.0.0-dev from ../wpf). Do not commit these changes."

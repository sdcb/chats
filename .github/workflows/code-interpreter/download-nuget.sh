#!/usr/bin/env bash
set -euo pipefail

mkdir -p /opt/nuget-local

for pkg in ${PRECACHE_PACKAGES:-}; do
  echo "Downloading $pkg and dependencies..."
  cd /tmp
  mkdir -p nuget-temp
  cd nuget-temp

  dotnet new console -n temp --no-restore
  cd temp
  dotnet add package "$pkg"
  dotnet restore --packages /opt/nuget-local

  cd /tmp
  rm -rf nuget-temp
done

echo "NuGet packages cached:"
ls -la /opt/nuget-local

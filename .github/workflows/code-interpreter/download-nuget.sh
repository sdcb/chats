#!/usr/bin/env bash
set -euo pipefail

mkdir -p /opt/nuget-local

if [[ -z "${PRECACHE_PACKAGES:-}" ]]; then
  echo "No PRECACHE_PACKAGES specified; skipping NuGet precache."
  exit 0
fi

echo "Preparing NuGet cache for: ${PRECACHE_PACKAGES}"

cd /tmp
rm -rf nuget-temp
mkdir -p nuget-temp
cd nuget-temp

dotnet new console -n temp --no-restore
cd temp

for pkg in ${PRECACHE_PACKAGES}; do
  echo "Adding package $pkg..."
  dotnet add package "$pkg" --no-restore
done

echo "Restoring all packages to /opt/nuget-local..."
dotnet restore --packages /opt/nuget-local

cd /tmp
rm -rf nuget-temp

echo "NuGet packages cached:"
ls -la /opt/nuget-local

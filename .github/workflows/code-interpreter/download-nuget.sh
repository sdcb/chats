#!/usr/bin/env bash
set -euo pipefail

mkdir -p /opt/nuget-local

if [[ -z "${PRECACHE_PACKAGES:-}" ]]; then
  echo "No PRECACHE_PACKAGES specified; skipping NuGet precache."
  exit 0
fi

echo "Preparing NuGet cache for: ${PRECACHE_PACKAGES}"

# We intentionally restore into a temporary global-packages folder, then export
# all downloaded .nupkg files into /opt/nuget-local as a simple folder source.
# This enables offline restores when nuget.org is unavailable.
TMP_PACKAGES_DIR="/tmp/nuget-packages"
rm -rf "${TMP_PACKAGES_DIR}"
mkdir -p "${TMP_PACKAGES_DIR}"

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
NUGET_PACKAGES="${TMP_PACKAGES_DIR}" dotnet restore

echo "Exporting .nupkg files to /opt/nuget-local..."
find "${TMP_PACKAGES_DIR}" -type f -name "*.nupkg" -print0 \
  | xargs -0 -I {} cp -f {} /opt/nuget-local/

rm -rf "${TMP_PACKAGES_DIR}"

cd /tmp
rm -rf nuget-temp

echo "NuGet packages cached:"
ls -la /opt/nuget-local

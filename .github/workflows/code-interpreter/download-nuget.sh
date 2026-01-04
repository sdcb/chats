#!/usr/bin/env bash
set -euo pipefail

mkdir -p /opt/nuget-local

if [[ -z "${PRECACHE_PACKAGES:-}" ]]; then

# Restore each top-level package in an isolated temporary project. This avoids
# version conflicts between unrelated top-level packages and ensures we cache
# the dependency closure for each package (so offline restores work).
for pkg in ${PRECACHE_PACKAGES}; do
  rm -rf /tmp/nuget-temp/pkg
  mkdir -p /tmp/nuget-temp/pkg
  cd /tmp/nuget-temp/pkg

  dotnet new console -n temp --no-restore
  cd temp

  if [[ "$pkg" == *"@"* ]]; then
    name="${pkg%@*}"
    version="${pkg#*@}"
    echo "Adding package ${name} (${version})..."
    dotnet add package "${name}" --version "${version}" --no-restore
  else
    echo "Adding package $pkg..."
    dotnet add package "$pkg" --no-restore
  fi

  echo "Restoring ${pkg} to /opt/nuget-local..."
  NUGET_PACKAGES="${TMP_PACKAGES_DIR}" dotnet restore
done
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

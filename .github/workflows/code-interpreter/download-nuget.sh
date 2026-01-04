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

WORK_ROOT="/tmp/nuget-temp"
rm -rf "${WORK_ROOT}"
mkdir -p "${WORK_ROOT}"

add_package_reference() {
  local pkg_token="$1"
  if [[ "${pkg_token}" == *"@"* ]]; then
    local name="${pkg_token%@*}"
    local version="${pkg_token#*@}"
    echo "Adding package ${name} (${version})..."
    dotnet add package "${name}" --version "${version}" --no-restore
  else
    echo "Adding package ${pkg_token}..."
    dotnet add package "${pkg_token}" --no-restore
  fi
}

# Restore each top-level package in an isolated temporary project. This avoids
# version conflicts between unrelated top-level packages and ensures we cache
# the dependency closure for each package (so offline restores work).
for pkg in ${PRECACHE_PACKAGES}; do
  rm -rf "${WORK_ROOT}/pkg"
  mkdir -p "${WORK_ROOT}/pkg"
  cd "${WORK_ROOT}/pkg"

  dotnet new console -n temp --no-restore
  cd temp

  add_package_reference "${pkg}"

  echo "Restoring ${pkg} to /opt/nuget-local..."
  NUGET_PACKAGES="${TMP_PACKAGES_DIR}" dotnet restore
done

echo "Exporting .nupkg files to /opt/nuget-local..."
find "${TMP_PACKAGES_DIR}" -type f -name "*.nupkg" -print0 \
  | xargs -0 -I {} cp -f {} /opt/nuget-local/

rm -rf "${TMP_PACKAGES_DIR}"

rm -rf "${WORK_ROOT}"

echo "NuGet packages cached:"
ls -la /opt/nuget-local

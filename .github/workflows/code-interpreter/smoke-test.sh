#!/usr/bin/env bash
set -euo pipefail

if [[ "${RUNNING_IN_CONTAINER:-}" != "1" ]]; then
  IMAGE="${IMAGE:?IMAGE env is required}"

  # Execute all checks in a single container run.
  docker run --rm --network none \
    -e RUNNING_IN_CONTAINER=1 \
    -v "$(pwd)/.github/workflows/code-interpreter/smoke-test.sh:/tmp/smoke-test.sh:ro" \
    "$IMAGE" bash /tmp/smoke-test.sh
  exit 0
fi

echo "=== .NET Version ==="
dotnet --version

echo "=== Git Version ==="
git --version

echo "=== FFmpeg Version ==="
ffmpeg -version | head -n 1

echo "=== Python Version ==="
python3 --version

echo "=== GCC Version ==="
gcc --version | head -n 1

echo "=== Node.js Version ==="
node --version

echo "=== file(1) Version ==="
file --version | head -n 1

echo "=== SQLite Version ==="
sqlite3 --version

echo "=== Python Packages ==="
python3 -c "import numpy, pandas, matplotlib, scipy, PIL, requests, openpyxl; print('All packages imported successfully')"

echo "=== NuGet Local Cache ==="
ls /opt/nuget-local

echo "=== NuGet Config ==="
cat /etc/nuget/NuGet.Config

echo "=== Test ClosedXML restore from local cache ==="
get_local_version() {
	local pkg="$1"
	local pkg_lc="${pkg,,}"
	local prefix="${pkg_lc}."
	local ver
	ver="$(
		find /opt/nuget-local -maxdepth 1 -type f -iname "${pkg}.*.nupkg" -printf '%f\n' |
			while IFS= read -r f; do
				local file_lc="${f,,}"
				if [[ "${file_lc}" != ${prefix}*.nupkg ]]; then
					continue
				fi
				local v="${file_lc#${prefix}}"
				v="${v%.nupkg}"
				# Filter out sub-packages like ClosedXML.Parser (closedxml.parser.2.0.0.nupkg).
				if [[ "${v}" =~ ^[0-9] ]]; then
					echo "${v}"
				fi
			done |
			sort -V |
			tail -n 1
	)"
	if [[ -z "${ver}" ]]; then
		echo "ERROR: ${pkg} not found in /opt/nuget-local" >&2
		return 1
	fi
	echo "${ver}"
}

closedxml_ver="$(get_local_version ClosedXML)"
echo "Using ClosedXML version from /opt/nuget-local: ${closedxml_ver}"

cd /tmp
rm -rf test
mkdir -p test
cd test
dotnet new console -n test --no-restore
cd test

dotnet add package ClosedXML --version "${closedxml_ver}" --no-restore

restore_log="$(dotnet restore -p:NuGetAudit=false -v n 2>&1)"
echo "${restore_log}"

echo "${restore_log}" | grep -E '(/opt/nuget-local|Installed|Restored)'

echo "=== Skills.md ==="
cat /app/skills.md

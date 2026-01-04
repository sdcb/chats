#!/usr/bin/env bash
set -euo pipefail

IMAGE="${IMAGE:?IMAGE env is required}"

echo "=== .NET Version ==="
docker run --rm "$IMAGE" dotnet --version

echo "=== FFmpeg Version ==="
docker run --rm "$IMAGE" ffmpeg -version | head -n 1

echo "=== Python Version ==="
docker run --rm "$IMAGE" python3 --version

echo "=== GCC Version ==="
docker run --rm "$IMAGE" gcc --version | head -n 1

echo "=== Node.js Version ==="
docker run --rm "$IMAGE" node --version

echo "=== file(1) Version ==="
docker run --rm "$IMAGE" file --version | head -n 1

echo "=== SQLite Version ==="
docker run --rm "$IMAGE" sqlite3 --version

echo "=== Python Packages ==="
docker run --rm "$IMAGE" python3 -c "import numpy, pandas, matplotlib, scipy, PIL, requests, openpyxl; print('All packages imported successfully')"

echo "=== NuGet Local Cache ==="
docker run --rm "$IMAGE" ls /opt/nuget-local

echo "=== NuGet Config ==="
docker run --rm "$IMAGE" cat /etc/nuget/NuGet.Config

echo "=== Test ClosedXML restore from local cache ==="
docker run --rm "$IMAGE" bash -c "cd /tmp && mkdir -p test && cd test && dotnet new console -n test --no-restore && cd test && dotnet add package ClosedXML && dotnet restore -v n 2>&1 | grep -E '(local|Restored)'"

echo "=== Skills.md ==="
docker run --rm "$IMAGE" cat /app/skills.md

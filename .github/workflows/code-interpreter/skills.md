# Code Interpreter Skills

This environment is pre-configured with the following tools and libraries:

## .NET SDK {dotnetVersion}
- Commands: `dotnet build`, `dotnet run single-file.cs`, `dotnet add package`
- Pre-cached NuGet packages (in /opt/nuget-local):
  {all packages in this format: `* PackageName Version`}

## Python {pythonVersion}
- Commands: `python3 - <<'PY' ...`, `pip install --break-system-packages package-name`
- Pre-installed packages: numpy, pandas, matplotlib, scipy, pillow, requests, openpyxl

## FFmpeg {ffmpegVersion}
- Usage: Audio/video processing
- Commands: `ffmpeg`, `ffprobe`

## C/C++ Toolchain (gcc {gccVersion})
- Tools: `gcc`, `g++`, `make`, `cmake`
- Utility: `file`

## Node.js {nodeVersion}
- Commands: `node`, `npm`

## SQLite
- Commands: `sqlite3`

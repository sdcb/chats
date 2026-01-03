# Code Interpreter Skills

This environment is pre-configured with the following tools and libraries:

## .NET SDK 10.0
- Version: 10.0
- Commands: `dotnet build`, `dotnet run single-file.cs`, `dotnet add package`
- Pre-cached NuGet packages (in /opt/nuget-local):
  * ClosedXML 0.105.0

## Python 3.12
- Commands: `python3 - <<'PY' ...`, `pip install package-name`
- Pre-installed packages: numpy, pandas, matplotlib, scipy, pillow, requests

## FFmpeg
- Usage: Audio/video processing
- Commands: `ffmpeg`, `ffprobe`

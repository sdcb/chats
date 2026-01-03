# Code Interpreter Skills

This environment is pre-configured with the following tools and libraries:

## .NET SDK {dotnetVersion}
- Commands: `dotnet build`, `dotnet run single-file.cs`, `dotnet add package`
- Pre-cached NuGet packages (in /opt/nuget-local):
  {all packages in this format: `* PackageName Version`}

## Python {pythonVersion}
- Commands: `python3 - <<'PY' ...`, `pip install package-name`
- Pre-installed packages: numpy, pandas, matplotlib, scipy, pillow, requests

## FFmpeg {ffmpegVersion}
- Usage: Audio/video processing
- Commands: `ffmpeg`, `ffprobe`

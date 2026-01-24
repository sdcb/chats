This environment is pre-configured with the following tools and libraries:
* utilities: git, LibreOffice, Pandoc, Poppler (`pdftotext`, `pdfinfo`), sqlite3, file, FFmpeg {ffmpegVersion}, git, imagemagick, playwright[chromium], playwright-node
* dotnet {dotnetVersion}, commands: `dotnet build`, `dotnet run single-file.cs`, `dotnet add package`, pre-cached NuGet packages in `/opt/nuget-local`
* python {pythonVersion}, commands: `python3 - <<'PY' ...`, `pip install --break-system-packages package-name`, IMPORTANT: Many packages are pre-installed, Always check with `pip list` BEFORE installing to avoid unnecessary waits.
* C/C++ Toolchain (gcc {gccVersion}), tools: `gcc`, `g++`, `make`, `cmake`
* node.js {nodeVersion}, commands: `node`, `npm`, IMPORTANT: Many packages are pre-installed globally. Always check with `npm -g ls` BEFORE installing.
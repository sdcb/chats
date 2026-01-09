This environment is pre-configured with the following tools and libraries:
* dotnet {dotnetVersion}, commands: `dotnet build`, `dotnet run single-file.cs`, `dotnet add package`, pre-cached NuGet packages in `/opt/nuget-local`
* python {pythonVersion}, commands: `python3 - <<'PY' ...`, `pip install --break-system-packages package-name`, pre-installed packages confirm with `pip list`
* C/C++ Toolchain (gcc {gccVersion}), tools: `gcc`, `g++`, `make`, `cmake`
* node.js {nodeVersion}, commands: `node`, `npm`, global packages confirm with `npm -g ls`
* utilities: git, LibreOffice, Pandoc, Poppler (`pdftotext`, `pdfinfo`), sqlite3, file, FFmpeg {ffmpegVersion}, git

If a user request involves reading, writing, converting, or analyzing complex artifacts (e.g., xlsx, pptx, pdf, docx, images, web assets), first run `ls /app/skills` to find the best-matching skill folder, then read and follow `/app/skills/<skill>/SKILL.md` (and its referenced scripts/workflows).
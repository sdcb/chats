#!/usr/bin/env python3
import re
import subprocess
import sys
import os
from pathlib import Path


def run_text(cmd: list[str]) -> str:
    return subprocess.check_output(cmd, text=True, stderr=subprocess.STDOUT).strip()


def ffmpeg_version() -> str:
    first_line = run_text(["ffmpeg", "-version"]).splitlines()[0]
    m = re.search(r"ffmpeg\s+version\s+([^\s]+)", first_line)
    return m.group(1) if m else first_line


def python_version() -> str:
    out = run_text(["python3", "--version"])
    # Typical output: "Python 3.12.3"
    m = re.search(r"Python\s+([^\s]+)", out)
    return m.group(1) if m else out


def dotnet_version() -> str:
    return run_text(["dotnet", "--version"])


def gcc_version() -> str:
    try:
        # Produces a clean version string like "13.2.0" on Debian/Ubuntu.
        out = run_text(["gcc", "-dumpfullversion", "-dumpversion"]).strip()
        return out
    except Exception:
        first_line = run_text(["gcc", "--version"]).splitlines()[0]
        m = re.search(r"\)\s+([0-9]+\.[0-9]+(?:\.[0-9]+)?)", first_line)
        return m.group(1) if m else first_line


def node_version() -> str:
    out = run_text(["node", "--version"]).strip()
    # Typical output: "v20.10.0"
    return out[1:] if out.startswith("v") else out


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: render-skills.py <template.md> <output.md>", file=sys.stderr)
        return 2

    template_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])

    template = template_path.read_text(encoding="utf-8")

    dn = dotnet_version()
    py = python_version()
    ff = ffmpeg_version()
    gcc = gcc_version()
    node = node_version()
    run_number = os.getenv("RUN_NUMBER", "unknown")

    rendered = template
    rendered = rendered.replace("{dotnetVersion}", dn)
    rendered = rendered.replace("{pythonVersion}", py)
    rendered = rendered.replace("{ffmpegVersion}", ff)
    rendered = rendered.replace("{gccVersion}", gcc)
    rendered = rendered.replace("{nodeVersion}", node)
    rendered = rendered.replace("{runNumber}", run_number)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(rendered.rstrip() + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

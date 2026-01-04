#!/usr/bin/env python3
import os
import re
import subprocess
import sys
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


def version_key(version: str):
    parts = re.split(r"[\.-]", version)
    key = []
    for p in parts:
        if p.isdigit():
            key.append((0, int(p)))
        else:
            key.append((1, p.lower()))
    return key


def find_cached_nuget_version(package_name: str, cache_root: Path) -> str | None:
    # cache_root is expected to contain exported nupkg files, e.g.:
    #   /opt/nuget-local/ClosedXML.0.105.0.nupkg
    if not cache_root.exists() or not cache_root.is_dir():
        return None

    pkg_lower = package_name.lower()
    versions: list[str] = []
    for child in cache_root.iterdir():
        if not child.is_file():
            continue
        name_lower = child.name.lower()
        if not (name_lower.endswith(".nupkg") and name_lower.startswith(pkg_lower + ".")):
            continue

        # Strip: "<PackageId>." prefix and ".nupkg" suffix
        ver = child.name[len(package_name) + 1 : -len(".nupkg")]
        # Fallback if casing differs (we matched lower-case)
        if not ver:
            ver = child.name.split(".", 1)[1][:-len(".nupkg")]
        versions.append(ver)

    if not versions:
        return None

    return max(versions, key=version_key)


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

    cache_root = Path("/opt/nuget-local")
    precache = os.environ.get("PRECACHE_PACKAGES") or os.environ.get("NUGET_PRECACHE") or ""
    top_level = [p for p in re.split(r"\s+", precache.strip()) if p]

    bullet_lines: list[str] = []
    for pkg in top_level:
        ver = find_cached_nuget_version(pkg, cache_root) or "(not found)"
        bullet_lines.append(f"  * {pkg} {ver}")

    packages_block = "\n".join(bullet_lines) if bullet_lines else "  * (none)"

    rendered = template
    rendered = rendered.replace("{dotnetVersion}", dn)
    rendered = rendered.replace("{pythonVersion}", py)
    rendered = rendered.replace("{ffmpegVersion}", ff)
    rendered = rendered.replace("{gccVersion}", gcc)
    rendered = rendered.replace("{nodeVersion}", node)
    rendered = rendered.replace("  {all packages in this format: `* PackageName Version`}", packages_block)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(rendered.rstrip() + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

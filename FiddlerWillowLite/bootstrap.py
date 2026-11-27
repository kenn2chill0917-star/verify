#!/usr/bin/env python3
"""Prepare the local build toolchain for FiddlerWillowLite.

Run this once after cloning. It downloads the Roslyn compiler from NuGet and
copies the .NET Framework reference assemblies already present on Windows.
No Visual Studio installation is required.

The bundled toolchain is intentionally NOT committed to GitHub:
  - Roslyn binaries are large and re-downloadable.
  - Fiddler.exe is a Telerik binary that must not be redistributed.

build.py automatically uses the installed Fiddler Classic as its reference,
so Fiddler Classic must be installed before building.
"""

import argparse
import os
import shutil
import sys
import urllib.request
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
TOOLCHAIN = os.path.join(ROOT, ".toolchain")
ROSLYN = os.path.join(TOOLCHAIN, "roslyn")
NETFX = os.path.join(TOOLCHAIN, "netfx")

ROSLYN_VERSION = "4.11.0"
ROSLYN_URL = (
    "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/"
    + ROSLYN_VERSION
    + "/microsoft.net.compilers.toolset."
    + ROSLYN_VERSION
    + ".nupkg"
)

FRAMEWORK_REFS = [
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Runtime.Serialization.dll",
    "System.Xml.dll",
]


def framework_source():
    windir = os.environ.get("WINDIR", r"C:\Windows")
    candidates = [
        os.path.join(windir, "Microsoft.NET", "Framework", "v4.0.30319"),
        os.path.join(windir, "Microsoft.NET", "Framework64", "v4.0.30319"),
    ]
    for path in candidates:
        if os.path.isfile(os.path.join(path, "mscorlib.dll")):
            return path
    return None


def download(url, target):
    print("Downloading Roslyn " + ROSLYN_VERSION + " from NuGet ...")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with urllib.request.urlopen(url, timeout=120) as response, open(target, "wb") as out:
        shutil.copyfileobj(response, out)
    print("Downloaded: " + target)


def install_roslyn(package, force):
    marker = os.path.join(ROSLYN, "csc.exe")
    if os.path.isfile(marker) and not force:
        print("Roslyn already present: " + marker)
        return True
    if not os.path.isfile(package) or force:
        download(ROSLYN_URL, package)
    os.makedirs(ROSLYN, exist_ok=True)
    print("Extracting Roslyn compiler ...")
    with zipfile.ZipFile(package) as archive:
        prefix = "tasks/net472/"
        members = [n for n in archive.namelist() if n.startswith(prefix) and not n.endswith("/")]
        if not members:
            print("ERROR: Roslyn package has no tasks/net472/ payload")
            return False
        for name in members:
            rel = name[len(prefix):]
            if not rel:
                continue
            dest = os.path.join(ROSLYN, *rel.split("/"))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with archive.open(name) as src, open(dest, "wb") as out:
                shutil.copyfileobj(src, out)
    if not os.path.isfile(marker):
        print("ERROR: csc.exe missing after extraction")
        return False
    print("Roslyn ready: " + marker)
    return True


def install_framework_refs(force):
    source = framework_source()
    if not source:
        print("ERROR: .NET Framework 4.x reference assemblies not found")
        print("Install the .NET Framework 4.7.2 Developer Pack, then retry.")
        return False
    os.makedirs(NETFX, exist_ok=True)
    copied = 0
    for name in FRAMEWORK_REFS:
        src = os.path.join(source, name)
        dst = os.path.join(NETFX, name)
        if not os.path.isfile(src):
            print("ERROR: missing framework assembly: " + src)
            return False
        if force or not os.path.isfile(dst):
            shutil.copy2(src, dst)
            copied += 1
    print("Framework refs ready: " + source + " (" + str(copied) + " copied)")
    return True


def main():
    parser = argparse.ArgumentParser(description="Bootstrap the FiddlerWillowLite build toolchain.")
    parser.add_argument("--force", action="store_true", help="re-download/re-copy even if present")
    args = parser.parse_args()

    if os.name != "nt":
        print("ERROR: Fiddler Classic and this toolchain are Windows-only.")
        return 1

    package = os.path.join(TOOLCHAIN, "roslyn.nupkg")
    ok = install_roslyn(package, args.force) and install_framework_refs(args.force)
    if not ok:
        return 1

    print("\nToolchain ready. Next steps:")
    print("  python build.py --test     # build, verify, run tests, install")
    print("  Restart Fiddler Classic and allow the unsigned extension once.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

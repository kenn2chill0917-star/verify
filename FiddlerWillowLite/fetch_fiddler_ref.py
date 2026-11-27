#!/usr/bin/env python3
"""Fetch a Fiddler.exe reference for CI builds.

Fiddler Classic is not installed on GitHub Actions runners, and Telerik's
Fiddler.exe cannot be redistributed in this repository. This script downloads
the official installer from Telerik's public S3 bucket and extracts the
Fiddler.exe assembly from its two nested NSIS payloads into
``.toolchain/refs/Fiddler.exe`` so that ``build.py`` can compile against it.

Usage:
    python fetch_fiddler_ref.py                 # download + extract
    python fetch_fiddler_ref.py --installer X   # use a local installer copy
    python fetch_fiddler_ref.py --force         # overwrite an existing ref
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request

ROOT = os.path.dirname(os.path.abspath(__file__))
DEFAULT_INSTALLER = os.path.join(ROOT, ".toolchain", "FiddlerSetup.exe")
OUTPUT = os.path.join(ROOT, ".toolchain", "refs", "Fiddler.exe")
FIDDLER_URL = "https://telerik-fiddler.s3.amazonaws.com/fiddler/FiddlerSetup.exe"


def die(message):
    print("ERROR: " + message)
    sys.exit(1)


def find_7z():
    candidates = [
        shutil.which("7z"),
        shutil.which("7za"),
        os.path.join(os.environ.get("ProgramFiles", r"C:\Program Files"), "7-Zip", "7z.exe"),
        os.path.join(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"), "7-Zip", "7z.exe"),
    ]
    for path in candidates:
        if path and os.path.isfile(path):
            return path
    die("7-Zip not found. Install 7-Zip or provide Fiddler.exe via --fiddler-ref in build.py.")


def download(url, target):
    print("Downloading Fiddler Classic installer ...")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with urllib.request.urlopen(url, timeout=180) as response, open(target, "wb") as out:
        shutil.copyfileobj(response, out)
    print("Downloaded: " + target)


def extract(seven_zip, archive, output_dir):
    proc = subprocess.run(
        [seven_zip, "x", archive, "-o" + output_dir, "-y"],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    )
    if proc.returncode != 0:
        print(proc.stdout.decode("utf-8", "replace"))
        die("failed to extract: " + archive)


def find_file(root, name):
    for directory, _, files in os.walk(root):
        if name in files:
            return os.path.join(directory, name)
    return None


def main():
    parser = argparse.ArgumentParser(description="Fetch Fiddler.exe build reference for CI.")
    parser.add_argument("--installer", help="path to a local FiddlerSetup.exe")
    parser.add_argument("--force", action="store_true", help="overwrite an existing reference")
    args = parser.parse_args()

    if os.path.isfile(OUTPUT) and not args.force:
        print("Fiddler reference already present: " + OUTPUT)
        return 0

    installer = args.installer or DEFAULT_INSTALLER
    if not os.path.isfile(installer):
        download(FIDDLER_URL, installer)

    seven_zip = find_7z()
    with tempfile.TemporaryDirectory(prefix="willow-fiddler-ref-") as temp:
        payload = os.path.join(temp, "payload")
        extracted = os.path.join(temp, "extracted")
        print("Extracting outer installer ...")
        extract(seven_zip, installer, payload)
        inner = find_file(payload, "FiddlerSetup.exe")
        if not inner:
            die("inner FiddlerSetup.exe not found in installer payload")
        print("Extracting Fiddler.exe ...")
        extract(seven_zip, inner, extracted)
        fiddler = find_file(extracted, "Fiddler.exe")
        if not fiddler:
            die("Fiddler.exe not found in inner payload")

        os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
        shutil.copy2(fiddler, OUTPUT)

    print("Fiddler reference ready: " + OUTPUT + " (" + str(os.path.getsize(OUTPUT)) + " bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

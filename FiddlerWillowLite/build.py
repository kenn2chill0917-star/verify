#!/usr/bin/env python3
"""One-command build for FiddlerWillowLite.

Uses the toolchain in .toolchain/ (Roslyn compiler + Fiddler reference
assembly + .NET Framework reference assemblies). No Visual Studio required.
Run `python bootstrap.py` once on a fresh clone to prepare the toolchain.

Usage:
    python build.py                 # build + verify + install into Fiddler Scripts
    python build.py --test          # additionally run 7 behavior tests
    python build.py --no-install    # build only, do not copy to Fiddler
    python build.py -c Release      # release build
    python build.py --fiddler-ref X # use a custom Fiddler.exe as reference
    python build.py --install-dir X # custom Fiddler Scripts directory
"""

import argparse
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
TOOLCHAIN = os.path.join(ROOT, ".toolchain")
TESTS = os.path.join(ROOT, "tests")
NETFX = os.path.join(TOOLCHAIN, "netfx")
ROSLYN = os.path.join(TOOLCHAIN, "roslyn")

SOURCE_FILES = [
    "Models.cs",
    "RuleStore.cs",
    "EditDialog.cs",
    "HostRuleParser.cs",
    "BulkHostDialog.cs",
    "BulkDelete.cs",
    "BulkDeleteDialog.cs",
    "MultiSelectTree.cs",
    "WillowView.cs",
    "WillowPlugin.cs",
    os.path.join("Properties", "AssemblyInfo.cs"),
]

FRAMEWORK_REFS = [
    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Drawing.dll",
    "System.Windows.Forms.dll", "System.Runtime.Serialization.dll", "System.Xml.dll",
]

FIDDLER_INSTALL_CANDIDATES = [
    os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "Fiddler", "Fiddler.exe"),
    os.path.join(os.environ.get("ProgramFiles", ""), "Fiddler", "Fiddler.exe"),
    os.path.join(os.environ.get("ProgramFiles(x86)", ""), "Fiddler", "Fiddler.exe"),
    os.path.join(os.environ.get("USERPROFILE", ""), "Documents", "Fiddler2", "Fiddler.exe"),
]

SCRIPT_DIR_CANDIDATES = [
    os.path.join(os.environ.get("USERPROFILE", ""), "Documents", "Fiddler2", "Scripts"),
    os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "Fiddler", "Scripts"),
]


def die(message):
    print("ERROR: " + message)
    sys.exit(1)


def compiler_path():
    path = os.path.join(ROSLYN, "cs" + "c.exe")
    if not os.path.isfile(path):
        die("compiler missing: " + path)
    return path


def fiddler_reference(explicit):
    if explicit:
        if not os.path.isfile(explicit):
            die("Fiddler reference not found: " + explicit)
        return explicit, "explicit"
    for candidate in FIDDLER_INSTALL_CANDIDATES:
        if candidate and os.path.isfile(candidate):
            return candidate, "installed Fiddler"
    bundled = os.path.join(TOOLCHAIN, "refs", "Fiddler.exe")
    if os.path.isfile(bundled):
        return bundled, "bundled reference"
    die("no Fiddler.exe reference available")
    return None, None


def framework_refs():
    refs = []
    for name in FRAMEWORK_REFS:
        path = os.path.join(NETFX, name)
        if not os.path.isfile(path):
            die("framework reference missing: " + path)
        refs.append(path)
    return refs


def run_compiler(compiler, output, sources, refs, target="library"):
    args = [compiler, "/nologo", "/target:" + target, "/langversion:7.3", "/warn:4",
            "/out:" + output, "/nostdlib+"]
    args += ["/r:" + ref for ref in refs]
    args += sources
    proc = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = proc.stdout.decode("utf-8", "replace")
    if text.strip():
        print(text.rstrip())
    return proc.returncode == 0


def build(configuration, fiddler_ref):
    output = os.path.join(ROOT, "bin", configuration, "FiddlerWillowLite.dll")
    os.makedirs(os.path.dirname(output), exist_ok=True)
    sources = [os.path.join(ROOT, s) for s in SOURCE_FILES]
    refs = framework_refs() + [fiddler_ref]
    print("Compiler : " + compiler_path())
    print("Reference: " + fiddler_ref)
    print("Output   : " + output)
    if not run_compiler(compiler_path(), output, sources, refs):
        die("compilation failed")
    print("Build succeeded.")
    return output


def verify(dll_path, fiddler_ref):
    source = os.path.join(TESTS, "VerifyPlugin.cs")
    if not os.path.isfile(source):
        return True
    exe = os.path.join(ROOT, "bin", "VerifyPlugin.exe")
    refs = [os.path.join(NETFX, n) for n in ["mscorlib.dll", "System.dll", "System.Core.dll"]]
    if not run_compiler(compiler_path(), exe, [source], refs, target="exe"):
        die("verification build failed")
    proc = subprocess.run([exe, fiddler_ref, dll_path], stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = proc.stdout.decode("utf-8", "replace")
    print(text.rstrip())
    if proc.returncode != 0:
        die("plugin verification failed")
    return True


def run_tests():
    print("\nRunning behavior tests (shim environment)...")
    shim = os.path.join(TESTS, "FiddlerShim.cs")
    behavior = os.path.join(TESTS, "BehaviorTest.cs")
    plugin_dll = os.path.join(ROOT, "bin", "shim-check.dll")
    refs = [os.path.join(NETFX, n) for n in FRAMEWORK_REFS]
    if not run_compiler(compiler_path(), plugin_dll,
                        [shim] + [os.path.join(ROOT, s) for s in SOURCE_FILES], refs):
        die("shim build failed")
    test_exe = os.path.join(ROOT, "bin", "BehaviorTest.exe")
    if not run_compiler(compiler_path(), test_exe, [behavior], refs + [plugin_dll], target="exe"):
        die("test build failed")
    proc = subprocess.run([test_exe], stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = proc.stdout.decode("utf-8", "replace")
    print(text.rstrip())
    if proc.returncode != 0:
        die("behavior tests failed")
    return True


def install(dll_path, install_dir=None):
    if install_dir:
        target_dir = install_dir
    else:
        target_dir = next((p for p in SCRIPT_DIR_CANDIDATES if p and os.path.isdir(p)),
                          SCRIPT_DIR_CANDIDATES[0])
    os.makedirs(target_dir, exist_ok=True)
    target = os.path.join(target_dir, os.path.basename(dll_path))
    shutil.copy2(dll_path, target)
    print("Installed: " + target)
    return target


def main():
    parser = argparse.ArgumentParser(description="Build FiddlerWillowLite with the bundled toolchain.")
    parser.add_argument("-c", "--configuration", default="Debug", choices=["Debug", "Release"])
    parser.add_argument("--test", action="store_true", help="run behavior tests after build")
    parser.add_argument("--no-install", action="store_true", help="do not copy DLL into Fiddler Scripts")
    parser.add_argument("--fiddler-ref", help="path to a specific Fiddler.exe used as reference")
    parser.add_argument("--install-dir", help="custom Fiddler Scripts directory")
    args = parser.parse_args()

    fiddler_ref, source = fiddler_reference(args.fiddler_ref)
    print("Toolchain: " + TOOLCHAIN)
    print("Fiddler ref source: " + source)
    dll = build(args.configuration, fiddler_ref)
    verify(dll, fiddler_ref)
    if args.test:
        run_tests()
    if not args.no_install:
        install(dll, args.install_dir)
    print("\nDone. Restart Fiddler to load the plugin.")


if __name__ == "__main__":
    main()

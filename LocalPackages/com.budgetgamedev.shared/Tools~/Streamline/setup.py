"""Prepare the shared Windows Streamline plugin and a reproducible HDRP final-pass hook."""

import argparse
import hashlib
import json
import os
import shutil
import subprocess

# Import also works when setup.py is loaded by release/setup tests.
import sys
import urllib.request
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from urp_hooks import patch_urp

PACKAGE = Path(__file__).resolve().parents[2]
NATIVE = PACKAGE / "Native~" / "Streamline"
HDRP_FILE = Path("Runtime/RenderPipeline/HDRenderPipeline.PostProcess.cs")
HDRP_HASH = "06443f65ac7f9a936917332d38ed4eb449e16ac718d516e9784f83fc23763a27"
DRAW = (
    "                        HDUtils.DrawFullScreen(natCmd, backBufferRect, "
    "finalPassMaterial, data.destination, cubemapFace: data.cubemapFace);"
)
HOOK = (
    "                        SharedFrameGenerationHooks.Capture(data.hdCamera, natCmd, "
    "finalPassMaterial,\n                            data.uiBuffer, backBufferRect, "
    "data.postProcessIsFinalPass);\n"
)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def patch_hdrp(project, source=None):
    target = project / "Packages/com.unity.render-pipelines.high-definition"
    if target.exists():
        source = target
    elif source is None:
        candidates = list(
            (project / "Library/PackageCache").glob("com.unity.render-pipelines.high-definition@*")
        )
        candidates = [
            p
            for p in candidates
            if json.loads((p / "package.json").read_text())["version"] == "17.5.0"
        ]
        if len(candidates) != 1:
            raise RuntimeError("Resolve HDRP 17.5.0 in Unity first, or pass --hdrp-source.")
        source = candidates[0]
    text = (source / HDRP_FILE).read_text(encoding="utf-8")
    if HOOK in text:
        text = text.replace(HOOK, "")
    text = text.replace(HOOK.replace("data.postProcessIsFinalPass", "outputsToHDRBuffer"), "")
    if hashlib.sha256(text.encode()).hexdigest() != HDRP_HASH:
        raise RuntimeError(
            "HDRP final pass differs from the reviewed 17.5.0 source; refusing to patch it."
        )
    if source != target:
        shutil.copytree(source, target)
    (target / HDRP_FILE).write_text(text.replace(DRAW, HOOK + DRAW), encoding="utf-8", newline="\n")
    helper = target / "Runtime/RenderPipeline/SharedFrameGenerationHooks.cs"
    helper.write_text(
        (NATIVE / "SharedFrameGenerationHooks.cs.txt").read_text(encoding="utf-8"),
        encoding="utf-8",
        newline="\n",
    )
    linker = target / "Runtime/RenderPipeline/Streamline/link.xml"
    linker.parent.mkdir(exist_ok=True)
    linker.write_text((NATIVE / "link.xml.txt").read_text())
    print("Prepared HDRP 17.5.0 final-color hook:", target)


def sdk_directory(directory, artifacts, manifest):
    if directory is None:
        directory = artifacts / "sdk"
        if not (directory / "include/sl.h").exists():
            archive = artifacts / "streamline-sdk.zip"
            if not archive.exists() or sha256(archive) != manifest["archiveSha256"]:
                version = manifest["sdkVersion"]
                url = f"https://github.com/NVIDIA-RTX/Streamline/releases/download/v{version}/streamline-sdk-v{version}.zip"
                print("Downloading pinned NVIDIA SDK:", url, flush=True)
                urllib.request.urlretrieve(url, archive)  # noqa: S310 - fixed HTTPS NVIDIA release URL
            if sha256(archive) != manifest["archiveSha256"]:
                raise RuntimeError("NVIDIA SDK archive checksum mismatch")
            with zipfile.ZipFile(archive) as zipped:
                zipped.extractall(directory)
    for entry in manifest["files"]:
        path = directory / "bin/x64" / entry["name"]
        if not path.exists() or sha256(path) != entry["sha256"]:
            raise RuntimeError(f"Not the pinned production NVIDIA binary: {path}")
    version_header = (directory / "include/sl_version.h").read_text()
    if "#define SL_VERSION_MINOR 12" not in version_header:
        raise RuntimeError("Streamline 2.12 headers required")
    return directory


def verify_signatures(directory, manifest):
    if os.name != "nt":
        print(
            "Authenticode trust validation deferred to Windows; production SHA-256 hashes verified."
        )
        return
    for entry in manifest["files"]:
        if not entry["name"].endswith(".dll"):
            continue
        path = str(directory / entry["name"]).replace("'", "''")
        command = (
            f"$s = Get-AuthenticodeSignature -LiteralPath '{path}'; "
            "if ($s.Status -ne 'Valid' -or "
            "$s.SignerCertificate.Subject -notmatch 'NVIDIA') { exit 1 }"
        )
        subprocess.run(
            [
                shutil.which("pwsh") or "powershell",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                command,
            ],
            check=True,
        )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PACKAGE.parents[1])
    parser.add_argument("--unity-plugin-api", type=Path)
    parser.add_argument("--sdk", type=Path)
    parser.add_argument("--hdrp-source", type=Path)
    parser.add_argument("--urp-source", type=Path)
    parser.add_argument("--pipeline", choices=["urp", "hdrp", "both"], default="both")
    parser.add_argument("--hooks-only", action="store_true")
    parser.add_argument("--hdrp-only", action="store_true")
    args = parser.parse_args()
    if args.pipeline in ("hdrp", "both") or args.hdrp_only:
        patch_hdrp(args.project.resolve(), args.hdrp_source)
    if args.pipeline in ("urp", "both") and not args.hdrp_only:
        patch_urp(args.project.resolve(), args.urp_source)
    if args.hdrp_only or args.hooks_only:
        return
    if args.unity_plugin_api is None:
        raise RuntimeError(
            "Pass --unity-plugin-api pointing to Unity 6000.5.10f1's PluginAPI directory."
        )
    artifacts = NATIVE / "artifacts"
    artifacts.mkdir(exist_ok=True)
    manifest = json.loads((NATIVE / "production.json").read_text())
    sdk = sdk_directory(args.sdk, artifacts, manifest)
    build = artifacts / "build"
    output = artifacts / "win-x64"
    command = [
        "cmake",
        "-S",
        str(NATIVE),
        "-B",
        str(build),
        f"-DSTREAMLINE_SDK={sdk.resolve()}",
        f"-DUNITY_PLUGIN_API={args.unity_plugin_api.resolve()}",
    ]
    if os.name != "nt":
        if not shutil.which("x86_64-w64-mingw32-g++"):
            raise RuntimeError(
                "Use mingw-w64 to cross-compile, or run setup on Windows with Visual Studio C++."
            )
        command += [
            "-DCMAKE_SYSTEM_NAME=Windows",
            "-DCMAKE_CXX_COMPILER=x86_64-w64-mingw32-g++",
            "-DCMAKE_BUILD_TYPE=Release",
        ]
    else:
        command += ["-A", "x64"]
    subprocess.run(command, check=True)
    subprocess.run(
        ["cmake", "--build", str(build), "--config", "Release", "--parallel"], check=True
    )
    subprocess.run(
        ["cmake", "--install", str(build), "--config", "Release", "--prefix", str(output)],
        check=True,
    )
    verify_signatures(output, manifest)
    plugin = PACKAGE / "Runtime/Plugins/Streamline/GfxPluginBudgetGameDevStreamline.dll"
    plugin.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(output / plugin.name, plugin)
    print("Prepared shared Streamline plugin. Refresh Unity before building.")


if __name__ == "__main__":
    main()

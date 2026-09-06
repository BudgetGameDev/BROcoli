#!/usr/bin/env python3
"""Publish the isolated Windows sensor reader and its redistribution notices."""
import json
from pathlib import Path
import shutil
import subprocess
from urllib.request import urlopen
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
project = root / "LocalPackages/com.budgetgamedev.shared/Native~/HardwareSensors"
payload = project / "artifacts/win-x64"
subprocess.run(["dotnet", "publish", "HardwareSensors.csproj", "-c", "Release", "-o", str(payload),
                "-p:RestoreLockedMode=true"], cwd=project, check=True)
assets = json.loads((project / "obj/project.assets.json").read_text())
cache = Path(next(iter(assets["packageFolders"])))
license_dir = payload / "Licenses"
license_dir.mkdir(exist_ok=True)
notices = ["HardwareSensors: read-only sensor discovery using unmodified third-party libraries.",
           "Full package metadata, copyright and license texts are in Licenses/.",
           "MPL-covered library source is available from the repository URLs/commits below.",
           "No sensor driver is bundled or installed. The .NET runtime is self-contained.\n"]
for identity, library in sorted(assets["libraries"].items()):
    if library["type"] != "package":
        continue
    directory = cache / library["path"]
    nuspec = next(directory.glob("*.nuspec"))
    metadata = ET.parse(nuspec).getroot().find("{*}metadata")
    def value(name):
        node = metadata.find("{*}" + name)
        return node.text if node is not None else ""
    package_label = identity.replace("/", "-")
    shutil.copy2(nuspec, license_dir / (package_label + ".nuspec"))
    repository = metadata.find("{*}repository")
    source = "" if repository is None else repository.get("url", "") + " commit " + repository.get("commit", "")
    notices += [f"{identity}\n{value('copyright')}\nAuthors: {value('authors')}\n{value('projectUrl')}\n{source}"]
    license_node = metadata.find("{*}license")
    if license_node is not None and license_node.get("type") == "file":
        shutil.copy2(directory / license_node.text, license_dir / (package_label + "-LICENSE.txt"))
    elif license_node is not None:
        expression = license_node.text
        target = license_dir / (expression + ".txt")
        if not target.exists():
            with urlopen("https://raw.githubusercontent.com/spdx/license-list-data/main/text/" + expression + ".txt", timeout=30) as response:
                target.write_bytes(response.read())
        notices.append("License: " + expression)
    else:
        license_url = value("licenseUrl")
        if identity.startswith("Mono.Posix.NETStandard/"):
            license_url = "https://raw.githubusercontent.com/mono/mono/main/LICENSE"
        if license_url:
            target = license_dir / (package_label + "-LICENSE.txt")
            if not target.exists() or target.read_bytes().lstrip().startswith(b"<!DOCTYPE"):
                with urlopen(license_url, timeout=30) as response:
                    target.write_bytes(response.read())
    for path in directory.glob("*"):
        if path.is_file() and ("license" in path.name.lower() or "notice" in path.name.lower()):
            shutil.copy2(path, license_dir / (package_label + "-" + path.name))
runtime = json.loads((payload / "HardwareSensors.runtimeconfig.json").read_text())["runtimeOptions"]["includedFrameworks"][0]["version"]
runtime_package = cache / "microsoft.netcore.app.runtime.win-x64" / runtime
for path in runtime_package.glob("*"):
    if path.is_file() and ("license" in path.name.lower() or "notice" in path.name.lower()):
        shutil.copy2(path, license_dir / ("dotnet-" + path.name))
notices.append(f".NET Runtime {runtime}: https://github.com/dotnet/runtime/tree/v{runtime}; MIT and third-party notices in Licenses/.")
(payload / "THIRD-PARTY-NOTICES.txt").write_text("\n\n".join(notices) + "\n", encoding="utf-8")
shutil.copy2(project / "README.md", payload / "README.md")
print(f"Sensor payload ready: {payload}")

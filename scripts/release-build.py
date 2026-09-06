#!/usr/bin/env python3
"""Stage one product in a fresh project without mutating its source Editor."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
from contextlib import ExitStack
from pathlib import Path

try:
    from release_streamline import stage_streamline
    from release_workspace import (
        preserve_shader_reports,
        reset_shader_reports,
        reuse_workspace,
        stage_inputs,
    )
except ModuleNotFoundError:
    from scripts.release_streamline import stage_streamline
    from scripts.release_workspace import (
        preserve_shader_reports,
        reset_shader_reports,
        reuse_workspace,
        stage_inputs,
    )

GAME_PREFIX = "com.budgetgamedev.game."
AUTOPLAY_PREFIX = "com.budgetgamedev.autoplay"
HUB = "com.budgetgamedev.hub"


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def create_plan(source: Path, product: str, development: bool = False) -> tuple[dict, dict, dict]:
    manifest = read_json(source / "Packages/manifest.json")
    local = {}
    for directory in (source / "LocalPackages").iterdir():
        if (directory / "package.json").is_file():
            package = read_json(directory / "package.json")
            local[package["name"]] = (directory, package)
    installed_games = sorted(
        name for name in manifest["dependencies"] if name.startswith(GAME_PREFIX)
    )
    if any(name not in local for name in installed_games):
        raise ValueError("Game packages must exist under LocalPackages before isolated staging")
    selected_games = installed_games if product == "launcher" else [GAME_PREFIX + product]
    if not selected_games or any(name not in installed_games for name in selected_games):
        raise ValueError(
            f"Unknown product {product!r}; choose launcher or one of "
            + ", ".join(name.removeprefix(GAME_PREFIX) for name in installed_games)
        )
    excluded = {
        name
        for name in local
        if (name.startswith(GAME_PREFIX) and name not in selected_games)
        or (name == HUB and product != "launcher")
        or (name.startswith(AUTOPLAY_PREFIX) and not development)
        or (
            name.startswith(AUTOPLAY_PREFIX + ".")
            and product != "launcher"
            and name != AUTOPLAY_PREFIX + "." + product
        )
    }
    # Start with direct shared dependencies and the selected product; follow all
    # local transitive edges and reject a dependency on forbidden game/tool code.
    wanted = {name for name in manifest["dependencies"] if name in local and name not in excluded}
    pending = list(wanted)
    while pending:
        name = pending.pop()
        for dependency in local[name][1].get("dependencies", {}):
            if dependency in excluded:
                raise ValueError(
                    f"{name} depends on excluded package {dependency}; decouple it before releasing"
                )
            if dependency in local and dependency not in wanted:
                wanted.add(dependency)
                pending.append(dependency)
    excluded_assemblies = sorted(
        {read_json(path)["name"] for name in excluded for path in local[name][0].rglob("*.asmdef")}
    )
    product_name = (
        "GameLauncher"
        if product == "launcher"
        else ("BROcoli" if product == "brocoli" else product)
    )
    plan = {
        "product": product,
        "productName": product_name,
        "gamePackages": selected_games,
        "localPackages": sorted(wanted),
        "excludedAssemblies": excluded_assemblies,
        "development": development,
    }
    dependencies = {
        name: value
        for name, value in manifest["dependencies"].items()
        if name not in local
        and name not in excluded
        and name
        not in {
            "com.unity.pipeline",
            "com.unity.test-framework",
            "com.unity.testtools.codecoverage",
            "com.unity.ide.visualstudio",
        }
    }
    for name in sorted(wanted):
        dependencies[name] = f"file:../LocalPackages/{name}"
    manifest["dependencies"] = dependencies
    manifest.pop("testables", None)
    return plan, manifest, local


def stage_project(
    source: Path, product: str, development: bool = False, stage_root: Path | None = None
) -> Path:
    return stage_inputs(source, product, create_plan(source, product, development), stage_root)


def audit_player(stage: Path, output: Path) -> dict:
    plan = read_json(stage / "BuildContent.json")
    excluded = set(plan["excludedAssemblies"])
    assemblies = sorted(path for path in output.rglob("*.dll") if path.is_file())
    if (
        not assemblies
        and not list(output.rglob("global-metadata.dat"))
        and not any(".wasm" in path.name for path in output.rglob("*") if path.is_file())
    ):
        raise ValueError(f"No built player code found to audit: {output}")
    for path in assemblies:
        if path.stem in excluded or (not plan["development"] and "autoplay" in path.stem.lower()):
            raise ValueError(f"Forbidden assembly shipped: {path}")
    # Mono players expose type metadata as UTF-8 strings. Inspect game/shared
    # assemblies as well: moving a driver into the game assembly must not evade
    # the package/assembly allowlist. IL2CPP uses global-metadata.dat instead.
    forbidden_types = tuple(
        b"\0" + name.encode() + b"\0"
        for name in (
            "AutoplayController",
            "BotDriver",
            "RuntimeTuning",
            "RunTelemetry",
            "LevelUpAutoResolver",
            "GameplayDiagnostics",
            "NavigationUpdatePending",
            "get_NavigationUpdatePending",
            "PreviewNavigationDelta",
            "SuppressFocusLossPause",
            "get_SuppressFocusLossPause",
            "set_SuppressFocusLossPause",
            "AutoplaySessionDirector",
        )
    )
    if not plan["development"]:
        metadata = [path for path in assemblies if path.name.startswith("BudgetGameDev.")]
        metadata += list(output.rglob("global-metadata.dat"))
        for path in metadata:
            data = path.read_bytes()
            if any(token in data for token in forbidden_types):
                raise ValueError(f"Autoplay type metadata shipped in {path}")
    audit = {
        **plan,
        "playerAssemblies": [str(path.relative_to(output)) for path in assemblies],
        "excludedCodeAudit": "passed",
        "isolation": "packages absent before Unity import",
    }
    write_json(output / "release-audit.json", audit)
    return audit


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--product", required=True, help="game package suffix (brocoli), or launcher"
    )
    parser.add_argument("--source", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--targets", default="windows", help="windows,macos,linux or webgl")
    parser.add_argument("--pipeline", choices=("urp", "hdrp"), default="urp")
    parser.add_argument("--development", action="store_true")
    parser.add_argument("--stage-only", action="store_true")
    parser.add_argument("--stage-root", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument(
        "--reuse-stage",
        action="store_true",
        help="Reuse an isolated workspace and its Unity caches",
    )
    args = parser.parse_args()
    with ExitStack() as contexts:
        return build(args, contexts, parser)


def build(args, contexts, parser):
    targets = list(dict.fromkeys(args.targets.split(",")))
    if not targets or any(
        target not in {"windows", "macos", "linux", "webgl"} for target in targets
    ):
        parser.error("--targets must select windows,macos,linux or webgl")
    if "webgl" in targets and len(targets) != 1:
        parser.error("build webgl separately from native targets")
    stage = stage_project(args.source, args.product, args.development, args.stage_root)
    stage_streamline(args.source.resolve(), stage, args.pipeline, targets)
    if args.reuse_stage:
        stage = contexts.enter_context(
            reuse_workspace(
                stage, args.source, args.product, args.pipeline, targets, args.development
            )
        )
    print(f"Isolated {args.product} project: {stage}", flush=True)
    if args.stage_only:
        return 0
    output = (args.output or args.source / "build/releases" / args.product).resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError(f"Output must be empty to prevent shipping stale binaries: {output}")
    output.mkdir(parents=True, exist_ok=True)
    version = next(
        line.split(": ", 1)[1]
        for line in (stage / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
        if line.startswith("m_EditorVersion: ")
    )
    target_names = {
        "windows": "StandaloneWindows64",
        "macos": "StandaloneOSX",
        "linux": "StandaloneLinux64",
        "webgl": "WebGL",
    }
    command = [
        "unity",
        "run",
        str(stage),
        "--editor-version",
        version,
        "--timeout",
        "7200",
        "--non-interactive",
        "--no-banner",
        "--",
        "-buildTarget",
        target_names[targets[0]],
        "-executeMethod",
        "WebGLBuildScript.Build" if targets == ["webgl"] else "NativePlayerBuildScript.BuildAll",
        "-buildOutput",
        str(output),
        "-buildTargets",
        ",".join(targets),
        "-renderPipeline",
        args.pipeline,
        "-logFile",
        str(output / "unity-build.log"),
    ]
    if args.development:
        command.append("-development")
    environment = os.environ.copy()
    # Pass the existing licensed-asset key only through the child environment;
    # never copy .env to staging or put a secret in command arguments/logs.
    environment_variable = "BROCOLI_LICENSED_ASSET_KEY"
    env_file = args.source / ".env"
    if environment_variable not in environment and env_file.is_file():
        for line in env_file.read_text(encoding="utf-8-sig").splitlines():
            key, separator, value = line.strip().partition("=")
            if separator and key.strip() == environment_variable:
                environment[environment_variable] = value.strip().strip("\"'")
    reset_shader_reports(stage)
    try:
        subprocess.run(command, check=True, env=environment)
    finally:
        preserve_shader_reports(stage, output)
    expected = {
        "windows": f"windows/{read_json(stage / 'BuildContent.json')['productName']}.exe",
        "macos": f"macos/{read_json(stage / 'BuildContent.json')['productName']}.app",
        "linux": f"linux/{read_json(stage / 'BuildContent.json')['productName']}.x86_64",
        "webgl": "index.html",
    }
    for target in targets:
        if not (output / expected[target]).exists():
            raise ValueError(f"Unity did not produce {expected[target]}")
    audit_player(stage, output)
    print(f"Release content audit passed: {output / 'release-audit.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

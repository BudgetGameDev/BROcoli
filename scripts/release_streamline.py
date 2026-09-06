"""Carry the shared Streamline build into an isolated Windows HDRP release."""

import shutil
from pathlib import Path


def stage_streamline(source: Path, stage: Path, pipeline: str, targets: list[str]) -> None:
    if pipeline != "hdrp" or "windows" not in targets:
        return
    package = Path("LocalPackages/com.budgetgamedev.shared")
    native = package / "Native~/Streamline"
    if not (source / native).is_dir():
        return
    hdrp = Path("Packages/com.unity.render-pipelines.high-definition")
    if not (source / hdrp / "Runtime/RenderPipeline/SharedFrameGenerationHooks.cs").is_file():
        raise ValueError(
            "Run the shared package Tools~/Streamline/setup.py before staging Windows HDRP."
        )
    payload = native / "artifacts/win-x64"
    if not (source / payload / "GfxPluginBudgetGameDevStreamline.dll").is_file():
        raise ValueError("The shared Streamline Windows native build is missing. Run shared setup.")
    shutil.copytree(source / hdrp, stage / hdrp)
    shutil.copytree(source / payload, stage / payload)

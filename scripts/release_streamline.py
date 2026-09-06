"""Carry the shared Streamline build into an isolated Windows URP or HDRP release."""

import shutil
from pathlib import Path


def stage_streamline(source: Path, stage: Path, pipeline: str, targets: list[str]) -> None:
    # Resources are included even when their owning HDRP assembly is filtered out.
    # Hide the shared HDRP-only resources before Unity imports a URP staging project.
    resources = (
        stage / "LocalPackages/com.budgetgamedev.shared/Runtime/Rendering/HighDefinition/Resources"
    )
    if pipeline == "urp" and resources.is_dir():
        resources.rename(resources.with_name("Resources~"))
        resources.with_suffix(".meta").unlink(missing_ok=True)
    if pipeline not in ("urp", "hdrp"):
        return
    settings = source / "ProjectSettings/ProjectSettings.asset"
    framework = settings.is_file() and "ENABLE_UPSCALER_FRAMEWORK" in settings.read_text()
    if "windows" not in targets:
        if framework and any(target in targets for target in ("macos", "linux")):
            urp = Path("Packages/com.unity.render-pipelines.universal")
            if not (source / urp / "Runtime/SharedStreamlineHooks.cs").is_file():
                raise ValueError(
                    "Run shared setup.py --hooks-only before staging the upscaler framework."
                )
            shutil.copytree(source / urp, stage / urp, dirs_exist_ok=True)
        return
    package = Path("LocalPackages/com.budgetgamedev.shared")
    native = package / "Native~/Streamline"
    if not (source / native).is_dir():
        return
    render_package = Path("Packages") / (
        "com.unity.render-pipelines.high-definition"
        if pipeline == "hdrp"
        else "com.unity.render-pipelines.universal"
    )
    hook = (
        "Runtime/RenderPipeline/SharedFrameGenerationHooks.cs"
        if pipeline == "hdrp"
        else "Runtime/SharedStreamlineHooks.cs"
    )
    if not (source / render_package / hook).is_file():
        raise ValueError(
            f"Run shared Tools~/Streamline/setup.py before staging Windows {pipeline.upper()}."
        )
    payload = native / "artifacts/win-x64"
    if not (source / payload / "GfxPluginBudgetGameDevStreamline.dll").is_file():
        raise ValueError("The shared Streamline Windows native build is missing. Run shared setup.")
    urp = Path("Packages/com.unity.render-pipelines.universal")
    if pipeline == "hdrp" and not (source / urp / "Runtime/SharedStreamlineHooks.cs").is_file():
        raise ValueError(
            "Run shared setup.py with --pipeline both; "
            "HDRP builds also need the URP framework fixes."
        )
    shutil.copytree(source / render_package, stage / render_package, dirs_exist_ok=True)
    # The shared package references URP even in HDRP players.
    if pipeline == "hdrp":
        shutil.copytree(source / urp, stage / urp, dirs_exist_ok=True)
    shutil.copytree(source / payload, stage / payload)

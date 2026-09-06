"""Version-pinned URP capture hooks; no dependency from Unity's assembly back to the host."""

# ruff: noqa: E501 -- Exact, hash-pinned Unity source strings.
import hashlib
import json
import shutil
from pathlib import Path

NATIVE = Path(__file__).resolve().parents[2] / "Native~/Streamline"
# Hashes are checked before any file is written. Patched hashes support repeat setup.
FILES = {
    "Runtime/2D/Rendergraph/Renderer2DRendergraph.cs": [],
    "Editor/ShaderBuildPreprocessor.cs": [],
    "Runtime/UniversalRenderer.cs": [],
    "Runtime/UniversalRendererRenderGraph.cs": [],
    "Runtime/Passes/FinalBlitPass.cs": [],
    "Runtime/Passes/PostProcess/FinalPostProcessPass.cs": [],
}


def transform(name, text):
    if name == "Runtime/2D/Rendergraph/Renderer2DRendergraph.cs":
        old = "cameraData.upscalingFilter == ImageUpscalingFilter.FSR"
        text = text.replace(old, "streamlineFsr1Enabled")
        anchor = "            bool applyFinalPostProcessing = cameraData.resolveFinalTarget"
        text = text.replace(
            anchor,
            """#if ENABLE_UPSCALER_FRAMEWORK
            bool streamlineFsr1Enabled = cameraData.resolvedUpscalerHash == UniversalRenderPipeline.k_UpscalerHash_FSR1;
#else
            bool streamlineFsr1Enabled = cameraData.upscalingFilter == ImageUpscalingFilter.FSR;
#endif
"""
            + anchor,
        )
    elif name == "Editor/ShaderBuildPreprocessor.cs":
        old = "urpAsset.upscalingFilter == UpscalingFilterSelection.Point"
        text = text.replace(old, "streamlinePointEnabled")
        anchor = "            if (streamlinePointEnabled)"
        text = text.replace(
            anchor,
            """#if ENABLE_UPSCALER_FRAMEWORK
            bool streamlinePointEnabled = urpAsset.upscalerName == UniversalRenderPipeline.k_UpscalerName_Point;
#else
            bool streamlinePointEnabled = urpAsset.upscalingFilter == UpscalingFilterSelection.Point;
#endif
"""
            + anchor,
        )
    elif name == "Runtime/UniversalRenderer.cs":
        anchor = (
            "            inputSummary.requiresColorTexture |= cameraData.requiresOpaqueTexture;"
        )
        text = text.replace(
            anchor,
            """            if (SharedStreamlineHooks.IsEnabled(cameraData.camera))
            {
                inputSummary.requiresDepthTexture = true;
                inputSummary.requiresColorTexture = true;
                inputSummary.requiresMotionVectors = true;
                inputSummary.requiresDepthTextureEarliestEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }
"""
            + anchor,
        )
    elif name == "Runtime/UniversalRendererRenderGraph.cs":
        anchor = "            RecordCustomRenderGraphPasses(renderGraph, RenderPassEvent.BeforeRenderingPostProcessing);"
        text = text.replace(
            anchor,
            anchor + "\n            SharedStreamlineHooks.CaptureInputs(renderGraph, frameData);",
        )
        anchor = "bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find("
        text = text.replace(
            anchor,
            "bool hasPassesAfterPostProcessing = SharedStreamlineHooks.IsEnabled(cameraData.camera) || activeRenderPassQueue.Find(",
        )
    elif name == "Runtime/Passes/FinalBlitPass.cs":
        text = text.replace(
            "internal bool useFullScreenViewport;",
            "internal bool useFullScreenViewport;\n            internal bool streamlineHudless;",
        )
        text = text.replace(
            "bool useFullScreenViewport = false)\n",
            "bool useFullScreenViewport = false, TextureHandle streamlineTarget = default, bool streamlineHudless = false)\n",
        )
        text = text.replace(
            "var destinationTexture = resourceData.backBufferColor;",
            "var destinationTexture = streamlineTarget.IsValid() ? streamlineTarget : resourceData.backBufferColor;",
        )
        text = text.replace(
            "                passData.sourceID =",
            "                passData.streamlineHudless = streamlineHudless;\n                passData.sourceID =",
        )
        text = text.replace(
            "data.hdrOutputLuminanceParams, data.cameraData.rendersOverlayUI)",
            "data.hdrOutputLuminanceParams, data.cameraData.rendersOverlayUI && !data.streamlineHudless)",
        )
        anchor = (
            "            Render(renderGraph, cameraData, resourceData, resourceData.cameraColor);"
        )
        text = text.replace(
            anchor,
            """            SharedStreamlineHooks.CaptureFinal(renderGraph, frameData,
                target => Render(renderGraph, cameraData, resourceData, resourceData.cameraColor, false, target, true));
"""
            + anchor,
        )
    else:
        text = text.replace(
            "internal bool applyFxaa;",
            "internal bool applyFxaa;\n            internal bool streamlineHudless;",
        )
        anchor = "        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)\n"
        text = text.replace(
            anchor,
            """        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            SharedStreamlineHooks.CaptureFinal(renderGraph, frameData,
                target => RenderStreamlineFinal(renderGraph, frameData, target, true));
            RenderStreamlineFinal(renderGraph, frameData, default, false);
        }

        private void RenderStreamlineFinal(RenderGraph renderGraph, ContextContainer frameData, TextureHandle streamlineTarget, bool streamlineHudless)
""",
        )
        text = text.replace(
            "var destinationTexture = resourceData.backBufferColor;",
            "var destinationTexture = streamlineTarget.IsValid() ? streamlineTarget : resourceData.backBufferColor;",
        )
        text = text.replace(
            "                passData.destinationTexture =",
            "                passData.streamlineHudless = streamlineHudless;\n                passData.destinationTexture =",
        )
        text = text.replace(
            "data.hdrOperations, cameraData.rendersOverlayUI)",
            "data.hdrOperations, cameraData.rendersOverlayUI && !data.streamlineHudless)",
        )
        text = text.replace(
            "            resourceData.SwitchActiveTexturesToBackbuffer();",
            "            if (!streamlineHudless) resourceData.SwitchActiveTexturesToBackbuffer();",
        )
    return text


def patch_urp(project, source=None):
    target = project / "Packages/com.unity.render-pipelines.universal"
    if target.exists():
        source = target
    elif source is None:
        candidates = [
            p
            for p in (project / "Library/PackageCache").glob(
                "com.unity.render-pipelines.universal@*"
            )
            if json.loads((p / "package.json").read_text())["version"] == "17.5.0"
        ]
        if len(candidates) != 1:
            raise RuntimeError("Resolve URP 17.5.0 first, or pass --urp-source.")
        source = candidates[0]
    hashes = json.loads((NATIVE / "urp-hashes.json").read_text())
    outputs = {}
    for name in FILES:
        text = (source / name).read_text()
        digest = hashlib.sha256(text.encode()).hexdigest()
        if digest == hashes[name]["patched"]:
            outputs[name] = text
        elif digest == hashes[name]["original"]:
            outputs[name] = transform(name, text)
            if hashlib.sha256(outputs[name].encode()).hexdigest() != hashes[name]["patched"]:
                raise RuntimeError("URP patch implementation differs from reviewed output.")
        else:
            raise RuntimeError(
                f"URP source differs from reviewed 17.5.0; refusing to patch {name}."
            )
    if source != target:
        shutil.copytree(source, target)
    for name, text in outputs.items():
        (target / name).write_text(text)
    (target / "Runtime/SharedStreamlineHooks.cs").write_text(
        (NATIVE / "SharedUniversalStreamlineHooks.cs.txt").read_text()
    )
    (target / "Runtime/SharedStreamlineHooks.cs.meta").write_text(
        "fileFormatVersion: 2\nguid: e3c87f7d723e4f1e8880e3de4a21cd45\n"
    )
    linker = target / "Runtime/Streamline/link.xml"
    linker.parent.mkdir(exist_ok=True)
    linker.write_text(
        '<linker><assembly fullname="Unity.RenderPipelines.Universal.Runtime"><type fullname="UnityEngine.Rendering.Universal.SharedStreamlineHooks" preserve="all" /></assembly></linker>\n'
    )
    print("Prepared URP 17.5.0 Streamline capture hooks:", target)

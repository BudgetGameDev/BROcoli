"""Keep the URP adapter reproducible and refuse partially matching engine sources."""

# ruff: noqa: E501 -- Exact source fixtures for the pinned engine patch.
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

MODULE = (
    Path(__file__).resolve().parents[2]
    / "LocalPackages/com.budgetgamedev.shared/Tools~/Streamline/urp_hooks.py"
)
SPEC = importlib.util.spec_from_file_location("streamline_urp_hooks", MODULE)
hooks = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(hooks)


class UniversalHookTests(unittest.TestCase):
    def fixture(self, root):
        target = root / "Packages/com.unity.render-pipelines.universal"
        native = root / "native"
        native.mkdir()
        fixtures = {
            "Runtime/UniversalRenderer.cs": "            inputSummary.requiresColorTexture |= cameraData.requiresOpaqueTexture;\n",
            "Runtime/UniversalRendererRenderGraph.cs": (
                "            RecordCustomRenderGraphPasses(renderGraph, RenderPassEvent.BeforeRenderingPostProcessing);\n"
                "bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x);\n"
            ),
            "Runtime/Passes/FinalBlitPass.cs": (
                "            Render(renderGraph, cameraData, resourceData, resourceData.cameraColor);\n"
                "var destinationTexture = resourceData.backBufferColor;\n"
                "data.hdrOutputLuminanceParams, data.cameraData.rendersOverlayUI)\n"
            ),
            "Runtime/Passes/PostProcess/FinalPostProcessPass.cs": (
                "        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)\n"
                "            resourceData.SwitchActiveTexturesToBackbuffer();\n"
                "data.hdrOperations, cameraData.rendersOverlayUI)\n"
            ),
            "Runtime/2D/Rendergraph/Renderer2DRendergraph.cs": (
                "            bool applyFinalPostProcessing = cameraData.resolveFinalTarget\n"
                "cameraData.upscalingFilter == ImageUpscalingFilter.FSR\n"
            ),
            "Editor/ShaderBuildPreprocessor.cs": "            if (urpAsset.upscalingFilter == UpscalingFilterSelection.Point)\n",
        }
        hashes = {}
        for name, text in fixtures.items():
            path = target / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
            patched = hooks.transform(name, text)
            self.assertNotEqual(text, patched, name)
            hashes[name] = {
                "original": hashlib.sha256(text.encode()).hexdigest(),
                "patched": hashlib.sha256(patched.encode()).hexdigest(),
            }
        (native / "urp-hashes.json").write_text(json.dumps(hashes))
        (native / "SharedUniversalStreamlineHooks.cs.txt").write_text("// test hook\n")
        return target, native, fixtures

    def test_repeated_setup_keeps_single_hooks_and_separates_ui_in_both_final_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target, native, _ = self.fixture(root)
            with patch.object(hooks, "NATIVE", native):
                hooks.patch_urp(root)
                first = {name: (target / name).read_text() for name in hooks.FILES}
                hooks.patch_urp(root)
            self.assertEqual(first, {name: (target / name).read_text() for name in hooks.FILES})
            for name in (
                "Runtime/Passes/FinalBlitPass.cs",
                "Runtime/Passes/PostProcess/FinalPostProcessPass.cs",
            ):
                self.assertEqual(first[name].count("SharedStreamlineHooks.CaptureFinal"), 1)
                self.assertIn("!data.streamlineHudless", first[name])
            self.assertTrue((target / "Runtime/Streamline/link.xml").exists())
            self.assertTrue((target / "Runtime/SharedStreamlineHooks.cs.meta").exists())

    def test_unknown_source_refuses_entire_patch_before_writing_any_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target, native, fixtures = self.fixture(root)
            bad = "Runtime/Passes/FinalBlitPass.cs"
            (target / bad).write_text("unknown engine source")
            with (
                patch.object(hooks, "NATIVE", native),
                self.assertRaisesRegex(RuntimeError, "refusing to patch"),
            ):
                hooks.patch_urp(root)
            for name, text in fixtures.items():
                self.assertEqual(
                    (target / name).read_text(), "unknown engine source" if name == bad else text
                )
            self.assertFalse((target / "Runtime/SharedStreamlineHooks.cs").exists())


if __name__ == "__main__":
    unittest.main()

"""Verify that native payloads and the HDRP patch survive release isolation."""

import tempfile
import unittest
from pathlib import Path

from scripts.release_streamline import stage_streamline


class StreamlineStagingTests(unittest.TestCase):
    def test_urp_excludes_hdrp_resources_before_import_but_hdrp_keeps_them(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            relative = Path(
                "LocalPackages/com.budgetgamedev.shared/Runtime/Rendering/HighDefinition/Resources"
            )
            for pipeline in ("urp", "hdrp"):
                stage = root / pipeline
                resources = stage / relative
                resources.mkdir(parents=True)
                (resources / "HDRP.shader").write_text("HDRP-only shader")
                resources.with_suffix(".meta").write_text("folder metadata")
                shared = stage / (
                    "LocalPackages/com.budgetgamedev.shared/Runtime/Rendering/"
                    "Streamline/Resources/Streamline/UIAlpha.shader"
                )
                shared.parent.mkdir(parents=True)
                shared.write_text("shared URP and HDRP shader")
                stage_streamline(root, stage, pipeline, ["linux"])
                self.assertEqual(resources.exists(), pipeline == "hdrp")
                self.assertEqual(resources.with_suffix(".meta").exists(), pipeline == "hdrp")
                shader = resources if pipeline == "hdrp" else resources.with_name("Resources~")
                self.assertEqual((shader / "HDRP.shader").read_text(), "HDRP-only shader")
                self.assertEqual(shared.read_text(), "shared URP and HDRP shader")

    def test_macos_with_framework_stages_engine_fixes_without_requiring_windows_dlls(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            settings = root / "ProjectSettings/ProjectSettings.asset"
            settings.parent.mkdir()
            settings.write_text("Standalone: ENABLE_UPSCALER_FRAMEWORK")
            with self.assertRaisesRegex(ValueError, "hooks-only"):
                stage_streamline(root, root / "stage", "urp", ["macos"])
            hook = Path(
                "Packages/com.unity.render-pipelines.universal/Runtime/SharedStreamlineHooks.cs"
            )
            (root / hook).parent.mkdir(parents=True)
            (root / hook).write_text("framework fixes")
            stage_streamline(root, root / "stage", "urp", ["macos"])
            self.assertEqual((root / "stage" / hook).read_text(), "framework fixes")
            self.assertFalse((root / "stage/LocalPackages").exists())

    def test_non_windows_does_not_require_or_copy_streamline(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for pipeline, targets in [("urp", ["macos"]), ("hdrp", ["macos"])]:
                stage_streamline(root, root / "stage", pipeline, targets)
                self.assertFalse((root / "stage").exists())

    def test_windows_hdrp_requires_both_hook_and_native_bridge(self):
        self.check_windows_staging(
            "hdrp",
            "com.unity.render-pipelines.high-definition",
            "Runtime/RenderPipeline/SharedFrameGenerationHooks.cs",
        )

    def test_windows_urp_requires_both_hook_and_native_bridge(self):
        self.check_windows_staging(
            "urp", "com.unity.render-pipelines.universal", "Runtime/SharedStreamlineHooks.cs"
        )

    def check_windows_staging(self, pipeline, package, hook_name):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            native = Path("LocalPackages/com.budgetgamedev.shared/Native~/Streamline")
            (root / native).mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, "setup.py"):
                stage_streamline(root, root / "stage", pipeline, ["windows"])
            hdrp = Path("Packages") / package
            hook = hdrp / hook_name
            (root / hook).parent.mkdir(parents=True)
            (root / hook).write_text("hook")
            with self.assertRaisesRegex(ValueError, "native build is missing"):
                stage_streamline(root, root / "stage", pipeline, ["windows"])
            if pipeline == "hdrp":
                urp = (
                    root
                    / "Packages/com.unity.render-pipelines.universal"
                    / "Runtime/SharedStreamlineHooks.cs"
                )
                urp.parent.mkdir(parents=True)
                urp.write_text("URP framework fixes")
            payload = native / "artifacts/win-x64"
            (root / payload).mkdir(parents=True)
            (root / payload / "GfxPluginBudgetGameDevStreamline.dll").write_bytes(b"bridge")
            stage_streamline(root, root / "stage", pipeline, ["windows"])
            self.assertEqual((root / "stage" / hook).read_text(), "hook")
            self.assertEqual(
                (root / "stage" / payload / "GfxPluginBudgetGameDevStreamline.dll").read_bytes(),
                b"bridge",
            )


if __name__ == "__main__":
    unittest.main()

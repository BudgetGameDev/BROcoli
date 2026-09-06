"""Verify that native payloads and the HDRP patch survive release isolation."""

import tempfile
import unittest
from pathlib import Path

from scripts.release_streamline import stage_streamline


class StreamlineStagingTests(unittest.TestCase):
    def test_urp_and_non_windows_do_not_require_or_copy_streamline(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for pipeline, targets in [("urp", ["windows"]), ("hdrp", ["macos"])]:
                stage_streamline(root, root / "stage", pipeline, targets)
                self.assertFalse((root / "stage").exists())

    def test_windows_hdrp_requires_both_hook_and_native_bridge(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            native = Path("LocalPackages/com.budgetgamedev.shared/Native~/Streamline")
            (root / native).mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, "setup.py"):
                stage_streamline(root, root / "stage", "hdrp", ["windows"])
            hdrp = Path("Packages/com.unity.render-pipelines.high-definition")
            hook = hdrp / "Runtime/RenderPipeline/SharedFrameGenerationHooks.cs"
            (root / hook).parent.mkdir(parents=True)
            (root / hook).write_text("hook")
            with self.assertRaisesRegex(ValueError, "native build is missing"):
                stage_streamline(root, root / "stage", "hdrp", ["windows"])
            payload = native / "artifacts/win-x64"
            (root / payload).mkdir(parents=True)
            (root / payload / "GfxPluginBudgetGameDevStreamline.dll").write_bytes(b"bridge")
            stage_streamline(root, root / "stage", "hdrp", ["windows"])
            self.assertEqual((root / "stage" / hook).read_text(), "hook")
            self.assertEqual(
                (root / "stage" / payload / "GfxPluginBudgetGameDevStreamline.dll").read_bytes(),
                b"bridge",
            )


if __name__ == "__main__":
    unittest.main()

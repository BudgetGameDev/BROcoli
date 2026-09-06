"""Protect the version-pinned HDRP patch against duplicate hooks and unknown source."""

import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SETUP = (
    Path(__file__).resolve().parents[2]
    / "LocalPackages/com.budgetgamedev.shared/Tools~/Streamline/setup.py"
)
SPEC = importlib.util.spec_from_file_location("streamline_setup", SETUP)
setup = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(setup)


class HdrpHookTests(unittest.TestCase):
    def test_patch_is_idempotent_and_installs_linker_rules_with_the_optional_hook(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "Packages/com.unity.render-pipelines.high-definition"
            source = target / setup.HDRP_FILE
            source.parent.mkdir(parents=True)
            original = "// reviewed fixture\n" + setup.DRAW + "\n"
            source.write_text(original)
            with patch.object(setup, "HDRP_HASH", hashlib.sha256(original.encode()).hexdigest()):
                setup.patch_hdrp(root)
                first = source.read_text()
                setup.patch_hdrp(root)
            self.assertEqual(source.read_text(), first)
            self.assertEqual(first.count(setup.HOOK), 1)
            self.assertIn(setup.HOOK + setup.DRAW, first)
            self.assertTrue((target / "Runtime/RenderPipeline/Streamline/link.xml").is_file())

    def test_unknown_or_modified_hdrp_source_is_left_untouched(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "Packages/com.unity.render-pipelines.high-definition" / setup.HDRP_FILE
            source.parent.mkdir(parents=True)
            source.write_text("unreviewed HDRP source")
            with self.assertRaisesRegex(RuntimeError, "refusing to patch"):
                setup.patch_hdrp(root)
            self.assertEqual(source.read_text(), "unreviewed HDRP source")


if __name__ == "__main__":
    unittest.main()

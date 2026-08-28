import argparse
import importlib.util
import json
import os
import tempfile
import unittest
import zipfile
from pathlib import Path

SCRIPT_PATH = Path(__file__).resolve().parents[1] / "licensed_asset_crypto.py"
SPEC = importlib.util.spec_from_file_location("licensed_asset_crypto", SCRIPT_PATH)
licensed_asset_crypto = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(licensed_asset_crypto)


class LicensedAssetCryptoTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="brocoli-licensed-tests-")
        self.root = Path(self.temporary.name)
        self.project = self.root / "Project"
        self.project.mkdir()
        self.original_project_root = licensed_asset_crypto.PROJECT_ROOT
        licensed_asset_crypto.PROJECT_ROOT = self.project
        self.original_key = os.environ.get(licensed_asset_crypto.KEY_NAME)
        os.environ[licensed_asset_crypto.KEY_NAME] = "test-key-" + "a" * 48

    def tearDown(self):
        licensed_asset_crypto.PROJECT_ROOT = self.original_project_root
        if self.original_key is None:
            os.environ.pop(licensed_asset_crypto.KEY_NAME, None)
        else:
            os.environ[licensed_asset_crypto.KEY_NAME] = self.original_key
        self.temporary.cleanup()

    def encrypt_args(self, source, output, generated_path, **overrides):
        values = {
            "input": str(source),
            "output": output,
            "generated_path": generated_path,
            "source_url": "https://assetstore.unity.com/packages/example",
            "author": "Example Publisher",
            "license": "Standard Unity Asset Store EULA",
            "title": None,
            "asset_version": None,
            "license_type": None,
            "acquired_date": None,
            "price": None,
        }
        values.update(overrides)
        return argparse.Namespace(**values)

    def test_legacy_file_round_trip_remains_supported(self):
        source = self.root / "model.fbx"
        source.write_bytes(b"legacy model bytes")
        encrypted = "Assets/Encrypted/Licensed/model.fbx.enc"
        args = self.encrypt_args(
            source,
            encrypted,
            "Assets/Resources/Generated/Licensed/model.fbx",
        )

        licensed_asset_crypto.encrypt(args)

        metadata = json.loads((self.project / f"{encrypted}.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["formatVersion"], 1)
        restored = self.root / "restored.fbx"
        licensed_asset_crypto.decrypt(argparse.Namespace(input=encrypted, output=str(restored)))
        self.assertEqual(restored.read_bytes(), source.read_bytes())

    def test_directory_package_round_trip_preserves_tree_and_meta_files(self):
        source = self.root / "WaterPackage"
        shader = source / "Publisher" / "Water" / "Water.shader"
        shader.parent.mkdir(parents=True)
        shader.write_text('Shader "Example/Water" {}\n', encoding="utf-8")
        shader.with_suffix(".shader.meta").write_text(
            "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n",
            encoding="utf-8",
        )
        (source / "Publisher" / "Water" / "Empty").mkdir()
        encrypted = "Assets/Encrypted/Licensed/water-package.zip.enc"
        args = self.encrypt_args(
            source,
            encrypted,
            "Assets/Generated/Licensed/WaterPackage",
            title="Example Water",
            asset_version="1.2.3",
            license_type="Extension Asset",
            acquired_date="2026-08-28",
            price="Free",
        )

        licensed_asset_crypto.encrypt(args)

        metadata = json.loads((self.project / f"{encrypted}.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["formatVersion"], 2)
        self.assertEqual(metadata["payloadType"], "directory")
        self.assertEqual(metadata["archiveFormat"], "zip")
        self.assertEqual(metadata["fileCount"], 2)
        self.assertEqual(len(metadata["rootGuid"]), 32)
        self.assertEqual(metadata["price"], "Free")

        restored = self.root / "RestoredWaterPackage"
        licensed_asset_crypto.decrypt(argparse.Namespace(input=encrypted, output=str(restored)))
        self.assertEqual(
            (restored / "Publisher" / "Water" / "Water.shader").read_bytes(),
            shader.read_bytes(),
        )
        self.assertEqual(
            (restored / "Publisher" / "Water" / "Water.shader.meta").read_bytes(),
            shader.with_suffix(".shader.meta").read_bytes(),
        )
        self.assertTrue((restored / "Publisher" / "Water" / "Empty").is_dir())

    def test_directory_packages_require_acquisition_metadata(self):
        source = self.root / "Package"
        source.mkdir()
        args = self.encrypt_args(
            source,
            "Assets/Encrypted/Licensed/package.zip.enc",
            "Assets/Generated/Licensed/Package",
        )

        with self.assertRaisesRegex(RuntimeError, "Directory packages require metadata"):
            licensed_asset_crypto.encrypt(args)

    def test_generated_package_path_cannot_escape_ignored_root(self):
        with self.assertRaisesRegex(RuntimeError, "safe project-relative path"):
            licensed_asset_crypto.validate_generated_path(
                "Assets/Generated/Licensed/../Plaintext", directory=True
            )

    def test_archive_extraction_rejects_parent_traversal(self):
        archive_path = self.root / "malicious.zip"
        with zipfile.ZipFile(archive_path, "w") as archive:
            archive.writestr("../escaped.txt", b"no")

        with self.assertRaisesRegex(RuntimeError, "Unsafe package archive entry"):
            licensed_asset_crypto.extract_directory_archive(
                archive_path,
                self.root / "Output",
                {"fileCount": 1, "uncompressedSize": 2},
            )
        self.assertFalse((self.root / "escaped.txt").exists())


if __name__ == "__main__":
    unittest.main()

"""Reusable builds retain caches without retaining obsolete product inputs."""

import os
import tempfile
import unittest
from pathlib import Path

from scripts.release_workspace import preserve_shader_reports, reuse_workspace, sync_tree


class ReleaseWorkspaceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()

    def fresh(self, name):
        stage = self.root / name
        for directory in ("Assets", "LocalPackages", "Packages", "ProjectSettings"):
            (stage / directory).mkdir(parents=True)
        (stage / "ProjectSettings/ProjectVersion.txt").write_text("6000.5.10f1")
        (stage / "BuildContent.json").write_text('{"product":"brocoli"}')
        (stage / "Assets/Code.cs").write_text("// unchanged")
        return stage

    def reuse(self, fresh, pipeline="hdrp", development=False):
        return reuse_workspace(fresh, self.source, "brocoli", pipeline, ["windows"], development)

    def test_reuse_keeps_cache_and_unchanged_mtime_but_removes_stale_inputs(self):
        with self.reuse(self.fresh("first")) as stage:
            (stage / "Library/ShaderCache").mkdir(parents=True)
            (stage / "Library/ShaderCache/cached").write_text("compiled")
            (stage / "LocalPackages/removed-game").mkdir()
            (stage / "LocalPackages/removed-game/code.cs").write_text("forbidden")
            os.utime(stage / "Assets/Code.cs", ns=(123456789000, 123456789000))
        fresh = self.fresh("second")
        (fresh / "Assets/New.cs").write_text("// new")
        with self.reuse(fresh) as second:
            self.assertEqual(stage, second)
            self.assertEqual((second / "Assets/Code.cs").stat().st_mtime_ns, 123456789000)
            self.assertTrue((second / "Assets/New.cs").exists())
            self.assertFalse((second / "LocalPackages/removed-game").exists())
            self.assertEqual((second / "Library/ShaderCache/cached").read_text(), "compiled")
            self.assertFalse(fresh.exists())

    def test_different_pipeline_and_mode_never_share_a_workspace(self):
        with self.reuse(self.fresh("first")) as hdrp:
            pass
        with self.reuse(self.fresh("second"), pipeline="urp") as urp:
            self.assertNotEqual(hdrp, urp)
        with self.reuse(self.fresh("third"), development=True) as development:
            self.assertNotEqual(hdrp, development)

    def test_concurrent_use_is_rejected_and_exception_releases_lease(self):
        with (
            self.assertRaisesRegex(RuntimeError, "build failed"),
            self.reuse(self.fresh("first")),
        ):
            with (
                self.assertRaisesRegex(ValueError, "already leased"),
                self.reuse(self.fresh("second")),
            ):
                self.fail("lease was ignored")
            raise RuntimeError("build failed")
        with self.reuse(self.fresh("third")):
            pass

    def test_open_editor_or_missing_ownership_marker_blocks_sync(self):
        with self.reuse(self.fresh("first")) as stage:
            (stage / "Temp").mkdir()
            lock = stage / "Temp/UnityLockfile"
            lock.touch()
        with (
            self.assertRaisesRegex(ValueError, "Close Unity"),
            self.reuse(self.fresh("second")),
        ):
            self.fail("open Editor was ignored")
        lock.unlink()
        (stage / ".release-workspace.json").unlink()
        with (
            self.assertRaisesRegex(ValueError, "unrecognized"),
            self.reuse(self.fresh("third")),
        ):
            self.fail("ownership was ignored")

    def test_sync_updates_same_size_content_and_handles_file_directory_changes(self):
        source, destination = self.root / "input", self.root / "output"
        source.write_text("new")
        destination.write_text("old")
        sync_tree(source, destination)
        self.assertEqual(destination.read_text(), "new")
        source.unlink()
        source.mkdir()
        (source / "child").write_text("x")
        sync_tree(source, destination)
        self.assertEqual((destination / "child").read_text(), "x")
        (source / "child").unlink()
        source.rmdir()
        source.write_text("file again")
        sync_tree(source, destination)
        self.assertEqual(destination.read_text(), "file again")

    def test_stripping_reports_are_preserved_beside_the_build_log(self):
        stage = self.fresh("first")
        (stage / "Temp").mkdir()
        (stage / "Temp/shader-stripping.json").write_text("{}")
        output = self.root / "player"
        output.mkdir()
        preserve_shader_reports(stage, output)
        self.assertEqual((output / "shader-stripping.json").read_text(), "{}")

"""Exercise real staging copies with two games and deliberately forbidden code."""

import importlib.util
import tempfile
import unittest
from pathlib import Path

SPEC = importlib.util.spec_from_file_location(
    "release_build", Path(__file__).parents[1] / "release-build.py"
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Release builder module could not be loaded")
release = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release)


class ReleaseIsolationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.source = Path(self.temporary.name)
        (self.source / "Assets").mkdir()
        (self.source / "ProjectSettings").mkdir()
        (self.source / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 6000.3.6f1\n"
        )
        self.packages = {
            "com.budgetgamedev.shared": [],
            "com.budgetgamedev.hub": ["com.budgetgamedev.shared"],
            "com.budgetgamedev.game.brocoli": ["com.budgetgamedev.shared"],
            "com.budgetgamedev.game.other": ["com.budgetgamedev.shared"],
            "com.budgetgamedev.autoplay": [],
            "com.budgetgamedev.autoplay.brocoli": [
                "com.budgetgamedev.autoplay",
                "com.budgetgamedev.game.brocoli",
            ],
            "com.budgetgamedev.autoplay.other": [
                "com.budgetgamedev.autoplay",
                "com.budgetgamedev.game.other",
            ],
        }
        for name, dependencies in self.packages.items():
            directory = self.source / "LocalPackages" / name
            release.write_json(
                directory / "package.json",
                {"name": name, "dependencies": dict.fromkeys(dependencies, "0.1.0")},
            )
            release.write_json(directory / "Runtime/test.asmdef", {"name": name})
            (directory / "Runtime/Code.cs").write_text("// " + name)
            (directory / "Resources").mkdir()
            (directory / "Resources/OnlyThisGame.txt").write_text(name)
        release.write_json(
            self.source / "Packages/manifest.json",
            {
                "dependencies": {
                    **{name: "file:../LocalPackages/" + name for name in self.packages},
                    "com.unity.pipeline": "0.5.0-exp.1",
                },
                "testables": list(self.packages),
            },
        )

    def test_single_game_physically_omits_other_game_launcher_and_all_autoplay(self):
        stage = release.stage_project(self.source, "brocoli")
        self.assertEqual(
            {p.name for p in (stage / "LocalPackages").iterdir()},
            {
                "com.budgetgamedev.shared",
                "com.budgetgamedev.game.brocoli",
            },
        )
        resources = list(stage.rglob("OnlyThisGame.txt"))
        self.assertEqual(len(resources), 2)
        self.assertNotIn("testables", release.read_json(stage / "Packages/manifest.json"))
        self.assertNotIn(
            "com.unity.pipeline",
            release.read_json(stage / "Packages/manifest.json")["dependencies"],
        )
        self.assertFalse((stage / "Library").exists())

    def test_launcher_contains_both_games_and_no_autoplay(self):
        stage = release.stage_project(self.source, "launcher")
        plan = release.read_json(stage / "BuildContent.json")
        self.assertEqual(len(plan["gamePackages"]), 2)
        self.assertIn("com.budgetgamedev.hub", plan["localPackages"])
        self.assertFalse(any("autoplay" in name for name in plan["localPackages"]))

    def test_second_game_has_no_brocoli_sources_or_resources(self):
        stage = release.stage_project(self.source, "other")
        names = {p.name for p in (stage / "LocalPackages").iterdir()}
        self.assertEqual(names, {"com.budgetgamedev.shared", "com.budgetgamedev.game.other"})

    def test_development_injects_only_selected_games_adapter(self):
        plan, _, _ = release.create_plan(self.source, "brocoli", True)
        self.assertIn("com.budgetgamedev.autoplay.brocoli", plan["localPackages"])
        self.assertNotIn("com.budgetgamedev.autoplay.other", plan["localPackages"])

    def test_unknown_product_fails_before_creating_stage(self):
        with self.assertRaisesRegex(ValueError, "Unknown product"):
            release.stage_project(self.source, "missing")
        self.assertFalse((self.source / "build").exists())

    def test_unstaged_remote_game_cannot_be_imported_accidentally(self):
        path = self.source / "Packages/manifest.json"
        manifest = release.read_json(path)
        manifest["dependencies"]["com.budgetgamedev.game.remote"] = (
            "https://example.invalid/game.git"
        )
        release.write_json(path, manifest)
        with self.assertRaisesRegex(ValueError, "must exist under LocalPackages"):
            release.create_plan(self.source, "brocoli")

    def test_transitive_dependency_cannot_pull_excluded_code_back_in(self):
        path = self.source / "LocalPackages/com.budgetgamedev.shared/package.json"
        data = release.read_json(path)
        data["dependencies"] = {"com.budgetgamedev.autoplay": "0.1.0"}
        release.write_json(path, data)
        with self.assertRaisesRegex(ValueError, "depends on excluded package"):
            release.create_plan(self.source, "brocoli")

    def test_each_stage_is_fresh_and_source_manifest_untouched(self):
        manifest = (self.source / "Packages/manifest.json").read_bytes()
        first = release.stage_project(self.source, "brocoli")
        (first / "stale.dll").write_text("stale")
        second = release.stage_project(self.source, "brocoli")
        self.assertNotEqual(first, second)
        self.assertFalse((second / "stale.dll").exists())
        self.assertEqual(manifest, (self.source / "Packages/manifest.json").read_bytes())

    def test_audit_rejects_excluded_assembly(self):
        stage = release.stage_project(self.source, "brocoli")
        output = self.source / "player"
        output.mkdir()
        (output / "com.budgetgamedev.game.other.dll").write_bytes(b"test")
        with self.assertRaisesRegex(ValueError, "Forbidden assembly"):
            release.audit_player(stage, output)

    def test_audit_rejects_empty_output(self):
        stage = release.stage_project(self.source, "brocoli")
        output = self.source / "empty-player"
        output.mkdir()
        with self.assertRaisesRegex(ValueError, "No built player code"):
            release.audit_player(stage, output)

    def test_audit_rejects_autoplay_type_moved_into_game_assembly(self):
        stage = release.stage_project(self.source, "brocoli")
        output = self.source / "player"
        output.mkdir()
        (output / "BudgetGameDev.Games.Brocoli.dll").write_bytes(
            b"metadata\0AutoplayController\0more"
        )
        with self.assertRaisesRegex(ValueError, "Autoplay type metadata"):
            release.audit_player(stage, output)

    def test_audit_rejects_development_hooks_in_runtime_binaries(self):
        stage = release.stage_project(self.source, "brocoli")
        output = self.source / "player"
        output.mkdir()
        for symbol in (
            "NavigationUpdatePending",
            "get_NavigationUpdatePending",
            "PreviewNavigationDelta",
            "SuppressFocusLossPause",
            "get_SuppressFocusLossPause",
            "set_SuppressFocusLossPause",
        ):
            with self.subTest(symbol=symbol):
                assembly = output / (
                    "BudgetGameDev.Shared.dll"
                    if "FocusLoss" in symbol
                    else "BudgetGameDev.Games.Brocoli.dll"
                )
                assembly.write_bytes(b"metadata\0" + symbol.encode() + b"\0more")
                with self.assertRaisesRegex(ValueError, "Autoplay type metadata"):
                    release.audit_player(stage, output)
                assembly.write_bytes(b"clean metadata")


if __name__ == "__main__":
    unittest.main()

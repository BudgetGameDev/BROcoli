import contextlib
import importlib.util
import io
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

SCRIPT_PATH = SCRIPTS_ROOT / "ci_receipt.py"
SPEC = importlib.util.spec_from_file_location("ci_receipt", SCRIPT_PATH)
ci_receipt = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ci_receipt)


def run_quiet(function):
    """Call a reporting function without its output reaching the test log."""
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        return function()


class CiReceiptTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="brocoli-receipt-tests-")
        self.addCleanup(self.directory.cleanup)
        self.project = Path(self.directory.name)

        subprocess.run(["git", "init", "--quiet"], cwd=self.project, check=True)
        subprocess.run(
            ["git", "config", "user.email", "tests@example.com"],
            cwd=self.project,
            check=True,
        )
        subprocess.run(["git", "config", "user.name", "Tests"], cwd=self.project, check=True)
        (self.project / "source.txt").write_text("original\n", encoding="utf-8")
        self.commit("first commit")

        self.player = self.project / "build" / "WebGL"
        (self.player / "Build").mkdir(parents=True)
        (self.player / "index.html").write_text("<html></html>", encoding="utf-8")
        (self.player / "Build" / "player.wasm").write_text("wasm", encoding="utf-8")

        self.original_root = ci_receipt.ROOT
        self.original_receipt = ci_receipt.RECEIPT
        self.original_player = ci_receipt.PLAYER
        ci_receipt.ROOT = self.project
        ci_receipt.RECEIPT = self.project / "build" / "ci-pass.json"
        ci_receipt.PLAYER = self.player

    def tearDown(self):
        ci_receipt.ROOT = self.original_root
        ci_receipt.RECEIPT = self.original_receipt
        ci_receipt.PLAYER = self.original_player

    def commit(self, message):
        subprocess.run(["git", "add", "--all"], cwd=self.project, check=True)
        subprocess.run(["git", "commit", "--quiet", "-m", message], cwd=self.project, check=True)

    def touch_player_file(self, name, contents):
        target = self.player / name
        target.write_text(contents, encoding="utf-8")
        # mtime resolution is coarse enough that a same-second rewrite can hash
        # identically, so move it deliberately out of the recorded state.
        stat = target.stat()
        os.utime(target, ns=(stat.st_atime_ns, stat.st_mtime_ns + 1_000_000_000))

    def test_a_written_receipt_verifies(self):
        run_quiet(ci_receipt.write)
        self.assertEqual(run_quiet(ci_receipt.verify), 0)

    def test_missing_receipt_is_refused(self):
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_cleared_receipt_is_refused(self):
        run_quiet(ci_receipt.write)
        run_quiet(ci_receipt.clear)
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_edit_after_the_pass_is_refused(self):
        run_quiet(ci_receipt.write)
        (self.project / "source.txt").write_text("edited\n", encoding="utf-8")
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_new_commit_after_the_pass_is_refused(self):
        run_quiet(ci_receipt.write)
        (self.project / "source.txt").write_text("edited\n", encoding="utf-8")
        self.commit("second commit")
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_swapped_player_is_refused(self):
        run_quiet(ci_receipt.write)
        self.touch_player_file("index.html", "<html>replaced</html>")
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_added_player_file_is_refused(self):
        run_quiet(ci_receipt.write)
        self.touch_player_file("extra.js", "console.log(1);")
        self.assertEqual(run_quiet(ci_receipt.verify), 1)

    def test_publish_outputs_do_not_invalidate_the_receipt(self):
        run_quiet(ci_receipt.write)
        # cd.sh writes both of these into the player after verifying, so a
        # re-run must not be blocked by its own output.
        self.touch_player_file("version.json", '{"buildId": "later"}')
        self.touch_player_file("manifest-staging.json", '{"name": "later"}')
        self.assertEqual(run_quiet(ci_receipt.verify), 0)


if __name__ == "__main__":
    unittest.main()

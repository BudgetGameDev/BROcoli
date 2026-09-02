import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

SCRIPT_PATH = SCRIPTS_ROOT / "check_coverage.py"
SPEC = importlib.util.spec_from_file_location("check_coverage", SCRIPT_PATH)
# These scripts are CLI entry points rather than an installed package, so a test
# reaches them by path. spec_from_file_location returns None for a file it cannot
# load, which would otherwise surface as an AttributeError two lines later.
if SPEC is None or SPEC.loader is None:
    raise ImportError(f"cannot load {SCRIPT_PATH}")
check_coverage = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(check_coverage)

MODULE_TEMPLATE = """<CoverageSession>
  <Modules>
{modules}  </Modules>
</CoverageSession>
"""

MODULE_ENTRY = """    <Module>
      <ModuleName>{name}</ModuleName>
      <Files>
{files}      </Files>
      <Classes><Class><Methods><Method><SequencePoints>
{points}      </SequencePoints></Method></Methods></Class></Classes>
    </Module>
"""


def run_quiet(function):
    """Call a reporting function without its output reaching the test log."""
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        return function()


class CheckCoverageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="brocoli-coverage-tests-")
        self.addCleanup(self.directory.cleanup)
        self.project = Path(self.directory.name)

        self.runtime = self.project / "LocalPackages" / "game" / "Runtime"
        self.runtime.mkdir(parents=True)
        self.report_dir = self.project / "build" / "Coverage"
        self.report_dir.mkdir(parents=True)
        self.baseline = self.project / ".quality" / "coverage-baseline.tsv"
        self.baseline.parent.mkdir(parents=True)

        self.patch(check_coverage, "ROOT", self.project)
        self.patch(check_coverage, "BASELINE", self.baseline)
        self.patch(check_coverage, "MEASURED", {"Game": self.runtime})

    def patch(self, module, name, value):
        original = getattr(module, name)
        setattr(module, name, value)
        self.addCleanup(setattr, module, name, original)

    def write_source(self, name, body="class Sample { }\n"):
        path = self.runtime / name
        path.write_text(body, encoding="utf-8")
        return path

    def write_report(self, covered_by_file, module="Game"):
        """covered_by_file maps a runtime file name to a list of (line, visited)."""
        files = []
        points = []
        for uid, (name, lines) in enumerate(covered_by_file.items(), 1):
            files.append(f'        <File uid="{uid}" fullPath="{self.runtime / name}" />\n')
            for line, visited in lines:
                points.append(
                    f'        <SequencePoint fileid="{uid}" sl="{line}" '
                    f'vc="{1 if visited else 0}" />\n'
                )
        entry = MODULE_ENTRY.format(name=module, files="".join(files), points="".join(points))
        (self.report_dir / "results.xml").write_text(
            MODULE_TEMPLATE.format(modules=entry), encoding="utf-8"
        )

    def write_baseline(self, entries):
        lines = ["# baseline"] + [f"{count}\t{path}" for path, count in entries]
        self.baseline.write_text("\n".join(lines) + "\n", encoding="utf-8")

    def check(self):
        return run_quiet(lambda: check_coverage.main([str(self.report_dir)]))

    def relative(self, name):
        return (self.runtime / name).relative_to(self.project).as_posix()

    def test_fully_covered_runtime_passes_without_a_baseline_entry(self):
        self.write_source("Covered.cs")
        self.write_report({"Covered.cs": [(1, True), (2, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 0)

    def test_uncovered_file_without_a_baseline_entry_fails(self):
        self.write_source("Uncovered.cs")
        self.write_report({"Uncovered.cs": [(1, True), (2, False)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 1)

    def test_uncovered_file_within_its_allowance_passes(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, True), (2, False), (3, False)]})
        self.write_baseline([(self.relative("Legacy.cs"), 2)])
        self.assertEqual(self.check(), 0)

    def test_coverage_regression_beyond_the_allowance_fails(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, False), (2, False), (3, False)]})
        self.write_baseline([(self.relative("Legacy.cs"), 2)])
        self.assertEqual(self.check(), 1)

    def test_improving_below_the_allowance_passes_so_the_ratchet_never_blocks_progress(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, True), (2, True), (3, False)]})
        self.write_baseline([(self.relative("Legacy.cs"), 2)])
        self.assertEqual(self.check(), 0)

    def test_file_reaching_full_coverage_must_drop_its_baseline_entry(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, True), (2, True)]})
        self.write_baseline([(self.relative("Legacy.cs"), 2)])
        self.assertEqual(self.check(), 1)

    def test_baseline_entry_for_a_missing_file_is_stale(self):
        self.write_source("Present.cs")
        self.write_report({"Present.cs": [(1, True)]})
        self.write_baseline([(self.relative("Gone.cs"), 3)])
        self.assertEqual(self.check(), 1)

    def test_zero_allowance_is_rejected_in_favour_of_deleting_the_entry(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, True)]})
        self.write_baseline([(self.relative("Legacy.cs"), 0)])
        self.assertEqual(self.check(), 1)

    def test_duplicate_baseline_entries_are_rejected(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, False)]})
        self.write_baseline([(self.relative("Legacy.cs"), 5), (self.relative("Legacy.cs"), 5)])
        self.assertEqual(self.check(), 1)

    def test_malformed_baseline_line_is_rejected(self):
        self.write_source("Legacy.cs")
        self.write_report({"Legacy.cs": [(1, True)]})
        self.baseline.write_text("not a baseline row\n", encoding="utf-8")
        self.assertEqual(self.check(), 1)

    def test_file_without_executable_lines_needs_no_entry(self):
        # An interface or enum contributes no sequence points, so the report
        # never mentions it. There is nothing there to cover.
        self.write_source("Covered.cs")
        self.write_source("IThing.cs", "interface IThing { }\n")
        self.write_report({"Covered.cs": [(1, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 0)

    def test_unmeasured_assembly_fails_rather_than_silently_passing(self):
        self.write_source("Covered.cs")
        self.write_report({"Covered.cs": [(1, True)]}, module="SomethingElse")
        self.write_baseline([])
        self.assertEqual(self.check(), 1)

    def test_coverage_suppression_attribute_is_rejected(self):
        self.write_source(
            "Suppressed.cs",
            "[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\nclass Sample { }\n",
        )
        self.write_report({"Suppressed.cs": [(2, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 1)

    def test_unity_coverage_suppression_attribute_is_rejected(self):
        self.write_source("Suppressed.cs", "[ExcludeFromCoverage]\nclass Sample { }\n")
        self.write_report({"Suppressed.cs": [(2, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 1)

    def test_the_word_in_a_comment_is_not_a_suppression_attribute(self):
        self.write_source(
            "Documented.cs",
            "// ExcludeFromCodeCoverage is banned by the coverage gate.\nclass Sample { }\n",
        )
        self.write_report({"Documented.cs": [(2, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 0)

    def test_a_line_visited_by_any_sequence_point_counts_as_covered(self):
        self.write_source("Branchy.cs")
        self.write_report({"Branchy.cs": [(1, False), (1, True)]})
        self.write_baseline([])
        self.assertEqual(self.check(), 0)

    def test_missing_report_directory_is_reported(self):
        self.write_baseline([])
        self.assertEqual(run_quiet(lambda: check_coverage.main([str(self.project / "absent")])), 1)

    def test_usage_error_without_a_report_directory(self):
        self.assertEqual(run_quiet(lambda: check_coverage.main([])), 2)

    def test_write_baseline_records_only_files_with_uncovered_lines(self):
        self.write_source("Covered.cs")
        self.write_source("Legacy.cs")
        self.write_report(
            {"Covered.cs": [(1, True)], "Legacy.cs": [(1, False), (2, False)]},
        )
        self.assertEqual(
            run_quiet(lambda: check_coverage.main([str(self.report_dir), "--write-baseline"])), 0
        )
        written = self.baseline.read_text(encoding="utf-8")
        self.assertIn(f"2\t{self.relative('Legacy.cs')}", written)
        self.assertNotIn("Covered.cs", written)
        self.assertEqual(self.check(), 0)

    def test_write_baseline_refuses_an_incomplete_report(self):
        self.write_source("Covered.cs")
        self.write_report({"Covered.cs": [(1, True)]}, module="SomethingElse")
        self.assertEqual(
            run_quiet(lambda: check_coverage.main([str(self.report_dir), "--write-baseline"])), 1
        )
        self.assertFalse(self.baseline.exists())


if __name__ == "__main__":
    unittest.main()

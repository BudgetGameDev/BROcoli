import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPORT_READER = Path(__file__).parents[1] / "unity_build_report.py"


class UnityBuildReportTests(unittest.TestCase):
    def run_summary(self, result: dict) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            response_path = Path(directory) / "response.json"
            response_path.write_text(
                json.dumps({"data": {"result": result}}),
                encoding="utf-8",
            )
            return subprocess.run(
                [sys.executable, str(REPORT_READER), "summary", str(response_path)],
                check=False,
                capture_output=True,
                text=True,
            )

    def test_clean_success_passes(self) -> None:
        completed = self.run_summary(
            {
                "result": "Succeeded",
                "totalErrors": 0,
                "totalWarnings": 0,
            }
        )

        self.assertEqual(completed.returncode, 0)
        self.assertIn("0 errors, 0 warnings", completed.stdout)

    def test_success_with_warning_fails(self) -> None:
        completed = self.run_summary(
            {
                "result": "Succeeded",
                "totalErrors": 0,
                "totalWarnings": 1,
                "warnings": ["A shader emitted a warning."],
            }
        )

        self.assertEqual(completed.returncode, 1)
        self.assertIn("A shader emitted a warning.", completed.stderr)


if __name__ == "__main__":
    unittest.main()

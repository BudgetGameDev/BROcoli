import tempfile
from pathlib import Path
import unittest
from scripts.release_sensors import PAYLOAD, stage_sensors


class SensorStagingTests(unittest.TestCase):
    def test_non_windows_needs_no_payload(self):
        stage_sensors(Path("missing"), Path("unused"), ["linux"])

    def test_missing_payload_gives_build_instruction(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "build-hardware-sensors.py"):
                stage_sensors(Path(directory), Path(directory) / "stage", ["windows"])

    def test_entire_runtime_and_notices_are_carried(self):
        with tempfile.TemporaryDirectory() as directory:
            source, stage = Path(directory) / "source", Path(directory) / "stage"
            (source / PAYLOAD / "Licenses").mkdir(parents=True)
            for name in ("HardwareSensors.exe", "HardwareSensors.runtimeconfig.json", "THIRD-PARTY-NOTICES.txt", "Licenses/runtime.txt"):
                (source / PAYLOAD / name).write_text(name)
            stage_sensors(source, stage, ["windows"])
            self.assertEqual((stage / PAYLOAD / "Licenses/runtime.txt").read_text(), "Licenses/runtime.txt")

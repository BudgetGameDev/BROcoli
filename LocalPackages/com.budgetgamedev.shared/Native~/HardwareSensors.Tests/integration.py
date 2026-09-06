"""Run after publishing HardwareSensors and building HardwareSensors.Tests.

Uses a renamed copy of OUR idle test executable to simulate a monitor process.
It neither launches a real tuning tool nor changes any hardware setting.
"""
import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import threading
import unittest

directory = Path(__file__).resolve().parent
helper = directory.parent / "HardwareSensors/artifacts/win-x64/HardwareSensors.exe"
test_bin = directory / "bin/Release/net8.0-windows/win-x64"
dummy = test_bin / "MSIAfterburner.exe"


class RunningReader:
    def __init__(self):
        self.process = subprocess.Popen([str(helper), "--parent", str(os.getpid())],
                                        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                        text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW)
        self.messages = queue.Queue()
        def read():
            for line in self.process.stdout:
                if line.startswith("{"):
                    self.messages.put(json.loads(line))
        self.reader_thread = threading.Thread(target=read, daemon=True)
        self.reader_thread.start()

    def snapshot(self, expected):
        for _ in range(10):
            value = self.messages.get(timeout=30)
            if value["state"] == expected:
                return value
        raise AssertionError("Expected reader state " + expected)

    def close(self):
        self.process.terminate()
        self.process.wait(timeout=10)
        self.reader_thread.join(timeout=5)
        self.process.stdout.close()


class Integration(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        shutil.copy2(test_bin / "HardwareSensors.Tests.exe", dummy)

    def reader(self):
        reader = RunningReader()
        self.addCleanup(reader.close)
        return reader

    def monitor(self):
        process = subprocess.Popen([str(dummy), "--hold"], creationflags=subprocess.CREATE_NO_WINDOW)
        def close():
            if process.poll() is None:
                process.terminate()
            process.wait(timeout=10)
        self.addCleanup(close)
        return process

    def assert_withheld(self, snapshot):
        self.assertTrue(snapshot["diskSmart"], "SMART still returns drive records")
        self.assertTrue(snapshot["readings"], "Firmware memory configuration retained")
        self.assertTrue(all(r["category"] == "Memory" for r in snapshot["readings"]))
        self.assertFalse(any(r["type"] in ("Voltage", "Temperature", "Load") for r in snapshot["readings"]))

    def test_existing_monitor_prevents_discovery(self):
        self.monitor()
        value = self.reader().snapshot("Hardware probing paused")
        self.assert_withheld(value)
        self.assertIn("MSIAfterburner", json.dumps(value["notices"]))

    def test_monitor_starting_later_pauses_and_latches(self):
        reader = self.reader()
        initial = reader.snapshot("Ready")
        self.assertTrue(any(r["category"].startswith("Gpu") for r in initial["readings"]))
        monitor = self.monitor()
        self.assert_withheld(reader.snapshot("Hardware probing paused"))
        monitor.terminate()
        monitor.wait(timeout=10)
        self.assert_withheld(reader.snapshot("Hardware probing paused"))

    def test_second_player_cannot_probe_concurrently(self):
        self.reader().snapshot("Ready")
        second = self.reader().snapshot("Hardware probing paused")
        self.assert_withheld(second)
        self.assertIn("already owns hardware probing", json.dumps(second["notices"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)

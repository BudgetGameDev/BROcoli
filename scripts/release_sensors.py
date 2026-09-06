"""Carry the sensor sidecar into isolated Windows release projects."""
import shutil
from pathlib import Path

PAYLOAD = Path("LocalPackages/com.budgetgamedev.shared/Native~/HardwareSensors/artifacts/win-x64")


def stage_sensors(source: Path, stage: Path, targets: list[str]) -> None:
    if "windows" not in targets:
        return
    for name in ("HardwareSensors.exe", "HardwareSensors.runtimeconfig.json", "THIRD-PARTY-NOTICES.txt"):
        if not (source / PAYLOAD / name).is_file():
            raise ValueError("Hardware sensor payload missing. Run python scripts/build-hardware-sensors.py first.")
    shutil.copytree(source / PAYLOAD, stage / PAYLOAD, dirs_exist_ok=True)

#!/usr/bin/env python3
"""Enforce a 300-line cap while ratcheting down grandfathered legacy files."""

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LIMIT = 300
BASELINE = ROOT / ".quality" / "loc-baseline.tsv"
SOURCE_ROOTS = (
    ROOT / "Assets" / "Scripts",
    ROOT / "Assets" / "Editor",
    ROOT / "Assets" / "Plugins" / "WebGL",
    ROOT / "scripts",
)
SOURCE_SUFFIXES = {".cs", ".jslib", ".ps1", ".py", ".sh"}
ROOT_SOURCES = (ROOT / "ci.sh", ROOT / "format.sh", ROOT / ".githooks" / "pre-push")


def physical_lines(path: Path) -> int:
    data = path.read_bytes()
    if not data:
        return 0
    return data.count(b"\n") + (0 if data.endswith(b"\n") else 1)


def source_files() -> list[Path]:
    files: set[Path] = set()
    for source_root in SOURCE_ROOTS:
        if not source_root.exists():
            continue
        files.update(
            path
            for path in source_root.rglob("*")
            if path.is_file() and path.suffix.lower() in SOURCE_SUFFIXES
        )
    files.update(path for path in ROOT_SOURCES if path.is_file())
    return sorted(files)


def load_baseline() -> dict[str, int]:
    baseline: dict[str, int] = {}
    for number, raw_line in enumerate(BASELINE.read_text(encoding="utf-8").splitlines(), 1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        count_text, separator, relative_path = line.partition("\t")
        if not separator or not count_text.isdigit() or not relative_path:
            raise ValueError(f"{BASELINE.relative_to(ROOT)}:{number}: expected '<lines>\\t<path>'")
        if relative_path in baseline:
            raise ValueError(f"{BASELINE.relative_to(ROOT)}:{number}: duplicate {relative_path}")
        baseline[relative_path] = int(count_text)
    return baseline


def main() -> int:
    try:
        baseline = load_baseline()
    except (OSError, ValueError) as error:
        print(f"source-size: {error}", file=sys.stderr)
        return 1

    failures: list[str] = []
    debt: list[str] = []
    seen: set[str] = set()

    for path in source_files():
        relative = path.relative_to(ROOT).as_posix()
        lines = physical_lines(path)
        allowance = baseline.get(relative)
        seen.add(relative)

        if lines <= LIMIT:
            if allowance is not None:
                failures.append(
                    f"{relative}: now {lines} lines; remove its obsolete baseline entry"
                )
            continue

        if allowance is None:
            failures.append(f"{relative}: {lines} lines exceeds the hard {LIMIT}-line limit")
        elif lines > allowance:
            failures.append(
                f"{relative}: grew to {lines} lines (legacy ceiling {allowance}, target {LIMIT})"
            )
        else:
            debt.append(f"{relative}: {lines} lines (grandfathered ceiling {allowance})")

    for relative in sorted(set(baseline) - seen):
        failures.append(f"{relative}: baseline entry is stale because the file is missing")

    if failures:
        print(f"source-size: FAIL ({LIMIT}-line source-file limit)", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1

    print(f"source-size: PASS ({LIMIT}-line limit; {len(debt)} legacy files ratcheted)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Fail when a tracked source file contains an emoji.

Emoji in source are a portability bug, not a style preference. The console that
runs these scripts decides the encoding: Windows PowerShell 5.1 reads a UTF-8
file without a BOM as ANSI, so an emoji arrives as mojibake, and a redirected
log or a CI transcript mangles it again. Severity already has a channel --
stderr, an exit code, a colour -- that survives every one of those hops.

Typography is not affected: an em dash, a middle dot, a degree sign, or a
letter with a diacritic is ordinary text and stays allowed.
"""

import subprocess
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent

SOURCE_SUFFIXES = {".cjs", ".cs", ".js", ".jslib", ".mjs", ".ps1", ".psd1", ".py", ".sh"}

# Pictographic ranges only. Everything a document legitimately needs -- U+2014
# em dash, U+00B7 middle dot, U+2022 bullet, U+00B0 degree, U+2026 ellipsis --
# sits outside them on purpose.
EMOJI_RANGES = (
    (0x231A, 0x231B),
    (0x23E9, 0x23FA),
    (0x2600, 0x27BF),
    (0x2B00, 0x2BFF),
    (0xFE0F, 0xFE0F),
    (0x1F000, 0x1FAFF),
)


def is_emoji(character: str) -> bool:
    codepoint = ord(character)
    return any(start <= codepoint <= end for start, end in EMOJI_RANGES)


def tracked_sources() -> list[Path]:
    listing = subprocess.run(
        ["git", "ls-files"],
        capture_output=True,
        check=True,
        cwd=PROJECT_ROOT,
        text=True,
    )
    return [
        PROJECT_ROOT / line
        for line in listing.stdout.splitlines()
        if line and Path(line).suffix.lower() in SOURCE_SUFFIXES
    ]


def offences(path: Path) -> list[str]:
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return []

    relative = path.relative_to(PROJECT_ROOT).as_posix()
    found = []
    for number, line in enumerate(text.splitlines(), start=1):
        characters = sorted({character for character in line if is_emoji(character)})
        if characters:
            names = " ".join(f"U+{ord(character):04X}" for character in characters)
            found.append(f"{relative}:{number}: {names}")
    return found


def main() -> int:
    failures = []
    for path in tracked_sources():
        failures.extend(offences(path))

    if failures:
        print("no-emoji: FAIL", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        print(
            "Emoji are not portable across the consoles these sources run in. See AGENTS.md.",
            file=sys.stderr,
        )
        return 1

    print("no-emoji: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

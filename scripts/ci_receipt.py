#!/usr/bin/env python3
"""Record and check the receipt proving ./ci.sh passed for this exact tree.

./cd.sh publishes whatever sits in build/WebGL, so a green ci.sh run is the only
thing standing between a broken player and GitHub Pages. The receipt binds that
run to the commit, the working tree, and the built player, so a stale or
unrelated pass cannot authorise a publish.
"""

import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RECEIPT = ROOT / "build" / "ci-pass.json"
PLAYER = ROOT / "build" / "WebGL"
RECEIPT_VERSION = 1

# cd.sh writes these into the player itself, so they must not take part in the
# digest or a re-run of cd.sh would invalidate its own receipt.
PUBLISH_OUTPUTS = {"version.json", "manifest-staging.json"}


def git(*arguments: str) -> str:
    return subprocess.run(
        ["git", *arguments],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def source_state() -> str:
    """HEAD plus any uncommitted change, so an edit invalidates the receipt."""
    digest = hashlib.sha256()
    digest.update(git("rev-parse", "HEAD").encode())
    digest.update(b"\0")
    digest.update(git("status", "--porcelain").encode())
    return digest.hexdigest()


def player_state() -> str:
    """Every built file's path, size and mtime, so a swapped player is caught."""
    if not PLAYER.is_dir():
        return ""

    digest = hashlib.sha256()
    for path in sorted(PLAYER.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(PLAYER).as_posix()
        if relative in PUBLISH_OUTPUTS:
            continue
        stat = path.stat()
        digest.update(f"{relative}\0{stat.st_size}\0{stat.st_mtime_ns}\0".encode())
    return digest.hexdigest()


def write() -> int:
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(
        json.dumps(
            {
                "version": RECEIPT_VERSION,
                "head": git("rev-parse", "HEAD"),
                "sourceState": source_state(),
                "playerState": player_state(),
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    return 0


def clear() -> int:
    RECEIPT.unlink(missing_ok=True)
    return 0


def refuse(reason: str) -> int:
    print(f"cd: refusing to publish - {reason}", file=sys.stderr)
    print("cd: run ./ci.sh and let every gate pass first.", file=sys.stderr)
    return 1


def verify() -> int:
    try:
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return refuse("./ci.sh has not passed for this tree")
    except (OSError, ValueError):
        return refuse("the ci.sh receipt is unreadable")

    if receipt.get("version") != RECEIPT_VERSION:
        return refuse("the ci.sh receipt was written by another version")
    if receipt.get("head") != git("rev-parse", "HEAD"):
        return refuse("the ci.sh receipt is for a different commit")
    if receipt.get("sourceState") != source_state():
        return refuse("the tree changed after ./ci.sh passed")
    if receipt.get("playerState") != player_state():
        return refuse("build/WebGL changed after ./ci.sh passed")

    print(f"cd: ./ci.sh passed for {receipt['head'][:7]}")
    return 0


def main() -> int:
    modes = {"write": write, "clear": clear, "verify": verify}
    if len(sys.argv) != 2 or sys.argv[1] not in modes:
        print("usage: ci_receipt.py {write|clear|verify}", file=sys.stderr)
        return 2
    return modes[sys.argv[1]]()


if __name__ == "__main__":
    raise SystemExit(main())

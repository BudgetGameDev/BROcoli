#!/usr/bin/env python3
"""Read the Unity pipeline's build and build_status payloads for the CI gate."""

import json
import sys
from typing import Optional


def build_result(path: str) -> dict:
    with open(path, encoding="utf-8") as stream:
        document = json.load(stream)

    result = (document.get("data") or {}).get("result")
    if isinstance(result, str):
        # build_status nests its report as a JSON string.
        result = json.loads(result)
    if not isinstance(result, dict):
        raise ValueError("pipeline response carried no result object")
    return result


def report_status(result: dict, expected_build_id: Optional[str]) -> int:
    # The pipeline reports refusals inside an otherwise successful envelope.
    if result.get("status") == "error":
        print(result.get("message") or "the build was refused", file=sys.stderr)
        return 1

    # build_status keeps the previous report until the next build registers, so
    # a poll that lands too early would otherwise read a stale "completed".
    build_id = result.get("buildId")
    if expected_build_id and build_id and build_id != expected_build_id:
        print("pending")
        return 0

    print(result.get("status") or "")
    return 0


def report_build_id(result: dict) -> int:
    print(result.get("buildId") or "")
    return 0


def report_summary(result: dict) -> int:
    outcome = result.get("result")
    errors = int(result.get("totalErrors", 0) or 0)
    seconds = int(result.get("buildTimeMs", 0) or 0) / 1000

    print(
        f"unity-webgl-build: {outcome} in {seconds:.0f}s, "
        f"{result.get('totalSizeBytes', 0)} bytes, "
        f"{errors} errors, {result.get('totalWarnings', 0)} warnings"
    )

    if outcome == "Succeeded" and not errors:
        return 0

    for message in (result.get("errors") or [])[:20]:
        print(f"  {message}", file=sys.stderr)
    return 1


def main() -> int:
    if len(sys.argv) not in {3, 4} or sys.argv[1] not in {"status", "build-id", "summary"}:
        print(
            "usage: unity_build_report.py {status|build-id|summary} "
            "<response.json> [expected-build-id]",
            file=sys.stderr,
        )
        return 2

    mode, path = sys.argv[1], sys.argv[2]
    expected_build_id = sys.argv[3] if len(sys.argv) == 4 else None
    try:
        result = build_result(path)
    except (OSError, ValueError) as error:
        print(f"unity-webgl-build: {error}", file=sys.stderr)
        return 1

    if mode == "status":
        return report_status(result, expected_build_id)
    if mode == "build-id":
        return report_build_id(result)
    return report_summary(result)


if __name__ == "__main__":
    raise SystemExit(main())

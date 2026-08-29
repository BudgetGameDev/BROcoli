#!/usr/bin/env bash
# Produce the deployable WebGL player used by the host-side smoke checks.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_PATH="$PROJECT_PATH/build/WebGL"
BUILD_LOG="${TMPDIR:-/tmp}/brocoli_webgl_build.log"

if ! command -v unity >/dev/null 2>&1; then
    echo "unity-webgl-build: Unity CLI is required" >&2
    exit 2
fi

# Never let a stale player satisfy the smoke checks after a failed build.
rm -rf "$OUTPUT_PATH"

connected_editor_pid() {
    unity status --project-path "$PROJECT_PATH" --format json 2>/dev/null | python3 -c '
import json, os, sys

project = os.path.realpath(sys.argv[1])
document = json.load(sys.stdin)
instances = (document.get("data") or {}).get("instances") or []
for instance in instances:
    if (
        isinstance(instance, dict)
        and os.path.realpath(instance.get("project") or "") == project
        and instance.get("state") == "ready"
    ):
        print(instance.get("pid") or "")
        break
' "$PROJECT_PATH"
}

EDITOR_PID="$(connected_editor_pid || true)"
if [ -n "$EDITOR_PID" ]; then
    if ! ps -p "$EDITOR_PID" -ww -o args= | grep -Eq -- '(^|[[:space:]])-automated([[:space:]]|$)'; then
        echo "unity-webgl-build: the project is open without -automated" >&2
        echo "Close it safely, then reopen it with: unity-open \"$PROJECT_PATH\"" >&2
        exit 2
    fi

    unity command eval \
        'WebGLBuildScript.Build(); return "WebGL build completed";' \
        --project-path "$PROJECT_PATH" \
        --timeout 1800
else
    unity build "$PROJECT_PATH" \
        --target WebGL \
        --execute-method WebGLBuildScript.Build \
        --output-path "$OUTPUT_PATH" \
        --log-file "$BUILD_LOG" \
        --allow-dirty-build \
        --non-interactive \
        --no-banner
fi

if [ ! -f "$OUTPUT_PATH/index.html" ]; then
    echo "unity-webgl-build: build did not produce $OUTPUT_PATH/index.html" >&2
    exit 1
fi

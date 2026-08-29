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

# shellcheck source=scripts/unity-editor-connection.sh
. "$PROJECT_PATH/scripts/unity-editor-connection.sh"

EDITOR_PID="$(connected_editor_pid "$PROJECT_PATH" || true)"
if [ -n "$EDITOR_PID" ]; then
    require_automated_editor "$EDITOR_PID" "$PROJECT_PATH" || exit 2

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

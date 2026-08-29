#!/usr/bin/env bash
# Produce the deployable WebGL player used by the host-side smoke checks.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_PATH="$PROJECT_PATH/build/WebGL"
BUILD_LOG="${TMPDIR:-/tmp}/brocoli_webgl_build.log"
STATUS_FILE="${TMPDIR:-/tmp}/brocoli_webgl_build_status.json"
REPORT_READER="$PROJECT_PATH/scripts/unity_build_report.py"
# The pipeline resolves a relative output path against the project root.
OUTPUT_RELATIVE_PATH="build/WebGL"

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

    # A player build runs far longer than the pipeline will hold the main
    # thread, so a synchronous eval reports failure while the build carries on
    # in the background. Queue it and poll for the report instead.
    unity command build \
        --target WebGL \
        --outputPath "$OUTPUT_RELATIVE_PATH" \
        --confirm true \
        --project-path "$PROJECT_PATH" \
        --timeout 120 \
        --format json >"$STATUS_FILE"

    QUEUE_STATE="$(python3 "$REPORT_READER" status "$STATUS_FILE")"
    case "$QUEUE_STATE" in
        queued | building) ;;
        *)
            echo "unity-webgl-build: build was not queued (status '$QUEUE_STATE')" >&2
            exit 1
            ;;
    esac

    BUILD_ID="$(python3 "$REPORT_READER" build-id "$STATUS_FILE")"
    if [ -z "$BUILD_ID" ]; then
        echo "unity-webgl-build: the pipeline queued a build without an id" >&2
        exit 1
    fi

    DEADLINE=$(($(date +%s) + 1800))
    while :; do
        if [ "$(date +%s)" -gt "$DEADLINE" ]; then
            echo "unity-webgl-build: timed out waiting for the player build" >&2
            exit 1
        fi

        sleep 10
        unity command build_status \
            --project-path "$PROJECT_PATH" \
            --timeout 120 \
            --format json >"$STATUS_FILE"

        BUILD_STATE="$(python3 "$REPORT_READER" status "$STATUS_FILE" "$BUILD_ID")"
        case "$BUILD_STATE" in
            completed) break ;;
            pending | queued | building) ;;
            *)
                echo "unity-webgl-build: unexpected build status '$BUILD_STATE'" >&2
                exit 1
                ;;
        esac
    done

    python3 "$REPORT_READER" summary "$STATUS_FILE"
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

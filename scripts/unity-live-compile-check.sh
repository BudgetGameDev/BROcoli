#!/usr/bin/env bash
# Recompile through the connected Unity Editor and fail on first-party warnings.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_PATH"

for tool in unity python3; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "unity-live-compile: missing '$tool'" >&2
        exit 2
    fi
done

parse_result() {
    # The response can transiently be null right after a recompile triggers a
    # domain reload; treat that as an empty result so the caller retries.
    python3 -c 'import json,sys; d=json.load(sys.stdin) or {}; data=d.get("data") or {}; r=data.get("result") or {}; print(r if isinstance(r,str) else json.dumps(r))'
}

retry_editor_command() {
    local command_name="$1"
    for _attempt in $(seq 1 30); do
        if unity command "$command_name" --timeout 5 --format json >/dev/null 2>&1; then
            return 0
        fi
        sleep 1
    done

    echo "unity-live-compile: '$command_name' remained unavailable during domain reload" >&2
    return 1
}

retry_editor_command clear_console
retry_editor_command recompile

status="compiling"
failed="false"
for _attempt in $(seq 1 120); do
    # The Pipeline endpoint can briefly disconnect while Unity reloads the
    # scripting domain. A failed poll is not a failed compilation; wait for the
    # editor connection to return and then inspect the authoritative status.
    if ! status_json="$(unity command recompile_status --format json 2>/dev/null)"; then
        sleep 1
        continue
    fi
    result="$(printf '%s' "$status_json" | parse_result)"
    status="$(printf '%s' "$result" | python3 -c 'import json,sys; print((json.load(sys.stdin) or {}).get("status","compiling"))')"
    failed="$(printf '%s' "$result" | python3 -c 'import json,sys; print(str((json.load(sys.stdin) or {}).get("failed",False)).lower())')"
    case "$status" in
        completed | up_to_date)
            break
            ;;
        compiling | triggered | idle)
            sleep 1
            ;;
        *)
            echo "unity-live-compile: unexpected status '$status'" >&2
            exit 1
            ;;
    esac
done

if [ "$status" != "completed" ] && [ "$status" != "up_to_date" ]; then
    echo "unity-live-compile: timed out waiting for compilation" >&2
    exit 1
fi
if [ "$failed" = "true" ]; then
    printf '%s' "$result" | python3 -c '
import json, sys
for error in json.load(sys.stdin).get("errors", []):
    print(error, file=sys.stderr)
'
    echo "unity-live-compile: Unity reported compiler errors" >&2
    exit 1
fi

warnings_json="$(unity command get_console_logs --severity warning --limit 1000 --format json)"
warning_count="$(printf '%s' "$warnings_json" | python3 -c '
import json, re, sys
d = json.load(sys.stdin)
data = d.get("data") or {}
result = data.get("result") or {}
logs = result.get("logs", [])
messages = [x.get("message", "") for x in logs]
warnings = [m for m in messages if re.search(r"Assets/(Scripts|Editor)/.*warning [A-Z]+[0-9]+", m)]
for warning in warnings:
    print(warning, file=sys.stderr)
print(len(warnings))
')"

if [ "$warning_count" -ne 0 ]; then
    echo "unity-live-compile: FAIL ($warning_count first-party compiler warning(s))" >&2
    exit 1
fi

echo "unity-live-compile: PASS (zero first-party compiler warnings)"

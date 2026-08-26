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
    python3 -c 'import json,sys; d=json.load(sys.stdin); r=d.get("data",{}).get("result",{}); print(r if isinstance(r,str) else json.dumps(r))'
}

unity command clear_console --format json >/dev/null
unity command recompile --format json >/dev/null

status="compiling"
failed="false"
for _attempt in $(seq 1 120); do
    result="$(unity command recompile_status --format json | parse_result)"
    status="$(printf '%s' "$result" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("status","unknown"))')"
    failed="$(printf '%s' "$result" | python3 -c 'import json,sys; print(str(json.load(sys.stdin).get("failed",False)).lower())')"
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
logs = d.get("data", {}).get("result", {}).get("logs", [])
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

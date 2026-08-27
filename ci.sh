#!/usr/bin/env bash
# One local/CI quality gate. Use --skip-unity only where a separate Unity player
# build in the same workflow supplies the authoritative compilation step.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_PATH"

SKIP_UNITY=0
if [ "${1:-}" = "--skip-unity" ]; then
    SKIP_UNITY=1
    shift
fi
if [ "$#" -ne 0 ]; then
    echo "Usage: ./ci.sh [--skip-unity]" >&2
    exit 2
fi

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "ci: missing required tool '$1'. See CONTRIBUTING.md." >&2
        exit 2
    fi
}

run_gate() {
    local name="$1"
    shift
    echo ""
    echo "==> $name"
    "$@"
}

has_connected_editor() {
    command -v unity >/dev/null 2>&1 || return 1

    unity status --format json 2>/dev/null | python3 -c '
import json, os, sys

project = os.path.realpath(os.getcwd())
document = json.load(sys.stdin)
instances = (document.get("data") or {}).get("instances") or []
connected = any(
    isinstance(instance, dict)
    and os.path.realpath(instance.get("project") or "") == project
    and instance.get("state") == "ready"
    for instance in instances
)
raise SystemExit(not connected)
'
}

require_tool dotnet
require_tool python3
require_tool node
require_tool uv
require_tool shellcheck
require_tool shfmt

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export SEMGREP_ENABLE_VERSION_CHECK=0

run_gate "Restore pinned .NET tools" dotnet tool restore
run_gate "C# formatting" dotnet csharpier check Assets/Scripts Assets/Editor
run_gate "Python lint" uvx ruff@0.12.11 check scripts
run_gate "Python formatting" uvx ruff@0.12.11 format --check scripts
run_gate "WebGL platform detection" node scripts/test-webgl-platform.cjs
run_gate "WebGL smoke probe syntax" node --check scripts/webgl-smoke.cjs
run_gate "Shell lint" shellcheck ci.sh format.sh scripts/*.sh .githooks/pre-push
run_gate "Shell formatting" shfmt -d -i 4 -ci ci.sh format.sh scripts/*.sh .githooks/pre-push
run_gate \
    "Static analysis" \
    uvx --from semgrep==1.169.0 semgrep scan \
    --config .semgrep.yml --error --strict --metrics=off \
    Assets/Scripts Assets/Editor scripts ci.sh .githooks
run_gate "Source file size" python3 scripts/check_source_size.py

if [ "$SKIP_UNITY" -eq 1 ]; then
    echo ""
    echo "==> Unity compilation: supplied by the separate Unity player-build job"
elif has_connected_editor; then
    run_gate "Unity compilation (connected Editor)" ./scripts/unity-live-compile-check.sh
else
    run_gate "Unity compilation (batch mode)" ./scripts/unity-build-check.sh
fi

echo ""
echo "ci: all gates passed"

#!/usr/bin/env bash
# Complete host-side quality gate used by the repository pre-push hook.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_PATH"

if [ "$#" -ne 0 ]; then
    echo "Usage: ./ci.sh" >&2
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

require_tool dotnet
require_tool python3
require_tool node
require_tool uv
require_tool shellcheck
require_tool shfmt
require_tool unity

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export SEMGREP_ENABLE_VERSION_CHECK=0

run_gate "Restore pinned .NET tools" dotnet tool restore
run_gate "C# formatting" dotnet csharpier check Assets/Scripts Assets/Editor Assets/Tests
run_gate "Python lint" uvx ruff@0.12.11 check scripts
run_gate "Python formatting" uvx ruff@0.12.11 format --check scripts
run_gate "Python unit tests" python3 -m unittest discover --start-directory scripts/tests --quiet
run_gate "WebGL platform detection" node scripts/test-webgl-platform.cjs
run_gate "WebGL service worker behavior" node scripts/test-webgl-service-worker.cjs
run_gate "WebGL template contract" node scripts/test-webgl-template.cjs
run_gate "WebGL smoke probe syntax" node --check scripts/webgl-smoke.cjs
run_gate "WebGL build contract syntax" node --check scripts/check-webgl-build.cjs
run_gate "Shell lint" shellcheck -x ci.sh format.sh scripts/*.sh .githooks/pre-push
run_gate "Shell formatting" shfmt -d -i 4 -ci ci.sh format.sh scripts/*.sh .githooks/pre-push
run_gate \
    "Static analysis" \
    uvx --from semgrep==1.169.0 semgrep scan \
    --config .semgrep.yml --error --strict --metrics=off \
    Assets/Scripts Assets/Editor Assets/Tests scripts ci.sh .githooks
run_gate "Source file size" python3 scripts/check_source_size.py

run_gate "Unity EditMode tests" ./scripts/unity-test-check.sh
run_gate "WebGL player build" ./scripts/unity-webgl-build.sh
run_gate "WebGL desktop smoke test" ./scripts/webgl-smoke.sh build/WebGL
run_gate \
    "WebGL iOS smoke test" \
    env WEBGL_SMOKE_PLATFORM=ios ./scripts/webgl-smoke.sh build/WebGL

echo ""
echo "ci: all gates passed"

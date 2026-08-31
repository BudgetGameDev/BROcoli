#!/usr/bin/env bash
# Run the EditMode suite under code coverage and enforce the ratchet baseline.
# Usage: ./scripts/unity-coverage-check.sh
#
# Set UNITY_EDITOR_PATH to override editor discovery.
#
# Coverage instrumentation is switched on when the Editor boots, and the
# Code Coverage package exposes no command on an already-attached Editor, so
# this gate always runs its own batch-mode Editor rather than reusing a
# connected one the way ./scripts/unity-test-check.sh does.

set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
COVERAGE_PATH="$PROJECT_PATH/build/Coverage"
LOG_FILE="${TMPDIR:-/tmp}/brocoli_coverage.log"
RESULTS_FILE="${TMPDIR:-/tmp}/brocoli_coverage_tests.xml"
VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"

# The shipping game runtime, and nothing else. Editor assemblies are authoring
# tooling rather than game code, test assemblies are the measuring instrument
# rather than the code under measurement, and everything outside
# BudgetGameDev.* belongs to Unity or a third party this project does not own.
ASSEMBLY_FILTERS='+BudgetGameDev.Shared,+BudgetGameDev.Hub,+BudgetGameDev.Games.Brocoli'

if [ "$#" -ne 0 ]; then
    echo "Usage: ./scripts/unity-coverage-check.sh" >&2
    exit 2
fi

if [ ! -f "$VERSION_FILE" ]; then
    echo "unity-coverage: missing Unity version file: $VERSION_FILE" >&2
    exit 1
fi

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$VERSION_FILE" | head -1)"
if [ -z "$UNITY_VERSION" ]; then
    echo "unity-coverage: could not read m_EditorVersion from $VERSION_FILE" >&2
    exit 1
fi

# Any Editor holding the project lock makes the batch-mode Editor this gate
# needs exit without writing coverage, reporting only that a second instance
# cannot open the project. An Editor that never installed the Pipeline package
# is invisible to `unity status` yet still holds that lock, so match on the
# process itself. AssetImportWorker children carry -batchMode and are not it.
holding_editor_pid() {
    local pid arguments
    for pid in $(pgrep -f 'Unity\.app/Contents/MacOS/Unity' || true); do
        arguments="$(ps -p "$pid" -ww -o args= 2>/dev/null || true)"
        case "$arguments" in
            *-batchMode* | *AssetImportWorker*) continue ;;
            *"-projectPath $PROJECT_PATH"*)
                printf '%s' "$pid"
                return 0
                ;;
        esac
    done
}

EDITOR_PID="$(holding_editor_pid || true)"
if [ -n "$EDITOR_PID" ]; then
    echo "unity-coverage: a Unity Editor (pid $EDITOR_PID) holds this project." >&2
    echo "Coverage instrumentation needs its own batch-mode Editor, which cannot" >&2
    echo "open a locked project. Close that Editor, then run this gate again." >&2
    exit 2
fi

detect_unity_path() {
    if [ -n "${UNITY_EDITOR_PATH:-}" ]; then
        return
    fi

    local candidate
    case "$(uname -s)" in
        Darwin*)
            for candidate in \
                "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" \
                "$HOME/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"; do
                if [ -x "$candidate" ]; then
                    UNITY_EDITOR_PATH="$candidate"
                    return
                fi
            done
            ;;
        Linux*)
            candidate="$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity"
            [ -x "$candidate" ] && UNITY_EDITOR_PATH="$candidate"
            ;;
        MINGW* | MSYS* | CYGWIN*)
            candidate="/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity.exe"
            [ -x "$candidate" ] && UNITY_EDITOR_PATH="$candidate"
            ;;
        *)
            echo "unity-coverage: unknown OS: $(uname -s)" >&2
            exit 1
            ;;
    esac

    return 0
}

detect_unity_path

if [ -z "${UNITY_EDITOR_PATH:-}" ] || [ ! -x "${UNITY_EDITOR_PATH:-}" ]; then
    echo "unity-coverage: Unity $UNITY_VERSION was not found." >&2
    echo "Install it via Unity Hub, or set UNITY_EDITOR_PATH." >&2
    exit 1
fi

echo "📊 Unity EditMode Coverage"
echo "=========================="
echo "Project: $PROJECT_PATH"
echo "Unity:   $UNITY_EDITOR_PATH"
echo ""

# Never let a stale report satisfy the gate after a failed run.
rm -rf "$COVERAGE_PATH"
rm -f "$RESULTS_FILE"
: >"$LOG_FILE"

# -debugCodeOptimization keeps the assemblies unoptimised, without which the
# sequence points coverage is counted from do not survive compilation.
set +e
"$UNITY_EDITOR_PATH" \
    -batchmode \
    -projectPath "$PROJECT_PATH" \
    -runTests \
    -testPlatform EditMode \
    -testResults "$RESULTS_FILE" \
    -enableCodeCoverage \
    -debugCodeOptimization \
    -coverageResultsPath "$COVERAGE_PATH" \
    -coverageOptions "generateAdditionalMetrics;assemblyFilters:$ASSEMBLY_FILTERS" \
    -logFile "$LOG_FILE" 2>&1
EXIT_CODE=$?
set -e

echo ""
echo "=========================="

if [ ! -f "$RESULTS_FILE" ]; then
    echo "unity-coverage: TEST RUN FAILED (Unity exit $EXIT_CODE, no results written)" >&2
    tail -40 "$LOG_FILE" 2>/dev/null || true
    echo "" >&2
    echo "Full log: $LOG_FILE" >&2
    exit 1
fi

python3 - "$RESULTS_FILE" <<'PY'
import sys
import xml.etree.ElementTree as ElementTree

root = ElementTree.parse(sys.argv[1]).getroot()
failed = int(root.get("failed", "0") or 0)
print(
    f"{root.get('passed', '0')}/{root.get('total', '0')} passed, "
    f"{failed} failed, {root.get('skipped', '0')} skipped"
)
raise SystemExit(1 if failed else 0)
PY

exec python3 "$PROJECT_PATH/scripts/check_coverage.py" "$COVERAGE_PATH"

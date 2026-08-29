#!/usr/bin/env bash
# Run the Unity EditMode test suite in batch mode and fail on any failure.
# Usage: ./scripts/unity-test-check.sh
#
# Set UNITY_EDITOR_PATH to override editor discovery.

set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
LOG_FILE="/tmp/unity_test_check.log"
RESULTS_FILE="/tmp/unity_test_results.xml"
VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"

if [ ! -f "$VERSION_FILE" ]; then
    echo "❌ Missing Unity version file: $VERSION_FILE"
    exit 1
fi

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$VERSION_FILE" | head -1)"
if [ -z "$UNITY_VERSION" ]; then
    echo "❌ Could not read m_EditorVersion from: $VERSION_FILE"
    exit 1
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
            echo "❌ Unknown OS: $(uname -s)"
            exit 1
            ;;
    esac

    return 0
}

detect_unity_path

if [ -z "${UNITY_EDITOR_PATH:-}" ] || [ ! -x "$UNITY_EDITOR_PATH" ]; then
    echo "❌ Unity $UNITY_VERSION was not found."
    echo ""
    echo "Please either:"
    echo "  1. Install Unity $UNITY_VERSION via Unity Hub"
    echo "  2. Set UNITY_EDITOR_PATH to the Unity editor executable"
    exit 1
fi

echo "🧪 Unity EditMode Tests"
echo "======================="
echo "Project: $PROJECT_PATH"
echo "Unity: $UNITY_EDITOR_PATH"
echo ""

# -runTests exits on its own once the run finishes; -quit would race it.
: >"$LOG_FILE"
rm -f "$RESULTS_FILE"
set +e
"$UNITY_EDITOR_PATH" \
    -batchmode \
    -projectPath "$PROJECT_PATH" \
    -runTests \
    -testPlatform EditMode \
    -testResults "$RESULTS_FILE" \
    -logFile "$LOG_FILE" 2>&1
EXIT_CODE=$?
set -e

echo ""
echo "======================="

if [ ! -f "$RESULTS_FILE" ]; then
    echo "❌ TEST RUN FAILED (Unity exit code $EXIT_CODE, no results written)"
    tail -40 "$LOG_FILE" 2>/dev/null || true
    echo ""
    echo "Full log: $LOG_FILE"
    exit 1
fi

python3 - "$RESULTS_FILE" <<'PY'
import sys
import xml.etree.ElementTree as ElementTree

root = ElementTree.parse(sys.argv[1]).getroot()
total = root.get("total", "0")
passed = root.get("passed", "0")
failed = int(root.get("failed", "0") or 0)
skipped = root.get("skipped", "0")
print(f"{passed}/{total} passed, {failed} failed, {skipped} skipped")

if failed:
    for case in root.iter("test-case"):
        if case.get("result") != "Failed":
            continue
        print(f"\n❌ {case.get('fullname')}")
        message = case.find("./failure/message")
        if message is not None and message.text:
            print("   " + message.text.strip().replace("\n", "\n   ")[:800])

raise SystemExit(1 if failed else 0)
PY

echo ""
echo "✅ EDITMODE TESTS PASSED"

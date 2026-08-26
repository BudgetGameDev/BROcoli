#!/bin/bash
# Unity batch-mode compilation verification script
# Usage: ./scripts/unity-build-check.sh
#
# Cross-platform Unity package resolution, asset import, and compilation check.
# This does not produce a player build; CI performs the full WebGL build.
# Set UNITY_EDITOR_PATH to override editor discovery.

set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
LOG_FILE="/tmp/unity_build_check.log"
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

echo "🔧 Unity Compilation Check"
echo "=========================="
echo "Project: $PROJECT_PATH"
echo "Version: $UNITY_VERSION"
echo ""

# Detect OS and set Unity path
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

# Check if Unity exists
if [ -z "${UNITY_EDITOR_PATH:-}" ] || [ ! -x "$UNITY_EDITOR_PATH" ]; then
    echo "❌ Unity $UNITY_VERSION was not found."
    echo ""
    echo "Please either:"
    echo "  1. Install Unity $UNITY_VERSION via Unity Hub"
    echo "  2. Set UNITY_EDITOR_PATH to the Unity editor executable"
    exit 1
fi

echo "OS: $(uname -s)"
echo "Unity: $UNITY_EDITOR_PATH"
echo ""
echo "⏳ Running Unity batch mode compilation..."
echo "   (This may take 1-3 minutes on first run, 3-5 minutes after clean)"
echo ""

# Run Unity in batch mode
: >"$LOG_FILE"
set +e
"$UNITY_EDITOR_PATH" \
    -batchmode \
    -projectPath "$PROJECT_PATH" \
    -buildTarget WebGL \
    -logFile "$LOG_FILE" \
    -quit 2>&1
EXIT_CODE=$?
set -e

echo ""
echo "=========================="

# Check for success
if [ "$EXIT_CODE" -eq 0 ] && grep -q "Exiting batchmode successfully" "$LOG_FILE"; then
    # Assets/csc.rsp promotes compiler warnings to errors. Keep this log scan as a
    # safeguard for first-party assemblies that may override compiler arguments.
    WARNINGS="$(grep -Ec 'Assets/(Scripts|Editor)/.*warning [A-Z]+[0-9]+' "$LOG_FILE" 2>/dev/null || true)"
    if [ "$WARNINGS" -gt 0 ]; then
        echo "❌ COMPILATION FAILED ($WARNINGS first-party warning(s))"
        grep -E 'Assets/(Scripts|Editor)/.*warning [A-Z]+[0-9]+' "$LOG_FILE" | head -20 || true
        echo ""
        echo "Warnings are treated as errors by the repository CI gate."
        echo "Full log: $LOG_FILE"
        exit 1
    fi

    echo "✅ COMPILATION SUCCEEDED (zero first-party warnings)"
    echo ""

    # Show compiled assemblies
    echo "Compiled assemblies:"
    find "$PROJECT_PATH/Library/ScriptAssemblies" -maxdepth 1 -type f \
        -name 'Assembly-CSharp*' -print 2>/dev/null | head -4 || true

    exit 0
else
    echo "❌ COMPILATION FAILED (Unity exit code $EXIT_CODE)"
    echo ""

    # Check if errors are in our code or package cache
    OUR_ERRORS="$(grep -Ec 'Assets/(Scripts|Editor)/.*error [A-Z]+[0-9]+' "$LOG_FILE" 2>/dev/null || true)"
    PKG_ERRORS="$(grep -c "Library/PackageCache.*error CS" "$LOG_FILE" 2>/dev/null || true)"

    if [ "$OUR_ERRORS" -gt 0 ]; then
        echo "❌ Errors in first-party code:"
        grep -E 'Assets/(Scripts|Editor)/.*error [A-Z]+[0-9]+' "$LOG_FILE" | head -20 || true
        echo ""
        echo "Fix these errors and try again."
    fi

    if [ "$PKG_ERRORS" -gt 0 ] && [ "$OUR_ERRORS" -eq 0 ]; then
        echo "❌ Errors in a resolved Unity package:"
        echo "   Check API compatibility against Packages/packages-lock.json first."
        echo ""
        echo "   If the pinned package cache is demonstrably corrupt, remove Library/"
        echo "   and rerun this check. Preserve Packages/packages-lock.json."
        echo ""
        grep "Library/PackageCache.*error CS" "$LOG_FILE" | head -5 || true
    fi

    echo ""
    echo "Full log: $LOG_FILE"
    exit 1
fi

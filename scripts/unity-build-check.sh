#!/bin/bash
# Unity CLI Build Verification Script
# Usage: ./scripts/unity-build-check.sh
# 
# Cross-platform Unity batch mode compilation check.
# Detects OS and Unity installation automatically.

set -e

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
LOG_FILE="/tmp/unity_build_check.log"

echo "🔧 Unity Build Check"
echo "===================="
echo "Project: $PROJECT_PATH"
echo ""

# Detect OS and set Unity path
detect_unity_path() {
    local version="6000.3.6f1"  # Update this to match your Unity version
    
    case "$(uname -s)" in
        Darwin*)
            # macOS
            UNITY_PATH="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
            ;;
        Linux*)
            # Linux
            UNITY_PATH="$HOME/Unity/Hub/Editor/$version/Editor/Unity"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            # Windows (Git Bash / MSYS2)
            UNITY_PATH="/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
            ;;
        *)
            echo "❌ Unknown OS: $(uname -s)"
            exit 1
            ;;
    esac
    
    echo "OS: $(uname -s)"
    echo "Unity: $UNITY_PATH"
}

detect_unity_path

# Check if Unity exists
if [ ! -f "$UNITY_PATH" ]; then
    echo "❌ Unity not found at: $UNITY_PATH"
    echo ""
    echo "Please either:"
    echo "  1. Install Unity $version via Unity Hub"
    echo "  2. Update the 'version' variable in this script to match your installed version"
    echo ""
    echo "Installed versions:"
    case "$(uname -s)" in
        Darwin*)
            ls /Applications/Unity/Hub/Editor/ 2>/dev/null || echo "  (none found)"
            ;;
        Linux*)
            ls ~/Unity/Hub/Editor/ 2>/dev/null || echo "  (none found)"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            ls "/c/Program Files/Unity/Hub/Editor/" 2>/dev/null || echo "  (none found)"
            ;;
    esac
    exit 1
fi

echo ""
echo "⏳ Running Unity batch mode compilation..."
echo "   (This may take 1-3 minutes on first run, 3-5 minutes after clean)"
echo ""

# Run Unity in batch mode
"$UNITY_PATH" \
    -batchmode \
    -projectPath "$PROJECT_PATH" \
    -buildTarget WebGL \
    -logFile "$LOG_FILE" \
    -quit 2>&1

EXIT_CODE=$?

echo ""
echo "===================="

# Check for success
if grep -q "Exiting batchmode successfully" "$LOG_FILE"; then
    echo "✅ BUILD SUCCEEDED"
    echo ""
    
    # Check for warnings in our code (not package cache)
    WARNINGS=$(grep "Assets/Scripts.*warning CS" "$LOG_FILE" 2>/dev/null | wc -l | tr -d ' ')
    if [ "$WARNINGS" -gt 0 ]; then
        echo "⚠️  $WARNINGS warning(s) in Assets/Scripts:"
        grep "Assets/Scripts.*warning CS" "$LOG_FILE" | head -10
        echo ""
    fi
    
    # Show compiled assemblies
    echo "Compiled assemblies:"
    ls -la "$PROJECT_PATH/Library/ScriptAssemblies/Assembly-CSharp"* 2>/dev/null | head -4
    
    exit 0
else
    echo "❌ BUILD FAILED"
    echo ""
    
    # Check if errors are in our code or package cache
    OUR_ERRORS=$(grep "Assets/Scripts.*error CS" "$LOG_FILE" 2>/dev/null | wc -l | tr -d ' ')
    PKG_ERRORS=$(grep "Library/PackageCache.*error CS" "$LOG_FILE" 2>/dev/null | wc -l | tr -d ' ')
    
    if [ "$OUR_ERRORS" -gt 0 ]; then
        echo "❌ Errors in YOUR code (Assets/Scripts/):"
        grep "Assets/Scripts.*error CS" "$LOG_FILE" | head -20
        echo ""
        echo "Fix these errors and try again."
    fi
    
    if [ "$PKG_ERRORS" -gt 0 ] && [ "$OUR_ERRORS" -eq 0 ]; then
        echo "⚠️  Errors in Package Cache (not your code):"
        echo "   This usually indicates a corrupted package cache."
        echo ""
        echo "   Run a clean rebuild:"
        echo "   $ rm -rf Library/ Packages/packages-lock.json"
        echo "   $ ./scripts/unity-build-check.sh"
        echo ""
        grep "Library/PackageCache.*error CS" "$LOG_FILE" | head -5
    fi
    
    echo ""
    echo "Full log: $LOG_FILE"
    exit 1
fi

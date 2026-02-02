#!/bin/bash
# Unity Clean Rebuild Script
# Usage: ./scripts/unity-clean.sh
#
# Safely cleans Unity's Library folder to force a fresh rebuild.
# Includes safety guardrails to prevent accidental deletion of system folders.

set -e

# Get project path from script location (safe - doesn't rely on pwd)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_PATH="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "🧹 Unity Clean Script"
echo "====================="
echo ""

# SAFETY CHECK 1: Verify we're in a Unity project
if [ ! -f "$PROJECT_PATH/Packages/manifest.json" ]; then
    echo "❌ SAFETY CHECK FAILED: Not a Unity project!"
    echo "   Expected to find: $PROJECT_PATH/Packages/manifest.json"
    echo "   Aborting to prevent accidental deletion."
    exit 1
fi

# SAFETY CHECK 2: Verify Library folder exists where expected
if [ ! -d "$PROJECT_PATH/Library" ] && [ ! -d "$PROJECT_PATH/Temp" ]; then
    echo "⚠️  No Library/ or Temp/ folder found - project may already be clean."
    echo "   Path: $PROJECT_PATH"
    exit 0
fi

# SAFETY CHECK 3: Never operate outside project directory
cd "$PROJECT_PATH"
CURRENT_DIR="$(pwd)"
if [ "$CURRENT_DIR" != "$PROJECT_PATH" ]; then
    echo "❌ SAFETY CHECK FAILED: Could not change to project directory!"
    exit 1
fi

echo "Project: $PROJECT_PATH"
echo ""

# Show what will be deleted
echo "📁 Folders to delete:"
[ -d "Library" ] && echo "   - Library/ ($(du -sh Library 2>/dev/null | cut -f1 || echo 'unknown size'))"
[ -d "Temp" ] && echo "   - Temp/"
[ -f "Packages/packages-lock.json" ] && echo "   - Packages/packages-lock.json"
echo ""

# Confirm with user (unless --force flag)
if [ "$1" != "--force" ] && [ "$1" != "-f" ]; then
    read -p "Continue with clean? [y/N] " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Aborted."
        exit 0
    fi
fi

echo ""
echo "🗑️  Cleaning..."

# Delete using relative paths ONLY (safety)
[ -d "Library" ] && rm -rf Library/ && echo "   ✓ Deleted Library/"
[ -d "Temp" ] && rm -rf Temp/ && echo "   ✓ Deleted Temp/"
[ -f "Packages/packages-lock.json" ] && rm -f Packages/packages-lock.json && echo "   ✓ Deleted packages-lock.json"

echo ""
echo "✅ Clean complete!"
echo ""
echo "Next steps:"
echo "  1. Run: ./scripts/unity-build-check.sh"
echo "  2. First rebuild will take 2-5 minutes (downloading packages)"

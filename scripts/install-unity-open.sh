#!/usr/bin/env bash
# Put scripts/unity-open.sh on PATH as the `unity-open` command AGENTS.md requires.
# Usage: ./scripts/install-unity-open.sh [--force] [bin-directory]
set -euo pipefail

SCRIPT_DIRECTORY="$(cd "$(dirname "$0")" && pwd -P)"
SOURCE_PATH="$SCRIPT_DIRECTORY/unity-open.sh"
BIN_DIRECTORY="${UNITY_OPEN_BIN_DIR:-$HOME/.local/bin}"
FORCE=0

for argument in "$@"; do
    case "$argument" in
        --force)
            FORCE=1
            ;;
        -*)
            echo "Usage: ./scripts/install-unity-open.sh [--force] [bin-directory]" >&2
            exit 2
            ;;
        *)
            BIN_DIRECTORY="$argument"
            ;;
    esac
done

mkdir -p "$BIN_DIRECTORY"
BIN_DIRECTORY="$(cd "$BIN_DIRECTORY" && pwd -P)"
TARGET_PATH="$BIN_DIRECTORY/unity-open"

# A hand-written command already on PATH may be someone's own tool, so replacing
# it is an explicit choice rather than a side effect of running the installer.
if [ -e "$TARGET_PATH" ] || [ -L "$TARGET_PATH" ]; then
    EXISTING_PATH="$(python3 -c 'import os, sys; print(os.path.realpath(sys.argv[1]))' "$TARGET_PATH")"
    if [ "$EXISTING_PATH" != "$SOURCE_PATH" ] && [ "$FORCE" -eq 0 ]; then
        echo "unity-open: $TARGET_PATH already exists and points at $EXISTING_PATH" >&2
        echo "Re-run with --force to replace it." >&2
        exit 3
    fi
fi

ln -sfn "$SOURCE_PATH" "$TARGET_PATH"
echo "unity-open installed: $TARGET_PATH -> $SOURCE_PATH"

case ":$PATH:" in
    *":$BIN_DIRECTORY:"*) ;;
    *)
        echo "Add $BIN_DIRECTORY to PATH to run 'unity-open' by name." >&2
        ;;
esac

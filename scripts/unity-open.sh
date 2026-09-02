#!/usr/bin/env bash
# Open this Unity project in the automated mode required by repository tooling.
# Usage: ./scripts/unity-open.sh [project-path]
set -euo pipefail

# The installer puts this script on PATH as a symlink, so follow the link back
# to the clone rather than trusting the directory the command was invoked from.
SCRIPT_PATH="$(python3 -c 'import os, sys; print(os.path.realpath(sys.argv[1]))' "${BASH_SOURCE[0]}")"
SCRIPT_DIRECTORY="$(cd "$(dirname "$SCRIPT_PATH")" && pwd -P)"

if [ "$#" -gt 1 ]; then
    echo "Usage: ./scripts/unity-open.sh [project-path]" >&2
    exit 2
fi

PROJECT_PATH="${1:-$SCRIPT_DIRECTORY/..}"

if ! command -v unity >/dev/null 2>&1; then
    echo "unity-open: Unity CLI is required" >&2
    exit 2
fi

if [ ! -f "$PROJECT_PATH/Packages/manifest.json" ]; then
    echo "unity-open: not a Unity project: $PROJECT_PATH" >&2
    exit 2
fi

PROJECT_PATH="$(cd "$PROJECT_PATH" && pwd -P)"

# shellcheck source=scripts/unity-editor-connection.sh
. "$SCRIPT_DIRECTORY/unity-editor-connection.sh"

EDITOR_PID="$(running_editor_pid "$PROJECT_PATH")"
if [ -n "$EDITOR_PID" ]; then
    if is_automated_editor "$EDITOR_PID"; then
        echo "unity-open: automated Editor is already ready (PID $EDITOR_PID)"
        exit 0
    fi

    echo "unity-open: the project is already open without -automated (PID $EDITOR_PID)" >&2
    echo "Close it safely, then run this command again." >&2
    exit 2
fi

echo "unity-open: opening $PROJECT_PATH in automated mode"
unity open "$PROJECT_PATH" --args "-automated"

#!/usr/bin/env bash
# Shared discovery for an automated Unity Editor already attached to a project.
# Source this file; it defines connected_editor_pid, running_editor_pid,
# is_automated_editor and require_automated_editor.

# Prints the PID of a ready Editor holding this project, or nothing.
connected_editor_pid() {
    local project_path="$1"

    unity status --project-path "$project_path" --format json 2>/dev/null | python3 -c '
import json, os, sys

project = os.path.realpath(sys.argv[1])
document = json.load(sys.stdin)
instances = (document.get("data") or {}).get("instances") or []
for instance in instances:
    if (
        isinstance(instance, dict)
        and os.path.realpath(instance.get("project") or "") == project
        and instance.get("state") == "ready"
    ):
        print(instance.get("pid") or "")
        break
' "$project_path"
}

# Prints the PID of any Editor holding this project, ready or still booting.
running_editor_pid() {
    local project_path="$1"
    local ready_pid

    ready_pid="$(connected_editor_pid "$project_path")"
    if [ -n "$ready_pid" ]; then
        echo "$ready_pid"
        return 0
    fi

    unity pipeline list --format json 2>/dev/null | python3 -c '
import json, os, sys

project = os.path.realpath(sys.argv[1])
document = json.load(sys.stdin)
instances = (document.get("data") or {}).get("instances") or []
for instance in instances:
    if (
        isinstance(instance, dict)
        and instance.get("isRunning")
        and os.path.realpath(instance.get("projectPath") or "") == project
    ):
        print(instance.get("pid") or "")
        break
' "$project_path"
}

# True when the Editor was launched with -automated, so tooling may drive it.
is_automated_editor() {
    local editor_pid="$1"

    ps -p "$editor_pid" -ww -o args= 2>/dev/null |
        grep -Eq -- '(^|[[:space:]])-automated([[:space:]]|$)'
}

# An Editor opened by hand cannot be driven safely, so stop rather than fight it.
require_automated_editor() {
    local editor_pid="$1"
    local project_path="$2"

    if ! is_automated_editor "$editor_pid"; then
        echo "unity: the project is open without -automated" >&2
        echo "Close it safely, then reopen it with: unity-open \"$project_path\"" >&2
        return 1
    fi
}

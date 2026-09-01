#!/usr/bin/env bash
# Manually build and package Windows, macOS, and Linux desktop players.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
NATIVE_ROOT="$PROJECT_PATH/build/native"
PLAYERS_ROOT="$NATIVE_ROOT/players"
ARTIFACTS_ROOT="$NATIVE_ROOT/artifacts"
BUILD_LOG="$NATIVE_ROOT/native-build.log"
DEVELOPMENT=0
REQUESTED_TARGETS="windows,macos,linux"

usage() {
    cat <<'EOF'
Usage: ./scripts/native-builds.sh [--targets <list>] [--development]

Build and package native desktop players. This is a manual release tool;
ci.sh and cd.sh do not invoke it.

Options:
    --targets <list>  Comma-separated subset of windows,macos,linux.
                      Defaults to all three.
    --development     Produce Unity development players.
    -h, --help        Show this help.
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --development) DEVELOPMENT=1 ;;
        --targets)
            if [ "$#" -lt 2 ]; then
                echo "native-builds: '--targets' requires a value" >&2
                exit 2
            fi
            REQUESTED_TARGETS="$2"
            shift
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "native-builds: unknown argument '$1'" >&2
            usage >&2
            exit 2
            ;;
    esac
    shift
done

TARGETS=()
IFS=',' read -r -a REQUESTED <<<"$REQUESTED_TARGETS"
for requested in ${REQUESTED[@]+"${REQUESTED[@]}"}; do
    case "$requested" in
        windows | macos | linux) ;;
        *)
            echo "native-builds: unknown target '$requested'" >&2
            exit 2
            ;;
    esac
    for selected in ${TARGETS[@]+"${TARGETS[@]}"}; do
        if [ "$selected" = "$requested" ]; then
            continue 2
        fi
    done
    TARGETS+=("$requested")
done
if [ "${#TARGETS[@]}" -eq 0 ]; then
    echo "native-builds: --targets selected no players" >&2
    exit 2
fi

has_target() {
    local candidate
    for candidate in "${TARGETS[@]}"; do
        if [ "$candidate" = "$1" ]; then
            return 0
        fi
    done
    return 1
}

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "native-builds: missing required tool '$1'" >&2
        exit 2
    fi
}

if has_target macos && [ "$(uname -s)" != "Darwin" ]; then
    echo "native-builds: building the macOS player requires a macOS host" >&2
    exit 2
fi

require_tool unity
require_tool git
require_tool shasum
if has_target windows; then
    require_tool zip
fi
if has_target macos; then
    require_tool ditto
fi
if has_target linux; then
    require_tool tar
fi

# shellcheck source=scripts/unity-editor-connection.sh
. "$PROJECT_PATH/scripts/unity-editor-connection.sh"

EDITOR_PID="$(connected_editor_pid "$PROJECT_PATH" || true)"
if [ -n "$EDITOR_PID" ]; then
    echo "native-builds: Unity currently has this project open (PID $EDITOR_PID)" >&2
    echo "Close it safely before running the batch build." >&2
    exit 2
fi

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" | head -1)"
if [ -z "$UNITY_VERSION" ]; then
    echo "native-builds: could not read the Unity editor version" >&2
    exit 2
fi

case "${TARGETS[0]}" in
    windows) INITIAL_BUILD_TARGET="StandaloneWindows64" ;;
    macos) INITIAL_BUILD_TARGET="StandaloneOSX" ;;
    linux) INITIAL_BUILD_TARGET="StandaloneLinux64" ;;
esac
SELECTED_TARGETS="$(
    IFS=,
    printf '%s' "${TARGETS[*]}"
)"

# Platform switches can rewrite serialized editor preferences even though they
# are not build inputs. Preserve the exact checked-out settings on success,
# failure, or interruption so this manual tool never dirties the source tree.
SETTINGS_BACKUP="$(mktemp -d "${TMPDIR:-/tmp}/brocoli-native-settings.XXXXXX")"
cp "$PROJECT_PATH/ProjectSettings/ProjectSettings.asset" "$SETTINGS_BACKUP/"
cp "$PROJECT_PATH/ProjectSettings/QualitySettings.asset" "$SETTINGS_BACKUP/"
restore_project_settings() {
    cp "$SETTINGS_BACKUP/ProjectSettings.asset" "$PROJECT_PATH/ProjectSettings/"
    cp "$SETTINGS_BACKUP/QualitySettings.asset" "$PROJECT_PATH/ProjectSettings/"
    rm -rf -- "$SETTINGS_BACKUP"
}
trap restore_project_settings EXIT

# These paths are intentionally fixed and narrow: stale native players must not
# survive a failed build and get packaged as if they were current. Clearing the
# whole tree also keeps a partial selection from publishing another target's
# leftovers.
rm -rf -- "$PLAYERS_ROOT" "$ARTIFACTS_ROOT"
mkdir -p "$PLAYERS_ROOT" "$ARTIFACTS_ROOT"

UNITY_ARGUMENTS=(
    run
    "$PROJECT_PATH"
    --editor-version "$UNITY_VERSION"
    --timeout 7200
    --non-interactive
    --no-banner
    --
    -buildTarget "$INITIAL_BUILD_TARGET"
    -executeMethod NativePlayerBuildScript.BuildAll
    -buildOutput "$PLAYERS_ROOT"
    -buildTargets "$SELECTED_TARGETS"
    -logFile "$BUILD_LOG"
)
if [ "$DEVELOPMENT" -eq 1 ]; then
    UNITY_ARGUMENTS+=(-development)
fi

echo "native-builds: building ${SELECTED_TARGETS//,/, }"
unity "${UNITY_ARGUMENTS[@]}"
restore_project_settings
trap - EXIT

require_player() {
    if [ ! -e "$1" ]; then
        echo "native-builds: expected player was not produced: $1" >&2
        exit 1
    fi
}

ARCHIVES=()
if has_target windows; then
    require_player "$PLAYERS_ROOT/windows/BROcoli.exe"
    (
        cd "$PLAYERS_ROOT/windows"
        zip -qry "$ARTIFACTS_ROOT/BROcoli-windows-x86_64.zip" .
    )
    ARCHIVES+=("BROcoli-windows-x86_64.zip")
fi
if has_target macos; then
    require_player "$PLAYERS_ROOT/macos/BROcoli.app"
    ditto -c -k --sequesterRsrc --keepParent \
        "$PLAYERS_ROOT/macos/BROcoli.app" "$ARTIFACTS_ROOT/BROcoli-macos-universal.zip"
    ARCHIVES+=("BROcoli-macos-universal.zip")
fi
if has_target linux; then
    require_player "$PLAYERS_ROOT/linux/BROcoli.x86_64"
    tar -C "$PLAYERS_ROOT/linux" -czf "$ARTIFACTS_ROOT/BROcoli-linux-x86_64.tar.gz" .
    ARCHIVES+=("BROcoli-linux-x86_64.tar.gz")
fi

COMMIT_SHA="$(git -C "$PROJECT_PATH" rev-parse HEAD)"
if [ -n "$(git -C "$PROJECT_PATH" status --porcelain)" ]; then
    DIRTY=true
else
    DIRTY=false
fi
if [ "$DEVELOPMENT" -eq 1 ]; then
    DEVELOPMENT_BUILD=true
else
    DEVELOPMENT_BUILD=false
fi

cat >"$ARTIFACTS_ROOT/build-info.txt" <<EOF
commit=$COMMIT_SHA
unity=$UNITY_VERSION
targets=$SELECTED_TARGETS
development=$DEVELOPMENT_BUILD
dirty=$DIRTY
built_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF

(
    cd "$ARTIFACTS_ROOT"
    shasum -a 256 "${ARCHIVES[@]}" build-info.txt >SHA256SUMS
)

echo ""
echo "native-builds: packaged release artifacts in $ARTIFACTS_ROOT"
(
    cd "$ARTIFACTS_ROOT"
    du -h "${ARCHIVES[@]}"
)

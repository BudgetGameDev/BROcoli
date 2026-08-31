#!/usr/bin/env bash
# Manually build and package Windows, macOS, and Linux desktop players.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
NATIVE_ROOT="$PROJECT_PATH/build/native"
PLAYERS_ROOT="$NATIVE_ROOT/players"
ARTIFACTS_ROOT="$NATIVE_ROOT/artifacts"
BUILD_LOG="$NATIVE_ROOT/native-build.log"
DEVELOPMENT=0

usage() {
    cat <<'EOF'
Usage: ./scripts/native-builds.sh [--development]

Build and package all native desktop players. This is a manual release tool;
ci.sh and cd.sh do not invoke it.

Options:
    --development  Produce Unity development players.
    -h, --help     Show this help.
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --development) DEVELOPMENT=1 ;;
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

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "native-builds: missing required tool '$1'" >&2
        exit 2
    fi
}

if [ "$(uname -s)" != "Darwin" ]; then
    echo "native-builds: building all three targets requires a macOS host" >&2
    exit 2
fi

require_tool unity
require_tool python3
require_tool git
require_tool ditto
require_tool zip
require_tool tar
require_tool shasum

# shellcheck source=scripts/unity-editor-connection.sh
. "$PROJECT_PATH/scripts/unity-editor-connection.sh"

EDITOR_PID="$(connected_editor_pid "$PROJECT_PATH" || true)"
if [ -n "$EDITOR_PID" ]; then
    echo "native-builds: Unity currently has this project open (PID $EDITOR_PID)" >&2
    echo "Close it safely before running the multi-target batch build." >&2
    exit 2
fi

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" | head -1)"
if [ -z "$UNITY_VERSION" ]; then
    echo "native-builds: could not read the Unity editor version" >&2
    exit 2
fi

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
# survive a failed build and get packaged as if they were current.
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
    -buildTarget StandaloneWindows64
    -executeMethod NativePlayerBuildScript.BuildAll
    -buildOutput "$PLAYERS_ROOT"
    -logFile "$BUILD_LOG"
)
if [ "$DEVELOPMENT" -eq 1 ]; then
    UNITY_ARGUMENTS+=(-development)
fi

echo "native-builds: building Windows, macOS, and Linux players"
unity "${UNITY_ARGUMENTS[@]}"
restore_project_settings
trap - EXIT

WINDOWS_PLAYER="$PLAYERS_ROOT/windows/BROcoli.exe"
MACOS_PLAYER="$PLAYERS_ROOT/macos/BROcoli.app"
LINUX_PLAYER="$PLAYERS_ROOT/linux/BROcoli.x86_64"
for player in "$WINDOWS_PLAYER" "$MACOS_PLAYER" "$LINUX_PLAYER"; do
    if [ ! -e "$player" ]; then
        echo "native-builds: expected player was not produced: $player" >&2
        exit 1
    fi
done

WINDOWS_ARCHIVE="$ARTIFACTS_ROOT/BROcoli-windows-x86_64.zip"
MACOS_ARCHIVE="$ARTIFACTS_ROOT/BROcoli-macos-universal.zip"
LINUX_ARCHIVE="$ARTIFACTS_ROOT/BROcoli-linux-x86_64.tar.gz"

(
    cd "$PLAYERS_ROOT/windows"
    zip -qry "$WINDOWS_ARCHIVE" .
)
ditto -c -k --sequesterRsrc --keepParent "$MACOS_PLAYER" "$MACOS_ARCHIVE"
tar -C "$PLAYERS_ROOT/linux" -czf "$LINUX_ARCHIVE" .

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
development=$DEVELOPMENT_BUILD
dirty=$DIRTY
built_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF

(
    cd "$ARTIFACTS_ROOT"
    shasum -a 256 \
        BROcoli-windows-x86_64.zip \
        BROcoli-macos-universal.zip \
        BROcoli-linux-x86_64.tar.gz \
        build-info.txt >SHA256SUMS
)

echo ""
echo "native-builds: packaged release artifacts in $ARTIFACTS_ROOT"
du -h "$WINDOWS_ARCHIVE" "$MACOS_ARCHIVE" "$LINUX_ARCHIVE"

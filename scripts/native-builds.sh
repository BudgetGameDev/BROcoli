#!/usr/bin/env bash
# Manually build and package Windows, macOS, and Linux desktop players.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
PRODUCT=""
DEVELOPMENT=0
RENDER_PIPELINE="urp"
REQUESTED_TARGETS="windows,macos,linux"

usage() {
    cat <<'EOF'
Usage: ./scripts/native-builds.sh --product <game|launcher> [--targets <list>] [--pipeline urp|hdrp] [--development]

Build and package native desktop players. This is a manual release tool;
ci.sh and cd.sh do not invoke it.

Options:
    --product <id>    Game package suffix or launcher (required).
    --targets <list>  Comma-separated subset of windows,macos,linux.
                      Defaults to all three.
    --pipeline <name>  Render pipeline: urp (default) or hdrp.
    --development     Produce Unity development players.
    -h, --help        Show this help.
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --development) DEVELOPMENT=1 ;;
        --product)
            PRODUCT="${2:?--product needs a game id or launcher}"
            shift
            ;;
        --pipeline)
            if [ "$#" -lt 2 ]; then
                echo "native-builds: --pipeline requires urp or hdrp" >&2
                exit 2
            fi
            RENDER_PIPELINE="$2"
            shift
            ;;
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

if [ -z "$PRODUCT" ] || [[ ! "$PRODUCT" =~ ^[a-z0-9][a-z0-9-]*$ ]]; then
    echo "native-builds: --product must name a game (brocoli) or launcher" >&2
    exit 2
fi
case "$PRODUCT" in
    brocoli) PRODUCT_NAME="BROcoli" ;;
    launcher) PRODUCT_NAME="GameLauncher" ;;
    *) PRODUCT_NAME="$PRODUCT" ;;
esac
NATIVE_ROOT="$PROJECT_PATH/build/native/$PRODUCT"
PLAYERS_ROOT="$NATIVE_ROOT/players"
ARTIFACTS_ROOT="$NATIVE_ROOT/artifacts"

case "$RENDER_PIPELINE" in
    urp | hdrp) ;;
    *)
        echo "native-builds: --pipeline must be urp or hdrp" >&2
        exit 2
        ;;
esac

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
require_tool python3
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

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" | head -1)"
if [ -z "$UNITY_VERSION" ]; then
    echo "native-builds: could not read the Unity editor version" >&2
    exit 2
fi

SELECTED_TARGETS="$(
    IFS=,
    printf '%s' "${TARGETS[*]}"
)"

# Outputs are fixed under build/native/<validated product>; clear stale artifacts.
rm -rf -- "$PLAYERS_ROOT" "$ARTIFACTS_ROOT"
mkdir -p "$PLAYERS_ROOT" "$ARTIFACTS_ROOT"
RELEASE_ARGUMENTS=(
    "$PROJECT_PATH/scripts/release-build.py"
    --product "$PRODUCT"
    --targets "$SELECTED_TARGETS"
    --pipeline "$RENDER_PIPELINE"
    --output "$PLAYERS_ROOT"
)
if [ "$DEVELOPMENT" -eq 1 ]; then
    RELEASE_ARGUMENTS+=(--development)
fi
python3 "${RELEASE_ARGUMENTS[@]}"
cp "$PLAYERS_ROOT/release-audit.json" "$ARTIFACTS_ROOT/"

require_player() {
    if [ ! -e "$1" ]; then
        echo "native-builds: expected player was not produced: $1" >&2
        exit 1
    fi
}

ARCHIVES=()
if has_target windows; then
    require_player "$PLAYERS_ROOT/windows/${PRODUCT_NAME}.exe"
    (
        cd "$PLAYERS_ROOT/windows"
        zip -qry "$ARTIFACTS_ROOT/${PRODUCT_NAME}-windows-x86_64.zip" .
    )
    ARCHIVES+=("${PRODUCT_NAME}-windows-x86_64.zip")
fi
if has_target macos; then
    require_player "$PLAYERS_ROOT/macos/${PRODUCT_NAME}.app"
    ditto -c -k --sequesterRsrc --keepParent \
        "$PLAYERS_ROOT/macos/${PRODUCT_NAME}.app" "$ARTIFACTS_ROOT/${PRODUCT_NAME}-macos-universal.zip"
    ARCHIVES+=("${PRODUCT_NAME}-macos-universal.zip")
fi
if has_target linux; then
    require_player "$PLAYERS_ROOT/linux/${PRODUCT_NAME}.x86_64"
    tar -C "$PLAYERS_ROOT/linux" -czf "$ARTIFACTS_ROOT/${PRODUCT_NAME}-linux-x86_64.tar.gz" .
    ARCHIVES+=("${PRODUCT_NAME}-linux-x86_64.tar.gz")
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

# The identifier has to change when the same commit is rebuilt, because a
# rolling release publishes many builds under one tag.
BUILT_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
BUILD_ID="${BUILT_AT//[-:]/}-$(git -C "$PROJECT_PATH" rev-parse --short=7 HEAD)"

cat >"$ARTIFACTS_ROOT/build-info.txt" <<EOF
build_id=$BUILD_ID
commit=$COMMIT_SHA
unity=$UNITY_VERSION
targets=$SELECTED_TARGETS
product=$PRODUCT
render_pipeline=$RENDER_PIPELINE
development=$DEVELOPMENT_BUILD
dirty=$DIRTY
built_at=$BUILT_AT
EOF

(
    cd "$ARTIFACTS_ROOT"
    shasum -a 256 "${ARCHIVES[@]}" build-info.txt release-audit.json >SHA256SUMS
)

echo ""
echo "native-builds: packaged release artifacts in $ARTIFACTS_ROOT"
(
    cd "$ARTIFACTS_ROOT"
    du -h "${ARCHIVES[@]}"
)

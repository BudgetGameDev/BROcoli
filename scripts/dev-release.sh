#!/usr/bin/env bash
# Build native players and publish them to the rolling development prerelease.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS_ROOT="$PROJECT_PATH/build/native/artifacts"
PUBLISH_ROOT="$PROJECT_PATH/build/native/publish"
TAG="nightly"
TARGETS="windows"
TITLE=""
SKIP_BUILD=0
DEVELOPMENT=0

usage() {
    cat <<'EOF'
Usage: ./scripts/dev-release.sh [options]

Build the selected native players and publish them to a single rolling
prerelease. Unlike ./scripts/native-release.sh, this release is meant to be
overwritten: the tag is force-moved to HEAD and the release keeps only the
assets from the newest build.

Options:
    --targets <list>  Comma-separated subset of windows,macos,linux.
                      Defaults to windows.
    --tag <name>      Rolling tag to publish (default: nightly).
    --title <text>    Override the release title.
    --skip-build      Publish the already-packaged build/native artifacts.
    --development     Publish Unity development players.
    -h, --help        Show this help.
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --skip-build) SKIP_BUILD=1 ;;
        --development) DEVELOPMENT=1 ;;
        --targets | --tag | --title)
            if [ "$#" -lt 2 ]; then
                echo "dev-release: '$1' requires a value" >&2
                exit 2
            fi
            case "$1" in
                --targets) TARGETS="$2" ;;
                --tag) TAG="$2" ;;
                --title) TITLE="$2" ;;
            esac
            shift
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "dev-release: unknown argument '$1'" >&2
            usage >&2
            exit 2
            ;;
    esac
    shift
done

if ! printf '%s' "$TAG" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9._-]*$'; then
    echo "dev-release: '$TAG' is not usable as a tag and file name suffix" >&2
    exit 2
fi

for tool in git gh awk shasum; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "dev-release: missing required tool '$tool'" >&2
        exit 2
    fi
done

# A rolling tag is only useful if it names a commit others can fetch, so the
# release must describe a clean, pushed HEAD rather than a working copy.
cd "$PROJECT_PATH"
if [ -n "$(git status --porcelain)" ]; then
    echo "dev-release: the checkout must be clean before publishing" >&2
    exit 2
fi
HEAD_COMMIT="$(git rev-parse HEAD)"

if ! gh auth status >/dev/null 2>&1; then
    echo "dev-release: authenticate GitHub CLI with 'gh auth login'" >&2
    exit 2
fi

if [ "$SKIP_BUILD" -eq 0 ]; then
    BUILD_ARGUMENTS=(--targets "$TARGETS")
    if [ "$DEVELOPMENT" -eq 1 ]; then
        BUILD_ARGUMENTS+=(--development)
    fi
    "$PROJECT_PATH/scripts/native-builds.sh" "${BUILD_ARGUMENTS[@]}"
fi

# shellcheck source=scripts/native-artifacts.sh
. "$PROJECT_PATH/scripts/native-artifacts.sh"

ASSETS=()
while IFS= read -r asset; do
    ASSETS+=("$asset")
done < <(native_artifacts_verify "$ARTIFACTS_ROOT" "$HEAD_COMMIT")
if [ "${#ASSETS[@]}" -eq 0 ]; then
    echo "dev-release: artifact verification failed" >&2
    exit 1
fi

BUILD_ID="$(native_artifacts_field "$ARTIFACTS_ROOT" build_id)"
BUILD_TARGETS="$(native_artifacts_field "$ARTIFACTS_ROOT" targets)"
BUILD_UNITY="$(native_artifacts_field "$ARTIFACTS_ROOT" unity)"
BUILD_AT="$(native_artifacts_field "$ARTIFACTS_ROOT" built_at)"
if [ -z "$BUILD_ID" ]; then
    echo "dev-release: build-info records no build id; rebuild" >&2
    exit 1
fi
if [ "$(native_artifacts_field "$ARTIFACTS_ROOT" development)" = "true" ]; then
    BUILD_KIND="development"
else
    BUILD_KIND="release"
fi

# A player archive is named after the channel it ships in, so a downloaded file
# still says which release it came from. The packaged artifacts stay generic:
# the same build can be published to another channel.
channel_name() {
    case "$1" in
        *.tar.gz) printf '%s-%s.tar.gz' "${1%.tar.gz}" "$TAG" ;;
        *.zip) printf '%s-%s.zip' "${1%.zip}" "$TAG" ;;
        *) printf '%s' "$1" ;;
    esac
}

rm -rf -- "$PUBLISH_ROOT"
mkdir -p "$PUBLISH_ROOT"
UPLOADS=()
for asset in "${ASSETS[@]}"; do
    published="$PUBLISH_ROOT/$(channel_name "$(basename "$asset")")"
    cp "$asset" "$published"
    UPLOADS+=("$published")
done

# Rename the entries rather than rehashing, so the published sums stay the ones
# that were just verified against the packaged build.
while read -r checksum name; do
    printf '%s  %s\n' "$checksum" "$(channel_name "$name")"
done <"$ARTIFACTS_ROOT/SHA256SUMS" >"$PUBLISH_ROOT/SHA256SUMS"
(
    cd "$PUBLISH_ROOT"
    shasum -a 256 -c SHA256SUMS >&2
)

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if ! git rev-parse --verify --quiet "refs/remotes/origin/$BRANCH" >/dev/null; then
    echo "dev-release: '$BRANCH' has no origin counterpart; push it first" >&2
    exit 2
fi
if ! git merge-base --is-ancestor "$HEAD_COMMIT" "refs/remotes/origin/$BRANCH"; then
    echo "dev-release: HEAD is not pushed to origin; push it before publishing" >&2
    exit 2
fi

NOTES_FILE="$(mktemp "${TMPDIR:-/tmp}/brocoli-dev-release.XXXXXX")"
trap 'rm -f -- "$NOTES_FILE"' EXIT
cat >"$NOTES_FILE" <<EOF
Rolling \`$TAG\` build of BROcoli. This release is overwritten in place: the
\`$TAG\` tag and every asset below move to the newest published build, so
download links always serve the latest \`$TAG\` player.

- Build ID: \`$BUILD_ID\`
- Commit: \`$HEAD_COMMIT\`
- Players: ${BUILD_TARGETS//,/, } ($BUILD_KIND)
- Unity: $BUILD_UNITY
- Packaged: $BUILD_AT

Verify a download against \`SHA256SUMS\` before running it. Windows SmartScreen
warns about these players because they are unsigned.
EOF

echo "dev-release: moving tag '$TAG' to $HEAD_COMMIT"
git tag -f "$TAG" "$HEAD_COMMIT" >/dev/null
git push --force origin "refs/tags/$TAG"

if [ -z "$TITLE" ]; then
    TITLE="$TAG"
fi

if gh release view "$TAG" >/dev/null 2>&1; then
    echo "dev-release: replacing the assets on the existing '$TAG' release"
    # Drop assets first: a previous publish may have covered other platforms,
    # and a stale player next to the new one is worse than no player at all.
    while IFS= read -r existing; do
        if [ -n "$existing" ]; then
            gh release delete-asset "$TAG" "$existing" --yes
        fi
    done < <(gh release view "$TAG" --json assets --jq '.assets[].name')
    gh release edit "$TAG" \
        --title "$TITLE" --notes-file "$NOTES_FILE" --prerelease --draft=false
    gh release upload "$TAG" "${UPLOADS[@]}" --clobber
else
    echo "dev-release: creating the '$TAG' release"
    gh release create "$TAG" --verify-tag --prerelease \
        --title "$TITLE" --notes-file "$NOTES_FILE" "${UPLOADS[@]}"
fi

echo ""
gh release view "$TAG" --json url --jq '"dev-release: published " + .url'

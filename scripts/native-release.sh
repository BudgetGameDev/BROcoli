#!/usr/bin/env bash
# Build native players and publish them as assets on an existing git tag.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS_ROOT="$PROJECT_PATH/build/native/brocoli/artifacts"
TAG=""
TITLE=""
NOTES_FILE=""
SKIP_BUILD=0
DRAFT=0
PRERELEASE=0

usage() {
    cat <<'EOF'
Usage: ./scripts/native-release.sh <tag> [options]

Build all native desktop players and create a GitHub Release containing them.
The tag must already exist, point at HEAD, and the checkout must be clean.

Options:
    --draft             Create a draft release.
    --prerelease        Mark the release as a prerelease.
    --skip-build        Publish the already-packaged build/native artifacts.
    --title <text>      Override the release title (defaults to the tag).
    --notes-file <path> Use release notes from a file instead of generated notes.
    -h, --help          Show this help.
EOF
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
    usage
    exit 0
fi
if [ "$#" -eq 0 ]; then
    usage >&2
    exit 2
fi

TAG="$1"
shift
while [ "$#" -gt 0 ]; do
    case "$1" in
        --draft) DRAFT=1 ;;
        --prerelease) PRERELEASE=1 ;;
        --skip-build) SKIP_BUILD=1 ;;
        --title | --notes-file)
            if [ "$#" -lt 2 ]; then
                echo "native-release: '$1' requires a value" >&2
                exit 2
            fi
            if [ "$1" = "--title" ]; then
                TITLE="$2"
            else
                NOTES_FILE="$2"
            fi
            shift
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "native-release: unknown argument '$1'" >&2
            usage >&2
            exit 2
            ;;
    esac
    shift
done

for tool in git gh awk shasum; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "native-release: missing required tool '$tool'" >&2
        exit 2
    fi
done
if [ -n "$NOTES_FILE" ] && [ ! -f "$NOTES_FILE" ]; then
    echo "native-release: notes file does not exist: $NOTES_FILE" >&2
    exit 2
fi

cd "$PROJECT_PATH"
if [ -n "$(git status --porcelain)" ]; then
    echo "native-release: the checkout must be clean before publishing" >&2
    exit 2
fi

HEAD_COMMIT="$(git rev-parse HEAD)"
if ! TAG_COMMIT="$(git rev-parse "$TAG^{commit}" 2>/dev/null)"; then
    echo "native-release: tag '$TAG' does not exist locally" >&2
    exit 2
fi
if [ "$HEAD_COMMIT" != "$TAG_COMMIT" ]; then
    echo "native-release: tag '$TAG' does not point at HEAD" >&2
    exit 2
fi

if ! gh auth status >/dev/null 2>&1; then
    echo "native-release: authenticate GitHub CLI with 'gh auth login'" >&2
    exit 2
fi
if gh release view "$TAG" >/dev/null 2>&1; then
    echo "native-release: GitHub Release '$TAG' already exists" >&2
    exit 2
fi

if [ "$SKIP_BUILD" -eq 0 ]; then
    "$PROJECT_PATH/scripts/native-builds.sh" --product brocoli
fi

# shellcheck source=scripts/native-artifacts.sh
. "$PROJECT_PATH/scripts/native-artifacts.sh"

ASSETS=()
while IFS= read -r asset; do
    ASSETS+=("$asset")
done < <(native_artifacts_verify "$ARTIFACTS_ROOT" "$HEAD_COMMIT")
if [ "${#ASSETS[@]}" -eq 0 ]; then
    echo "native-release: artifact verification failed" >&2
    exit 1
fi
if [ "$(native_artifacts_field "$ARTIFACTS_ROOT" targets)" != "windows,macos,linux" ]; then
    echo "native-release: a tagged release must contain all three players" >&2
    exit 1
fi
if [ "$(native_artifacts_field "$ARTIFACTS_ROOT" development)" != "false" ]; then
    echo "native-release: refusing to publish development players" >&2
    exit 1
fi

if [ -z "$TITLE" ]; then
    TITLE="$TAG"
fi

RELEASE_ARGUMENTS=(release create "$TAG" --verify-tag --title "$TITLE")
if [ -n "$NOTES_FILE" ]; then
    RELEASE_ARGUMENTS+=(--notes-file "$NOTES_FILE")
else
    RELEASE_ARGUMENTS+=(--generate-notes)
fi
if [ "$DRAFT" -eq 1 ]; then
    RELEASE_ARGUMENTS+=(--draft)
fi
if [ "$PRERELEASE" -eq 1 ]; then
    RELEASE_ARGUMENTS+=(--prerelease)
fi

gh "${RELEASE_ARGUMENTS[@]}" "${ASSETS[@]}"

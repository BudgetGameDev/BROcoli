#!/usr/bin/env bash
# Publish the player ci.sh just built to GitHub Pages.
#
# This replaces the hosted Pages build: the Mac mini has already produced and
# smoke-tested the exact artifact that job would rebuild, so it publishes that
# one instead of paying for the same build twice.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_PATH"

PLAYER_PATH="$PROJECT_PATH/build/WebGL"
# Overridable so a publish can be rehearsed against a scratch remote before it
# is pointed at the real Pages branch.
PAGES_REMOTE="${BROCOLI_PAGES_REMOTE:-origin}"
PAGES_BRANCH="${BROCOLI_PAGES_BRANCH:-gh-pages}"

usage() {
    cat >&2 <<'USAGE'
Usage: ./cd.sh <staging|production>

  staging     Publish to the BranchStaging and BranchMain folders.
  production  Publish to the Pages root, keeping both staging folders.

Publishes build/WebGL, which ./ci.sh produces. Run ./ci.sh first.
USAGE
}

if [ "$#" -ne 1 ]; then
    usage
    exit 2
fi

case "$1" in
    staging | production) TARGET_BRANCH="$1" ;;
    -h | --help)
        usage
        exit 0
        ;;
    *)
        usage
        exit 2
        ;;
esac

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "cd: missing required tool '$1'. See CONTRIBUTING.md." >&2
        exit 2
    fi
}

require_tool git
require_tool jq
require_tool node
require_tool rsync

# Nothing reaches Pages that ./ci.sh has not vouched for. The receipt binds a
# green run to this commit, this working tree, and this built player, so neither
# a stale pass nor a hand-run ./cd.sh can publish unverified code.
echo ""
echo "==> Verify ci.sh passed"
python3 scripts/ci_receipt.py verify

# ci.sh builds this immediately before cd.sh runs. Refuse to publish anything
# that is not a complete player rather than deploying a half-written directory.
if [ ! -f "$PLAYER_PATH/index.html" ] || [ ! -d "$PLAYER_PATH/Build" ]; then
    echo "cd: no player at $PLAYER_PATH; run ./ci.sh first" >&2
    exit 1
fi

echo ""
echo "==> Player contract"
node scripts/check-webgl-build.cjs "$PLAYER_PATH"

echo ""
echo "==> Build metadata"
COMMIT_SHA="$(git rev-parse HEAD)"
BUILD_ID="build-$(date +%s)-$(git rev-parse --short=7 HEAD)"
BUILD_TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
# The hosted job used the workflow run number. The commit count is the local
# equivalent: an integer that only increases as the branch advances.
BUILD_NUMBER="$(git rev-list --count HEAD)"
COMMIT_MSG="$(git log -1 --pretty=%B | head -n 1)"

jq -n \
    --arg buildId "$BUILD_ID" \
    --arg buildTimestamp "$BUILD_TIMESTAMP" \
    --argjson buildNumber "$BUILD_NUMBER" \
    --arg commitSha "$COMMIT_SHA" \
    --arg description "$COMMIT_MSG" \
    '{buildId: $buildId, buildTimestamp: $buildTimestamp,
      buildNumber: $buildNumber, commitSha: $commitSha,
      description: $description}' \
    >"$PLAYER_PATH/version.json"
echo "cd: $BUILD_ID (build $BUILD_NUMBER)"

if [ "$TARGET_BRANCH" = "staging" ]; then
    BASE_NAME="$(jq -r '.name' "$PLAYER_PATH/manifest.json")"
    SHORT_NAME="$(jq -r '.short_name' "$PLAYER_PATH/manifest.json")"

    jq --arg name "${BASE_NAME} Staging" \
        --arg short_name "${SHORT_NAME} Staging" \
        --arg desc "${BASE_NAME} Staging Build - Development/Testing Version" \
        '.name = $name | .short_name = $short_name | .description = $desc' \
        "$PLAYER_PATH/manifest-staging.json" \
        >"$PLAYER_PATH/manifest-staging-tmp.json"
    mv "$PLAYER_PATH/manifest-staging-tmp.json" "$PLAYER_PATH/manifest-staging.json"
    echo "cd: staging manifest names the build '${BASE_NAME} Staging'"
fi

echo ""
echo "==> Publish to $PAGES_BRANCH"
WORKTREE_PATH="$(mktemp -d "${TMPDIR:-/tmp}/brocoli-pages.XXXXXX")"

cleanup() {
    git worktree remove --force "$WORKTREE_PATH" >/dev/null 2>&1 || true
    rm -rf "$WORKTREE_PATH"
}
trap cleanup EXIT

# Build on the tip just fetched rather than a remote-tracking ref, which may be
# stale and does not exist at all when the remote is given as a URL.
git fetch --quiet "$PAGES_REMOTE" "$PAGES_BRANCH"
git worktree add --quiet --detach "$WORKTREE_PATH" "$(git rev-parse FETCH_HEAD)"

if [ "$TARGET_BRANCH" = "staging" ]; then
    # Staging owns both folders; BranchMain is the legacy path kept for old
    # installs that still point at it.
    for folder in BranchStaging BranchMain; do
        mkdir -p "$WORKTREE_PATH/$folder"
        rsync --archive --delete "$PLAYER_PATH/" "$WORKTREE_PATH/$folder/"
        echo "cd: staged $folder"
    done
else
    # Production owns the root. Keep both staging folders, the Pages metadata,
    # and the worktree's own .git link, which --delete would otherwise remove.
    rsync --archive --delete \
        --exclude "/.git" \
        --exclude "/.gitignore" \
        --exclude "/BranchMain" \
        --exclude "/BranchStaging" \
        --exclude "/.nojekyll" \
        --exclude "/CNAME" \
        "$PLAYER_PATH/" "$WORKTREE_PATH/"
    echo "cd: staged the Pages root"
fi

# Republish as a single root commit. Every deploy rewrites ~90MB of player, so
# keeping the history would grow the repository without bound; the source
# commit each deploy came from stays recoverable through version.json.
# Built with plumbing rather than `checkout --orphan`, which would leave a
# branch behind and fail the next publish. commit-tree with no parent is the
# root commit we want, and names nothing.
git -C "$WORKTREE_PATH" add --all
PAGES_TREE="$(git -C "$WORKTREE_PATH" write-tree)"
PAGES_COMMIT="$(
    git -C "$WORKTREE_PATH" commit-tree "$PAGES_TREE" \
        -m "Deploy $TARGET_BRANCH $BUILD_ID" \
        -m "Source commit: $COMMIT_SHA"
)"
git -C "$WORKTREE_PATH" push --force --quiet \
    "$PAGES_REMOTE" "$PAGES_COMMIT:refs/heads/$PAGES_BRANCH"

echo ""
echo "cd: published $TARGET_BRANCH to $PAGES_BRANCH"

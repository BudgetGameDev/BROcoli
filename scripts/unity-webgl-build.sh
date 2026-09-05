#!/usr/bin/env bash
# Build an explicit product in isolation, even while the source Editor is open.
set -euo pipefail
PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
PRODUCT="${1:?Usage: unity-webgl-build.sh brocoli|launcher|<game-id>}"
OUTPUT_PATH="$PROJECT_PATH/build/WebGL"
# Fixed generated output: stale code cannot satisfy later smoke checks.
rm -rf -- "$OUTPUT_PATH"
python3 "$PROJECT_PATH/scripts/release-build.py" --product "$PRODUCT" \
    --targets webgl --pipeline urp --output "$OUTPUT_PATH"

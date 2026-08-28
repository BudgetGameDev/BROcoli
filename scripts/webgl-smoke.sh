#!/usr/bin/env bash
# Launch a built WebGL player in headless Chrome and require Unity startup to finish.
set -euo pipefail

BUILD_DIR="${1:-build/WebGL}"
if [ ! -f "$BUILD_DIR/index.html" ]; then
    echo "webgl-smoke: missing $BUILD_DIR/index.html" >&2
    exit 2
fi

node "$(dirname "$0")/check-webgl-build.cjs" "$BUILD_DIR"

BROWSER=""
for candidate in \
    google-chrome \
    google-chrome-stable \
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
    chromium \
    chromium-browser; do
    if command -v "$candidate" >/dev/null 2>&1; then
        BROWSER="$(command -v "$candidate")"
        break
    fi
done

if [ -z "$BROWSER" ]; then
    echo "webgl-smoke: Chrome or Chromium is required" >&2
    exit 2
fi

SMOKE_TEMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/brocoli-webgl-smoke.XXXXXX")"
SERVER_PID=""
BROWSER_PID=""
cleanup() {
    if [ -n "$BROWSER_PID" ] && kill -0 "$BROWSER_PID" 2>/dev/null; then
        kill "$BROWSER_PID"
        wait "$BROWSER_PID" 2>/dev/null || true
    fi
    if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2>/dev/null; then
        kill "$SERVER_PID"
        wait "$SERVER_PID" 2>/dev/null || true
    fi
    rm -rf "$SMOKE_TEMP_DIR" 2>/dev/null || true
}
trap cleanup EXIT

python3 -m http.server 4173 \
    --bind 127.0.0.1 \
    --directory "$BUILD_DIR" \
    >"$SMOKE_TEMP_DIR/server.log" 2>&1 &
SERVER_PID="$!"

SERVER_READY=false
for _ in $(seq 1 50); do
    if curl --fail --silent http://127.0.0.1:4173/ >/dev/null; then
        SERVER_READY=true
        break
    fi
    sleep 0.1
done

if [ "$SERVER_READY" != true ]; then
    echo "webgl-smoke: local server did not become ready" >&2
    tail -80 "$SMOKE_TEMP_DIR/server.log" >&2 || true
    exit 1
fi

"$BROWSER" \
    --headless=new \
    --no-sandbox \
    --disable-dev-shm-usage \
    --enable-webgl \
    --ignore-gpu-blocklist \
    --remote-allow-origins='*' \
    --remote-debugging-port=9223 \
    --user-data-dir="$SMOKE_TEMP_DIR/chrome-profile" \
    "http://127.0.0.1:4173/?webgl-smoke=1" \
    >"$SMOKE_TEMP_DIR/chrome.log" 2>&1 &
BROWSER_PID="$!"

if ! node "$(dirname "$0")/webgl-smoke.cjs" \
    "http://127.0.0.1:4173/" 9223 120000; then
    tail -80 "$SMOKE_TEMP_DIR/chrome.log" >&2 || true
    exit 1
fi

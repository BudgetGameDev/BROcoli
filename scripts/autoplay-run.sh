#!/bin/bash
# Autoplay / E2E run helper (core).
#
#   ./scripts/autoplay-run.sh [--build] [--seed N] [--duration S] [--interval S] \
#                             [--scenario smoke|survive|progress] [--out DIR] [extra...]
#
# --build first builds the StandaloneOSX player (requires the Unity editor CLOSED).
# Without --build it runs the already-built Build/BROcoli-autoplay.app.
# Extra args (e.g. --no-deterministic, --minlevel=3) are passed through to the player.
#
# Exit code mirrors the scenario pass/fail. Prints summary.json and (if Pillow is
# available) a per-band luminance report. See plans/2026-06-13-autoplay-e2e-harness.md.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="6000.3.6f1"
APP="$PROJECT_PATH/Build/BROcoli-autoplay.app"

SEED=42
DURATION=60
INTERVAL=0.5
SCENARIO=survive
OUT=""
DO_BUILD=0
EXTRA=()

while [ $# -gt 0 ]; do
  case "$1" in
    --build) DO_BUILD=1; shift ;;
    --seed) SEED="$2"; shift 2 ;;
    --duration) DURATION="$2"; shift 2 ;;
    --interval) INTERVAL="$2"; shift 2 ;;
    --scenario) SCENARIO="$2"; shift 2 ;;
    --out) OUT="$2"; shift 2 ;;
    *) EXTRA+=("$1"); shift ;;   # pass-through to the player
  esac
done

find_unity() {
  local base p
  for base in "/Applications/Unity/Hub/Editor" "$HOME/Applications/Unity/Hub/Editor"; do
    p="$base/$VERSION/Unity.app/Contents/MacOS/Unity"
    [ -x "$p" ] && { echo "$p"; return 0; }
  done
  return 1
}

SHA="$(git -C "$PROJECT_PATH" rev-parse --short HEAD 2>/dev/null || echo nogit)"
[ -n "$OUT" ] || OUT="$PROJECT_PATH/AutoplayRuns/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"

if [ "$DO_BUILD" -eq 1 ]; then
  UNITY="$(find_unity)" || { echo "Unity $VERSION not found under /Applications or ~/Applications"; exit 1; }
  echo "Building player (the Unity editor must be CLOSED)..."
  "$UNITY" -batchmode -quit -projectPath "$PROJECT_PATH" \
    -executeMethod AutoplayBuildScript.BuildAutoplayPlayer \
    -logFile "$OUT/build.log" || { echo "Build failed; see $OUT/build.log"; exit 1; }
fi

if [ ! -d "$APP" ]; then
  echo "Player not built. Run with --build (editor closed) or use Tools > Autoplay > Build Player."
  exit 1
fi

BIN="$(ls "$APP/Contents/MacOS/"* 2>/dev/null | head -1 || true)"
[ -n "$BIN" ] && [ -x "$BIN" ] || { echo "No executable found in $APP/Contents/MacOS"; exit 1; }

echo "Running [$SCENARIO] ${DURATION}s seed=$SEED sha=$SHA -> $OUT"
set +e
"$BIN" --autoplay --seed="$SEED" --duration="$DURATION" --interval="$INTERVAL" \
  --scenario="$SCENARIO" --sha="$SHA" "${EXTRA[@]+"${EXTRA[@]}"}" --out="$OUT" \
  -logFile "$OUT/player.log"
RC=$?
set -e

echo ""
echo "=== summary.json ==="; cat "$OUT/summary.json" 2>/dev/null; echo
echo "frames:    $OUT/frames ($(ls "$OUT/frames" 2>/dev/null | wc -l | tr -d ' ') png)"
echo "telemetry: $OUT/telemetry.jsonl"
if [ "$RC" -eq 0 ]; then echo "RESULT: PASS (exit 0)"; else echo "RESULT: FAIL (exit $RC)"; fi

if command -v python3 >/dev/null 2>&1; then
  python3 "$PROJECT_PATH/scripts/analyze-frames.py" "$OUT/frames" 2>/dev/null || true
fi

exit $RC

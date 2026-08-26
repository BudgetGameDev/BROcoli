#!/bin/bash
# TUNING session: a long, real-time run that hot-reloads lighting from a JSON file.
# Edit the JSON (default /tmp/brocoli-tuning.json) WHILE the game runs and changes
# apply live -- no rebuild. Frames are captured so each change can be reviewed.
#
#   ./scripts/autoplay-tune.sh [--build]      # build once if you changed code/scene
#   # then edit $TUNING and watch the frames update
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
TUNING="${TUNING:-/tmp/brocoli-tuning.json}"

if [ ! -f "$TUNING" ]; then
    cat >"$TUNING" <<'JSON'
{
  "worldLightIntensity": 250,
  "fillFactor": 0.6,
  "lightHeightZ": -8,
  "ambientIntensity": 1
}
JSON
    echo "Wrote default tuning file: $TUNING"
fi

echo "Tuning file: $TUNING  (edit it live; changes apply within ~0.5s)"
exec "$DIR/autoplay-run.sh" --duration 600 --interval 1.0 --scenario smoke \
    --no-deterministic "--tuning=$TUNING" "$@"

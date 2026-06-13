#!/bin/bash
# MARATHON tier (5th): simulate ~3 HOURS of gameplay (10800 game-seconds) to confirm
# the game stays playable and stable over a long session — compressed into a handful
# of real minutes via fake-time fast-forward. Physics keeps its fixed step (accurate);
# only wall-clock time is compressed. Captures every 60 game-seconds.
# Real time depends on your CPU and is reported at the end.
#   ./scripts/autoplay-marathon.sh [--build]
exec "$(cd "$(dirname "$0")" && pwd)/autoplay-run.sh" \
  --duration 10800 --interval 60 --scenario survive --timestep=0.04 "$@"

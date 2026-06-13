#!/bin/bash
# FAST tier (4th): ~5 MINUTES of gameplay compressed into ~30s of real time via
# fake-time fast-forward. Physics keeps its fixed step, so the simulation stays
# accurate — only wall-clock time is compressed. Sparse captures.
#   ./scripts/autoplay-fast.sh [--build]
exec "$(cd "$(dirname "$0")" && pwd)/autoplay-run.sh" \
  --duration 300 --interval 2 --scenario survive --timestep=0.033 "$@"

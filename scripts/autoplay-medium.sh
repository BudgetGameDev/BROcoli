#!/bin/bash
# MEDIUM tier: 1-minute run, asserts the player survives the whole minute.
# Pass --build to rebuild first, e.g.:  ./scripts/autoplay-medium.sh --build
exec "$(cd "$(dirname "$0")" && pwd)/autoplay-run.sh" \
    --duration 60 --interval 0.5 --scenario survive "$@"

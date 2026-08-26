#!/bin/bash
# LONG tier: 5-minute run for consistent gameplay testing (sparser captures).
# Pass --build to rebuild first, e.g.:  ./scripts/autoplay-long.sh --build
exec "$(cd "$(dirname "$0")" && pwd)/autoplay-run.sh" \
    --duration 300 --interval 1.0 --scenario survive "$@"

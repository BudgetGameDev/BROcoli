#!/bin/bash
# SMOKE tier: quick 5-second visual/sanity check (frequent captures).
# Pass --build to rebuild first, e.g.:  ./scripts/autoplay-smoke.sh --build
exec "$(cd "$(dirname "$0")" && pwd)/autoplay-run.sh" \
    --duration 5 --interval 0.25 --scenario smoke "$@"

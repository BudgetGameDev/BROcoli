# Autonomous autoplay

The autoplay harness runs a real game session through the same movement input path as a
player. It does not teleport the player or replay a command script. An autonomous utility
agent repeatedly observes the current run, chooses an intent, and steers the player.

## Agent behaviour

The agent can:

- explore unvisited rooms through open dungeon doorways;
- plan routes over the runtime NavMesh and follow path corners around walls and props;
- probe nearby space before moving and choose a clear alternative direction;
- detect lost movement progress and perform a bounded stuck-recovery manoeuvre;
- approach enemies until they enter spray range, then strafe and kite them;
- retreat from nearby enemies, crowds, or combat while low on health;
- predict incoming projectile paths and dodge across them; and
- score level-up choices from their actual bonus, penalty, current health, nearby threat
  count, and stat caps.

The active intent is one of `waiting`, `explore`, `engage`, `retreat`, `dodge`, or
`recover`. Navigation and combat decisions are deterministic for a given run seed.

## Running it

Build the desktop player once, then select a run tier:

```bash
./scripts/autoplay-smoke.sh --build
./scripts/autoplay-medium.sh
./scripts/autoplay-fast.sh
./scripts/autoplay-long.sh
./scripts/autoplay-marathon.sh
```

Use the core runner for a custom seed or scenario:

```bash
./scripts/autoplay-run.sh --seed 42 --duration 120 --scenario progress --minlevel=3
```

The supported scenarios are:

- `smoke`: the session completed without warnings, errors, or exceptions;
- `survive`: the player stayed alive for the configured duration; and
- `progress`: the player reached `--minlevel` before the run ended.

## Results

Each run writes screenshots, `telemetry.jsonl`, `summary.json`, and player logs beneath
`AutoplayRuns/` unless `--out` selects another directory. Telemetry includes position,
health, level, enemy pressure, current bot intent, visited rooms, route replans, and stuck
recoveries. The summary adds total distance travelled and final navigation counters.

A run fails if Unity emits any warning, error, assertion, or exception. This makes the
harness suitable for unattended E2E soak testing while keeping it opt-in from the normal
promotion gate, which does not create a separate desktop player build.

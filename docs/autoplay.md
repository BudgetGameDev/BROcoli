# Autonomous autoplay

The autoplay harness plays the game the way a person would and reports what it
reached. It drives the ordinary movement input path -- it never teleports the
player or replays a recorded script -- and an autonomous agent repeatedly
observes the run, scores its options, and steers.

The whole harness is game code. It lives in
`LocalPackages/com.budgetgamedev.game.brocoli/`, split between `Runtime/Autoplay/`
(the agent, the feature ledger, telemetry) and `Editor/Autoplay/` (building the
player, launching it, and reporting). Nothing about it is shell-specific, so the
same commands work on macOS, Linux, and Windows.

## Running it

With the Unity Editor closed:

```bash
unity run . -- -executeMethod \
    BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run -tier coverage
```

The runner builds the host platform's player if one is missing, launches it,
waits for it, prints a report, and exits with the run's verdict. Add `-build` to
force a rebuild after changing code or scenes.

With the Editor open, use **Tools > Autoplay** for the common tiers. Menu runs
start the player and return rather than blocking the Editor for the length of a
marathon.

The built player is also a first-class entry point, which is what makes the
harness portable: it understands the tiers itself.

```bash
./Build/BROcoli-autoplay.app/Contents/MacOS/BROcoli --autoplay --tier=medium
```

### Tiers

| Tier | Game time | What it is for |
| --- | --- | --- |
| `smoke` | 5s | Does the game start and render at all |
| `medium` | 1m | Ordinary play with the full feature sweep |
| `fast` | 5m | The same run compressed hard by a coarse update step |
| `long` | 5m | Five minutes at the ordinary update step |
| `marathon` | 3h | Does a long session stay stable and playable |
| `coverage` | 20m | From the main menu, asserting every system was used |
| `tune` | 10m | Real-time lighting tuning; no fast-forward |

### Options

Runner options are `-tier`, `-seed`, `-out`, `-build`, and `-timeout`. Anything
else is handed to the player untouched: `-duration`, `-interval`, `-timestep`,
`-scenario`, `-minlevel`, `-tuning`, `-menus`/`-noMenus`, and
`-features`/`-noFeatures`. An explicit option always beats the tier preset it
appears alongside.

```bash
unity run . -- -executeMethod \
    BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run \
    -tier long -seed 42 -scenario progress -minlevel 3
```

The scenarios are:

- `smoke`: the session completed without warnings, errors, or exceptions;
- `survive`: the player stayed alive for the configured duration;
- `progress`: the player reached `-minlevel` before the run ended; and
- `coverage`: every required feature was exercised.

Any scenario also fails on a Unity warning, error, assertion, or exception, and
any scenario fails if the run stalls -- two game-minutes without a level, an
experience pickup, or a new room. An agent pinned on something it cannot reach
survives happily and reaches its features, so nothing else here would catch it.

A `coverage` run starts a fresh life when the player dies rather than stopping
there. A roguelite run ends in death, and a sweep that stopped at the first one
would only test whatever that life happened to stumble into.

## What the agent does

Each tick the agent scores every goal it could pursue on one scale and takes the
best, with a small bias toward whatever it is already doing so it does not
dither. Recovering from being stuck and dodging a projectile already in the air
bypass the scoring, because deliberating over either is how an agent gets stuck
or shot.

The goals are `explore`, `engage`, `retreat`, `loot`, `collect`, `dodge`, and
`recover`. To pursue them it:

- explores unvisited rooms, preferring junctions that keep the frontier growing;
- plans routes over the runtime NavMesh and follows path corners around walls;
- probes nearby space before moving and picks a clear alternative direction;
- detects lost progress and performs a bounded stuck-recovery manoeuvre;
- fights at the range of the weapon the player is actually holding, so a spray
  upgrade or boost immediately changes how it kites;
- focuses the enemy it can finish soonest rather than merely the nearest;
- backs off from crowds, from anything inside its danger radius, and when hurt;
- walks to chests and to dropped boosts, which nothing else in the game brings to
  the player; and
- scores level-up choices from their real bonus, penalty, current health, nearby
  threat count, and stat caps.

## Feature coverage

Combat alone never opens the map. Alongside the agent, a director sweeps the
systems that need deliberate input -- the inventory and map overlays, the pause
menu and its settings pane, and checkpointing -- and records each one only after
checking the game responded. Everything else is recorded where it actually
happens in gameplay code, so the ledger reflects the game rather than the bot's
intentions.

The `coverage` scenario fails the run if a required feature was never reached.
Features that depend on the run's luck -- an elite spawning, a hydra surviving
long enough to split, the player dying -- are reported but never fail it.

The save probe writes only into a slot that was already empty, deletes it again,
and restores the two preferences it had to move. When all ten slots are taken it
falls back to a serialize/parse round trip rather than evicting a real run, so
the harness can never cost you a save.

## Accelerated time

Deterministic runs advance a fixed amount of game time per rendered frame and
render as fast as they can, so wall-clock time compresses while the simulation
keeps its own clock. Physics is unaffected: it still runs at the fixed step,
sub-stepped as many times per frame as the capture step covers.

That sub-stepping is the limit, and it is enforced rather than trusted. Unity
will not run more than `Time.maximumDeltaTime` of physics per frame, so a capture
step far above the fixed step would make physics silently fall behind the game
clock and the run would stop testing the game as it ships. `--timestep` is
clamped to four physics sub-steps per frame, and the achieved speedup is
reported in the summary so a run's compression is a measured number rather than
a hope.

## Results

Each run writes screenshots, `telemetry.jsonl`, `summary.json`, and player logs
beneath `AutoplayRuns/` unless `-out` selects another directory. Telemetry
includes position, health, level, enemy pressure, current intent, visited rooms,
route replans, and stuck recoveries. The summary adds the tier, the achieved
speedup, distance travelled, final navigation counters, the feature ledger, and
the list of required features the run never reached.

The runner also reads the captured frames back and prints mean luminance in top,
middle, and bottom screen bands. A run that has gone completely black, or blown
out to white, raises no exception and passes every other check, so the harness
measures the picture rather than trusting it.

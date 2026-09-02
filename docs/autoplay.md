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

A run plays silently and fills the display. Nobody sits and listens to a bot, so
it mutes the audio listener rather than the saved volume settings and hands the
sound back when the run ends; and its frames are the whole point of watching one,
so it takes the display's own resolution instead of a thumbnail window and the
pictures below read as ordinary screenshots.

### Tiers

| Tier | Game time | What it is for |
| --- | --- | --- |
| `smoke` | 5s | Does the game start and render at all |
| `medium` | 1m | Ordinary play with the full feature sweep |
| `fast` | 5m | The same run compressed hard by a coarse update step |
| `long` | 5m | Five minutes at the ordinary update step |
| `marathon` | 3h | Does a long session stay stable and playable |
| `coverage` | 20m | From the main menu, asserting every system was used |
| `balance` | 15m | Uninterrupted play, graded on progression and difficulty |
| `tune` | 10m | Real-time lighting tuning; no fast-forward |

### Options

Runner options are `-tier`, `-seed`, `-out`, `-build`, and `-timeout`. Anything
else is handed to the player untouched: `-duration`, `-interval`, `-timestep`,
`-scenario`, `-minlevel`, `-tuning`, `-capture-on`, `-menus`/`-noMenus`, and
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
- `progress`: the player reached `-minlevel` before the run ended;
- `coverage`: every required feature was exercised; and
- `balance`: the run's progression and difficulty stayed in band.

Any scenario also fails on a Unity warning, error, assertion, or exception, and
any scenario fails if the run stalls -- two game-minutes without a level, an
experience pickup, or a new room. An agent pinned on something it cannot reach
survives happily and reaches its features, so nothing else here would catch it.

A `coverage` or `balance` run starts a fresh life when the player dies rather
than stopping there. A roguelite run ends in death, and a sweep that stopped at
the first one would only test whatever that life happened to stumble into.

## Inspecting a live run

A batch run answers questions after the fact. An open Editor answers them while
the bot plays, which is what makes the harness useful for looking at the game
rather than only grading it: the agent inspects a real playthrough instead of
driving the game itself.

The player bootstrap reads the environment, so a run starts inside the Editor
without any separate entry point. Through the editor integration:

```bash
unity cmd eval 'System.Environment.SetEnvironmentVariable("BROCOLI_AUTOPLAY", "1");
System.Environment.SetEnvironmentVariable("BROCOLI_TIER", "tune");'
unity cmd editor_play
```

The bot then plays the game in the Game view while every editor query reads the
live objects behind it: `eval` for arbitrary C# against the running scene,
`find_gameobjects` and `get_component_properties` for the hierarchy,
`capture_game_view` with `source=screen` for the composited picture including
the HUD, and `get_console_logs` for what the game is saying about itself. The
same environment variables that steer a batch run steer this one --
`BROCOLI_TIER`, `BROCOLI_DURATION`, `BROCOLI_OUT`, `BROCOLI_CAPTURE_ON` -- so a
session can be shaped before Play and read afterwards from `AutoplayRuns/`.

Three things to know before relying on it:

- Clear those variables when finished (set them to `null` through `eval`), or the
  next Play in that Editor autoplays too.
- Prefer the `tune` tier. The deterministic tiers advance game time as fast as
  the machine renders, so a probe races the simulation; `tune` runs at real time.
- Read the state, not the verdict. The run still ends itself and leaves Play
  mode, and an Editor-only render warning such as "Ignoring depth surface store
  action as it is memoryless" is enough to mark the scenario failed. A verdict
  means what it says in the built player.

## Capturing a moment

The interval captures show a run; a trigger shows a moment. Name a recorded
event and the run photographs the frame it happened in, which is how an agent
asks a batch run one specific question -- show me the first experience orb that
drops -- rather than reading every frame looking for it.

```bash
unity run . -- -executeMethod \
    BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run \
    -tier medium -capture-on pickup.experience-dropped+0.4
```

The spec is `event[#occurrence|*][+delay]`, and the option may repeat or carry a
comma-separated list:

| Spec | What it photographs |
| --- | --- |
| `pickup.experience-dropped` | the first orb to drop |
| `combat.enemy-killed#3` | the third kill |
| `dungeon.chest-opened*` | every chest, capped at 40 frames per spec |
| `pickup.experience-dropped+0.4` | 0.4 game-seconds later, once the orb has landed |

The delay is what usually decides whether the picture is worth having: an event
fires when the game decides it, and the thing it is about is often still
arriving. An orb spawns in the air and takes about a third of a second to land.

The event names are the feature ledger's, listed in `AutoplayFeatures`
(`Runtime/Autoplay/AutoplayFeatures.cs`) -- everything the coverage sweep grades,
plus the moments recorded only so a run can be watched, such as
`pickup.experience-dropped`. Anything recorded through the ledger can be
triggered on, so a new moment costs one `AutoplayFeatureLog.Record` call at the
place in gameplay code where it genuinely happens.

Each fired trigger writes `events/<event>-<occurrence>.png` -- an ordinary
full-screen frame, the game exactly as a player would see it -- and a line in
`events.jsonl` naming the event, its occurrence, the game time, and the file.
`summary.json` carries the same list as `captures`, alongside `missingCaptures`
for triggers that were asked for and never fired, and the runner prints both.

A spec the harness cannot read warns, and a warning fails any scenario: a typo
is a mistake in the request, and a twenty-minute run that ends with no picture
is a worse way to learn about it. An event name that simply never happened is
not a warning -- it is reported as a capture that never fired.

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

## Progression and difficulty

Coverage says a system was reached. It says nothing about whether reaching it was
worth anything, and a run can pass every other check while the game behind it has
become trivial or unplayable: a bot that never drops below full health and a bot
that dies every ninety seconds both survive their runs, and every other number
the harness records reads the same for the two of them.

So a run also measures its own pacing and the pressure it played under, and the
`balance` scenario grades both against the band a session is meant to sit in.
The bands live in `ProgressionBalance` (`Runtime/Autoplay/ProgressionBalance.cs`)
and are the game's difficulty target written down:

| Measurement | Band | Outside it means |
| --- | --- | --- |
| Seconds per level | 25 to 150 | levels are confetti / levelling is a grind |
| Late vs early seconds per level | 0.9x to 4x | the curve never steepens / it walls |
| Mean health | 45% to 90% | fought at the edge of death / nothing is a threat |
| Share of the run under 35% health | 2% to 35% | never in trouble / only ever in trouble |
| Deaths per hour | 0.4 to 8 | the run cannot be lost / it cannot be learned |

Alongside those, the run records what the dungeon actually set each room to --
the ring, the player's power score at that moment, and the health, damage, and
count multipliers the room was built with -- because scaling fails quietly. A
build whose depth multiplier stopped applying still levels the player on schedule
and still kills them occasionally; what it stops doing is making the tenth room
different from the first, and nothing else here would notice.

```bash
unity run . -- -executeMethod \
    BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run -tier balance
```

The verdict is a list of findings rather than a bare pass, so a failing run says
which band it left and in which direction. `summary.json` carries the numbers
behind them under `progression` and `scaling`, and the runner prints the same as
`pacing`, `pressure`, `scaling`, and `balance` lines.

A balance run plays uninterrupted: the feature sweep pauses the game to poke
menus, which is right when the question is coverage and wrong when it is pacing,
because the seconds a level took would then include the seconds spent in the
inventory. It also runs at the ordinary update step rather than a coarse one, so
the bot gets a real player's number of decisions per game-second.

Two things a verdict cannot do. It cannot be drawn from a short run -- under four
game-minutes or six levels it reports that it is too short to judge rather than
guessing -- and one seed is one dungeon, so a tuning change is worth confirming
across a few (`-seed`).

The experience curve itself is `PlayerProgression`
(`Runtime/Player/PlayerProgression.cs`), and the enemy ladder that gates which
archetypes a ring may spawn is `DungeonEnemyPlacer.MinRingFor`. Those two and the
power exponents at the top of `DungeonEnemyPlacer` are the knobs a balance
finding points at.

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
beneath `AutoplayRuns/` unless `-out` selects another directory. Interval
captures land in `frames/` and cover the whole session, menus included, because
a run that never renders its first screen is exactly what the picture check
below is for. Triggered captures land in `events/` with `events.jsonl` beside
them. Telemetry includes position, health, level, enemy pressure, current
intent, visited rooms, route replans, and stuck recoveries. The summary adds the
tier, the achieved speedup, distance travelled, final navigation counters, the
feature ledger, the list of required features the run never reached, the
progression and scaling measurements with the balance findings drawn from them,
and the captures it took.

The runner also reads the captured frames back and prints mean luminance in top,
middle, and bottom screen bands. A run that has gone completely black, or blown
out to white, raises no exception and passes every other check, so the harness
measures the picture rather than trusting it.

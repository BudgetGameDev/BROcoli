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
| `journey` | 4m | Two characters made, resumed from the menu, and died in |
| `tune` | 10m | Real-time lighting tuning; no fast-forward |

### Options

Runner options are `-tier`, `-seed`, `-out`, `-build`, `-timeout`, and
`-keep-frames`. Anything else is handed to the player untouched: `-duration`,
`-interval`, `-timestep`, `-max-frames`, `-scenario`, `-minlevel`, `-tuning`,
`-capture-on`, `-menus`/`-noMenus`, `-features`/`-noFeatures`, and
`-journey`/`-noJourney`. An explicit option always beats the tier preset it
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
- `coverage`: every required feature was exercised;
- `balance`: the run's progression and difficulty stayed in band; and
- `journey`: the run went through every step of making, resuming, and losing a
  character.

Any scenario also fails on a Unity warning, error, assertion, or exception, and
any scenario fails if the run stalls -- two game-minutes without a level, an
experience pickup, or a new room. An agent pinned on something it cannot reach
survives happily and reaches its features, so nothing else here would catch it.

A `coverage`, `balance`, or `journey` run starts a fresh life when the player
dies rather than stopping there. A roguelite run ends in death, and a sweep that
stopped at the first one would only test whatever that life happened to stumble
into.

A `journey` run ends on its last step rather than on the clock. Its subject is a
fixed list of things to do, and the minutes after the last of them would be
nothing but the bot playing on.

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

- explores the nearest room it has not been in, crossing known ones to reach it,
  rather than only ranking the four rooms next door;
- walks out to the open middle of a room it has not cleared before going on, so a
  group waking up behind it does not start the fight in a corner;
- plans routes over the runtime NavMesh and follows path corners around walls;
- probes nearby space before moving and picks a clear alternative direction;
- detects lost progress and performs a bounded stuck-recovery manoeuvre;
- fights at the range of the weapon the player is actually holding, so a spray
  upgrade or boost immediately changes how it kites;
- focuses the enemy it can finish soonest rather than merely the nearest;
- backs off from crowds, from anything inside its danger radius, and when hurt;
- measures how much of the space around it is occupied and, once a crowd has more
  than half of it, breaks out through the widest gap rather than backing away
  from the crowd's middle -- which, in a ring, is where it is already standing;
- writes off a fight nothing is dying in, because holding weapon range against a
  crowd it cannot kill is how a run spends a game-minute walking in a circle;
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
| Seconds per ring | 30 to 200 | the run sprints past the ladder / never climbs it |
| Mean health | 45% to 90% | fought at the edge of death / nothing is a threat |
| Share of the run under 35% health | 2% to 35% | never in trouble / only ever in trouble |
| Deaths per hour | 0.4 to 8 | the run cannot be lost / it cannot be learned |

Depth is graded alongside pacing because it is the axis the rest of the
difficulty hangs off. The ring ladder unlocks archetypes and the depth multiplier
raises health, and a run that circles the rooms it started in meets neither --
while one that sprints outward arrives at archetypes it has no build for. Depth
is charged the way a level is: only a new personal best costs anything, and a
fresh life starts its own descent.

Scaling is then graded on its own, because it fails quietly. A build whose depth
multiplier stopped applying still levels the player on schedule and still kills
them occasionally; what it stops doing is making the tenth room different from
the first, and pacing and pressure read the same on that build as on a working
one. So the run records what the dungeon actually set each room to -- the ring,
the player's power score at that moment, and the depth, health, damage, and count
multipliers the room was built with -- and grades four things from it:

| Measurement | Band | Outside it means |
| --- | --- | --- |
| Enemy health, first room to toughest | 1.5x to 6x | the dungeon is flat / it outruns any build |
| Enemy damage at the hardest | 1.2x to 3x | depth is longer, not harder / it one-shots |
| Difficulty tracking | 0.6 to 1.15 | the player outgrows the dungeon / a treadmill |
| Rooms built against a ceiling | under 35% | -- / scaling has stopped answering the player |

Difficulty tracking is the measurement the other three exist to support. Health
and damage can both grow on schedule and still leave a run trivial, if the build
they are answering grew faster than either of them. It reads enemy threat --
health times damage, because health alone measures how long a fight lasts rather
than how dangerous it is -- against the player's own power score, as an exponent:
enemy threat grows as player power to this power, so a run reports the feedback
strength the game was built with rather than a ratio that means something
different at every power level. One is a treadmill, where every upgrade is
answered in full and none of them is ever felt; near zero is a feedback path that
has stopped applying, or one whose ceilings the run has already reached.

The ring a room sits in is left out of that number and graded as enemy health
growth instead. Depth is somewhere the player chose to walk rather than the
dungeon answering their build, and folding it in would read a run that pushed
deep as a treadmill however hard its upgrades were landing.

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

The knobs a balance finding points at are three files. The experience curve is
`PlayerProgression` (`Runtime/Player/PlayerProgression.cs`); the depth slopes,
power exponents, scaling ceilings, and each archetype's pace are `EnemyScaling`
(`Runtime/Dungeon/EnemyScaling.cs`); and the ladder gating which archetypes a
ring may spawn is `DungeonEnemyPlacer.MinRingFor`. The two name-keyed tables --
which ring an archetype appears in, and how it carries itself when it does --
are meant to be read together.

One thing the dungeon does that is not a multiplier: an enemy is anchored to
where it was placed and gives up about a room away from it, then walks back and
holds its attacks on the way (`EnemyBase.SetLeashHome`). Without it nothing in
the game ever ended a chase, so every enemy a run had woken followed the player
for the rest of the session -- a balance run measured eighty-five alive at once,
and the tenth room was fought with the first nine still in tow. A room is meant
to be a fight that can be won and left behind, and the balance numbers only mean
anything once it is.

## Feature coverage

Combat alone never opens the map. Alongside the agent, a director sweeps the
systems that need deliberate input -- the inventory and map overlays, the pause
menu and its settings pane, and checkpointing -- and records each one only after
checking the game responded. Everything else is recorded where it actually
happens in gameplay code, so the ledger reflects the game rather than the bot's
intentions.

The `coverage` scenario fails the run if a required feature was never reached.
Features that depend on the run's luck -- an elite spawning, a hydra surviving
long enough to split -- are reported but never fail it.

The save probe writes only into a slot that was already empty, deletes it again,
and restores the two preferences it had to move. When all ten slots are taken it
falls back to a serialize/parse round trip rather than evicting a real run, so
the harness can never cost you a save.

## The player's own journey

Everything above plays one life in one dungeon. A player does not: they make a
character, quit to the menu, come back to it tomorrow, start a second one
alongside it, and eventually die. The `journey` tier is the run that does that,
and it is the only run that ever leaves the dungeon.

It walks the run the menu started it in, quits through the pause menu's Main
Menu button, picks the run out of the save list and presses Play, and then checks
that what came back is what left: the same level, the same experience, the same
dungeon seed, the same rooms seen, and the player standing where they stopped. It
parks that character back in the menu, starts a second one from the saves panel,
and does the whole thing again -- and then reads the first character's slot to
confirm the second one never landed on top of it.

Then it dies on purpose. The bot is built to survive, so the screen every real
run ends on is the one nothing in the harness ever reaches; here the run takes a
hit through the entry point an enemy's strike lands on, and the harness reads
what the death cost. Dying drops the run being played, so the check is that the
slot it was in is empty, that the other character's slot is untouched, and that
the game-over screen came up and its Restart button started a fresh life.

Each of those is a ledger entry, and they are what the `journey` scenario is
graded on:

| Entry | What it means |
| --- | --- |
| `menu.shown` | the run started at the main menu |
| `menu.new-game` | a character was made |
| `dungeon.room-entered` | and walked somewhere worth saving |
| `save.checkpointed` | the run wrote a checkpoint of itself |
| `menu.continue` | the save list resumed a run |
| `save.resumed` | the resumed run came back as the run that left |
| `save.slots-independent` | two characters held their own slots |
| `gameover.shown` | the player died and the game said so |
| `save.dropped` | the death cost the run being played |
| `save.survived-another-runs-death` | and cost the other character nothing |
| `gameover.restart` | Restart started a fresh life |

This is the one tier that writes real save slots -- ordinary autoplay keeps
checkpointing switched off so a throwaway bot run cannot claim one of your ten.
It counts which slots were yours before it started, needs two of them free, and
frees every slot it claimed when the run ends, so it costs you nothing either.

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
them.

A run's pictures are a flipbook, not a film. Its interval is capped at
`-max-frames` pictures (120 by default) by coarsening the cadence rather than
stopping early, so the frames still span the whole session: a twenty-minute
coverage sweep photographs itself every ten game-seconds instead of writing
1200 full-screen PNGs. The runner then removes `frames/` once it has read them
back and reported, because a directory of them per run is gigabytes nobody
returns to. `-keep-frames` keeps the flipbook when the run was taken in order to
watch it; the triggered captures, the telemetry, the summary, and the player log
are never removed. Telemetry includes position, health, level, enemy pressure, current
intent, visited rooms, route replans, and stuck recoveries. The summary adds the
tier, the achieved speedup, distance travelled, final navigation counters, the
feature ledger, the list of required features the run never reached, the
progression and scaling measurements with the balance findings drawn from them,
and the captures it took.

The runner also reads the captured frames back and prints mean luminance in top,
middle, and bottom screen bands. A run that has gone completely black, or blown
out to white, raises no exception and passes every other check, so the harness
measures the picture rather than trusting it.

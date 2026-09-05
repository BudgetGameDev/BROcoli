# Fire, autoplay, and release isolation verification

The reusable autoplay package is `com.budgetgamedev.autoplay`; the BROcoli
adapter and its Editor runner are `com.budgetgamedev.autoplay.brocoli`.
The core owns utility selection, feature recording, progression measurement,
time control, and cohort grading. Navigation, controls, upgrade selection,
game observations, and balance targets belong to the adapter.

Release staging removes autoplay packages before Unity imports or compiles
them. Assembly constraints additionally require both a development build and
`GAME_AUTOPLAY` outside the Editor. BROcoli's diagnostic bridge and call sites
are conditionally compiled. Production assemblies have no autoplay reference.

Select products explicitly:

```powershell
python scripts/release-build.py --product brocoli --targets windows --output build/releases/brocoli
python scripts/release-build.py --product launcher --targets windows --output build/releases/launcher
```

The output directory must be empty. Another installed local game package can
be selected by its suffix. Single-game staging removes the launcher and every
other game package, including their Resources. Launcher staging includes all
installed games. The old default-game selection configuration is removed.
Only BROcoli is currently installed; synthetic two-game tests exercise the
other-game exclusion case.

`release-audit.json` records the package and shipped assembly checks. Release
players were also launched with autoplay arguments to verify those arguments
do not install an autoplayer.

Verified Windows URP deliverables:

| Product | Archive | Build logs | Binary audit |
| --- | --- | --- | --- |
| BROcoli | `build/releases/brocoli-final-physics-audited/BROcoli-windows-x86_64.zip` | Zero warnings/errors | BROcoli and shared assemblies; no Hub or autoplay |
| Launcher | `build/releases/launcher-final-physics-audited/GameLauncher-windows-x86_64.zip` | Zero warnings/errors | Hub, BROcoli, and shared assemblies; no autoplay |

Each archive has adjacent checksums and a source snapshot record. ZIP integrity,
payload bytes, and the selected source packages were checked again after the
autoplay-only reaction changes. The 51 Python script tests also pass, including
12 release-isolation regression cases. Other target platforms were not built
in this verification session.

Fire uses a dual-pipeline HDR shader and bounded particles, with turbulent
wisps, a hot core, smoke absorption, soft intersections, and rising embers.
`build/verification/torch-fire-URP.png` and `torch-fire-HDRP.png` are actual GPU
renders. Both fire rendering and scene-linear HDR lighting parity tests pass.

Balance testing uses independent 15-minute seeds and records level pacing,
early/late progression, depth, health pressure, close calls, deaths, and actual
enemy scaling. The cohort requires every seed's individual bands and grades
rare deaths across total exposure. Failed or stalled seeds remain failures.

The first sweeps exposed excessive sustain and stacked projectile damage.
Regeneration rewards changed from 1 to 0.2 HP/s and lifesteal rewards from
2% to 1%. Projectiles now use the same 0.3-second damage-immunity window,
feedback, and death handling as melee. Autoplay navigation also gained
streamed-room first-hop routing, independent combat-stall timing, and clean
navigation memory after respawn.

Replay diagnostics also exposed distant-enemy retreat oscillation, incomplete
NavMesh routes, and a recovery loop that repeatedly renewed a timeout while
aiming at an unreachable room center. Failed routes and recovery targets are
retired explicitly; navigation target and path status are recorded for diagnosis.

A live wall/enemy probe also reproduced a collision-model mismatch: the bot's
old sphere sweep reported free space that the player's capsule and enemy
stand-off rules rejected. The adapter now previews the actual movement
resolver through a development-only API. Release audits reject that API's
metadata, as well as the autoplay assemblies and readiness hook.

An authored-floor regression test covers the baked NavMesh's roughly 8 cm
height offset. Vertical sampling reach is independent of horizontal clearance;
safe inward movement can recover a player slightly outside the mesh. Rejected
movement also advances the stuck detector even when the accepted input is zero.

The user identified a `Shared Railing At Boundary` piece in the running Editor.
The passage plan now opens the short shared-wall ends that join the platform
shell, removing the entrance pockets consistently for geometry, navigation,
and prop placement. The separate cliff boundary remains solid.

Movement-speed saturation is reported separately from health/damage/count
headroom. A speed safety cap does not imply that enemy power has stopped scaling.

A holdout reached level 19 with only five hits over fifteen minutes. Ordinary
late-room melee were limited to 3.2–3.36 m/s against the player's base 4 m/s.
The speed multiplier ceiling increased from 1.6 to 1.85 so these enemies can
approach the existing 3.9 m/s safety ceiling. Opening-room scaling is unchanged.

Headless balance runs explicitly use `--no-capture`; their summaries say
`captureEnabled: false`. They verify simulation and logs, not presentation.
Hidden standalone screenshot capture was unavailable on this host; the
separate GPU rendering fixtures provide the fire presentation evidence.

Accelerated runs pause only while the dungeon explicitly reports pending
geometry/NavMesh streaming. Readiness waits are excluded from simulated time
and recorded in real seconds; a wait lasting 30 seconds fails the run. Invalid
paths and lack of progress are never treated as readiness waits.

Balance runs use an explicit `reference` reaction profile: observations every
0.1 simulation seconds and commands delayed by 0.2 seconds. These are declared
calibration settings, not a claim about measured human performance. The
original immediate-reaction `stress` profile remains available. Reports record
the profile, timing, observation count, and applied decision count; cohort
grading rejects mixed profiles or timing. Physics, collision resolution, and
following an existing route continue at the normal fixed step.

Progression duration and health sampling exclude paused gameplay. Menu control
and restart handling continue during those pauses; otherwise a dead run could
never resume. Earlier reports that included paused capture frames are retained
as diagnostic evidence rather than used as final balance measurements.

These automated bands are reproducible tuning targets. They do not establish
how difficult every human player will find the game.

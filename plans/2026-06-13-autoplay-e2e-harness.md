# Autoplay / E2E Playtest Harness

**Date:** 2026-06-13
**Status:** Phase 1 ✅ done. Phase 2 ✅ done (2026-06-13). Phase 3 documented for direction only.

> ⚠️ **Phase 3 is NOT implemented yet** — it's written down so the design is captured
> and we agree on the destination. It gets its own plan + approval before any work.

---

## Goal

Let an automated agent (Claude Code) **see and iterate on the running game**
without a human at the keyboard, and without depending on macOS Screen Recording
permissions (which are blocked for our terminal because the responsible process
is the detached `zellij` daemon).

The key architectural decision that makes this work:

- **Capture happens inside the engine**, via `ScreenCapture.CaptureScreenshot()`,
  which grabs the game's own backbuffer and writes PNGs to disk. This needs **no
  OS screen-recording permission** — to the agent it's just reading image files.
- **Driving happens inside the engine** too: a bot produces a movement vector fed
  into the existing input pipeline. No synthetic mouse/keyboard, no window focus,
  no Accessibility permission.

## What the codebase already gives us (investigation results)

- **Input System:** `activeInputHandler: 2` (Both). `PlayerInputHandler.UpdateInput()`
  (`Assets/Scripts/gamejam-2022/PlayerInputHandler.cs:49`) reads keyboard +
  virtual joystick and exposes `RawInput`. This is the single injection point.
- **Combat is automatic:** `PlayerController.FixedUpdate()` calls
  `_combat.HandleCombat()` (auto-target + auto-fire). The bot therefore only needs
  to output a **movement vector** — no aiming.
- **Enemy lookup:** `EnemySpatialHash.Instance.GetNearbyEnemies(Vector2 pos, float radius)`
  → `List<EnemyBase>`, plus `EnemyCount`. Enemies are on the `Enemy` layer.
  `EnemyBase : MonoBehaviour` (global namespace).
- **Player:** tag `Player`; `PlayerStats` exposes `CurrentHealth`, `CurrentMaxHealth`,
  `CurrentLevel`, `CurrentExperience`, `CurrentMaxExperience`, `IsAlive`.
  `PlayerDamageHandler.OnGameOver` (event) + `IsGameOver` = our run-end signal.
- **Run-gating to handle:**
  - `GamePreloader` sets `Time.timeScale=0` then `=1` at startup (preload gate).
  - **`LevelUpScreen` sets `Time.timeScale=0` and waits for a click**
    (`LevelUpScreen.cs:171`). An autonomous run MUST auto-pick or it stalls forever.
    `Show()`, `Hide()`, `IsShowing()` are public; `SelectUpgrade(int)` is private.
  - `ForceLandscapeAspect` sets `timeScale=0` while the window is portrait → we
    force a landscape resolution at boot.
- **Scenes (build order):** `MainMenuScene`(0) → `Game`(1) → `EndGame`(2).
  Autoplay jumps straight to `Game`.
- **Determinism:** spawners use `Random.insideUnitCircle` → seed
  `UnityEngine.Random.InitState(seed)` at boot (best-effort in Phase 1).
- **Build tooling:** Unity `6000.3.6f1`. On this machine the editor lives under
  `~/Applications/Unity/Hub/Editor/...` (the Hub "secondary install path"), NOT
  `/Applications/...` — the existing `scripts/unity-build-check.sh` assumes the
  latter, so our runner checks both.

---

## Phase 1 — Minimum viable autoplay + capture (THIS PASS)

**Outcome:** one Bash command launches the built game in autoplay mode; a bot
survives a level; the game writes a PNG frame sequence + a JSONL telemetry log to
a folder; the agent reads those and iterates. Zero OS permissions involved.

### Hooks into existing code (minimal, low-risk)

| File | Change |
|---|---|
| `Assets/Scripts/gamejam-2022/PlayerInputHandler.cs` | At the top of `UpdateInput()`: if `BotDriver.Active`, set `_rawInput`/`_smoothedInput`/`_lastNonZeroInput` from `BotDriver.Move` and return. (~5 lines) |
| `Assets/Scripts/LevelUpScreen.cs` | Add one public method `AutoSelectUpgrade(int) => SelectUpgrade(int)` so the resolver can pick an upgrade. (1 line) |

> Both hooks are inert during normal play (the bot flag is false), so they cannot
> affect a human playthrough.

### New files

| File | Role |
|---|---|
| `Assets/Scripts/Autoplay/AutoplayController.cs` | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` entry. Parses CLI args / env vars. If autoplay: `runInBackground=true`, seed Random, force landscape resolution, `LoadScene("Game")`, and on Game load wire up the bot + capture + telemetry + level-up resolver. **No-op if autoplay not requested.** |
| `Assets/Scripts/Autoplay/BotDriver.cs` | Static `Active`/`Move`. Each `FixedUpdate`: query nearby enemies, steer away from the closest threats with a gentle pull toward arena center, output a normalized move vector. Exercises movement, combat, spawns, leveling, and distance-lighting on approaching enemies. |
| `Assets/Scripts/Autoplay/LevelUpAutoResolver.cs` | In `Update` (runs even at `timeScale=0`): if `LevelUpScreen.IsShowing()`, call `AutoSelectUpgrade(randomChoice)`. |
| `Assets/Scripts/Autoplay/FrameCapture.cs` | Coroutine on **unscaled** time → `ScreenCapture.CaptureScreenshot(out/frames/frame_#####.png)` every `interval` seconds. |
| `Assets/Scripts/Autoplay/RunTelemetry.cs` | Coroutine on unscaled time → append JSONL (`t, pos, hp, maxHp, level, xp, enemyCount, fps, timeScale, levelUpShowing`). Subscribe `Application.logMessageReceived` to count/record warnings + errors + exceptions. End the run on `OnGameOver` **or** when `--duration` elapses → write `summary.json`, then quit (`Application.Quit()` / stop play mode in editor). |
| `Assets/Scripts/Editor/AutoplayBuildScript.cs` | `public static void BuildAutoplayPlayer()` for `-executeMethod`; builds `StandaloneOSX` → `Build/BROcoli-autoplay.app`. Also exposed as a menu item so it can be triggered from an already-open editor. |
| `scripts/autoplay-run.sh` | Wrapper: optional `--build` (batchmode build, requires editor closed), then launch the player binary with `--autoplay --seed --duration --out`, wait for exit, print the output dir for the agent to read. Resolves Unity under both `/Applications` and `~/Applications`. |

### CLI args / env vars (both supported)

| Arg | Env | Default | Meaning |
|---|---|---|---|
| `--autoplay` | `BROCOLI_AUTOPLAY=1` | off | enable the harness |
| `--seed=N` | `BROCOLI_SEED` | `12345` | RNG seed |
| `--duration=S` | `BROCOLI_DURATION` | `60` | max run seconds (real time) |
| `--out=PATH` | `BROCOLI_OUT` | `./AutoplayRuns/<timestamp>` | output dir for frames + telemetry |
| `--interval=S` | `BROCOLI_INTERVAL` | `0.5` | seconds between captures/telemetry samples |

### The loop

```
one-time (editor CLOSED):
  Unity -batchmode -quit -projectPath <repo> -executeMethod AutoplayBuildScript.BuildAutoplayPlayer

each run (agent, via Bash — no permissions needed):
  Build/BROcoli-autoplay.app/Contents/MacOS/BROcoli --autoplay --seed=42 --duration=60 --out=/tmp/run-1
    -> game self-quits
  agent reads /tmp/run-1/frames/*.png + /tmp/run-1/telemetry.jsonl + summary.json
    -> analyze -> edit code/scene -> rebuild -> rerun
```

### Caveats (Phase 1)

1. **Editor lock:** a batchmode *build* needs the editor **closed** (Unity = one
   instance per project). Runs of the built `.app` do not. The build menu item lets
   you build from the open editor when convenient.
2. **Rebuild cost:** code changes need a rebuild (~minutes). Scene/lighting-only
   tweaks could be made hot-loadable later (Phase 3), not now.
3. **Determinism is best-effort:** seeding global `Random` covers spawn positions,
   but frame-rate-dependent physics may still drift run to run. Hardened in Phase 2.
4. **Verification** of compilation requires the editor closed (or focusing the open
   editor to recompile), because we can't run a second Unity instance against a
   locked project.

### Phase 1 done-when

- A built player launched with `--autoplay` reaches the `Game` scene unattended,
  the bot moves and survives for the duration (or dies), the level-up popup never
  stalls the run, and a frames folder + telemetry JSONL + summary are produced and
  readable by the agent.

---

## Phase 2 — Deterministic, assertable E2E (NOT THIS PASS)

Turns "watch it play" into "test it."

- **Deterministic mode:** fixed `Time.fixedDeltaTime`, optional fixed
  `Application.targetFrameRate`, seed *all* RNG sources (spawner, level-up troll
  rolls, crit rolls), and a scripted/seeded wave schedule so two runs match.
- **Structured assertions:** a `--scenario=<name>` flag selecting predefined
  checks, e.g. "survive 60s", "reach level 5", "no exceptions logged",
  "≥ N enemies rendered within the lit zone (y < threshold)". Exit code reflects
  pass/fail so CI / the agent can gate on it.
- **Richer telemetry:** per-frame enemy positions + screen-space luminance samples
  so lighting/fog-of-war can be measured numerically, not just eyeballed.
- **Run manifest:** `summary.json` gains pass/fail, seed, git SHA, scenario,
  timings, error list — a self-describing artifact.

## Phase 3 — Smart bot, video, one-command pipeline (IN PROGRESS)

Quality-of-life + fidelity.

**Done (2026-06-13):** smarter bot (engage/kite/edge-avoid so combat + leveling
actually happen); hot-reload lighting tuning — `RuntimeTuning` watches a JSON
(`--tuning=PATH`) and live-applies world-light intensity, player fill factor,
light height, and ambient, via `scripts/autoplay-tune.sh` (tune lighting with no
rebuild).
**Remaining:** video/montage capture (needs ffmpeg or Unity Recorder — not
installed); one-command build→run→montage→summarize pipeline + optional CI job.

- **Smarter bot:** threat prioritization, XP/pickup seeking, kiting, edge
  avoidance — playthroughs that resemble real play, parameterized by a "skill"
  level.
- **Video capture:** Unity Recorder (or PNG sequence → `ffmpeg`) to produce an mp4
  per run for humans to watch; agent still analyzes sampled frames + a contact-sheet
  montage.
- **Hot-reload tuning:** the running player watches a JSON file for lighting/balance
  params and applies them live, so visual tweaks don't require a rebuild.
- **One command:** `scripts/autoplay.sh` does build → run → collect → montage →
  summarize, and (optionally) closes/reopens the editor around the build
  automatically. Optional CI job that runs the Phase-2 scenarios on PRs.

---

## Out of scope (all phases)

- Solving the macOS Screen Recording / zellij permission issue (this harness makes
  it unnecessary for game visuals).
- Headless `-nographics` runs (we need rendering to capture frames).

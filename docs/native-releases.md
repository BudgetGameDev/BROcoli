# Native desktop releases

Native desktop players are an explicit, local release operation. They are not
part of `ci.sh`, `cd.sh`, the pre-push hook, or a GitHub Actions trigger. The
automatic build and deployment path remains WebGL-only.

## Prerequisites

Run the multi-platform tooling on macOS because Unity can only produce the
macOS player on a Mac. Install the project editor version and its Windows and
Linux Mono build support modules:

```bash
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"
unity install-modules --editor-version "$UNITY_VERSION" \
    --module windows-mono --module linux-mono --yes --accept-eula
```

The scripts also require Python 3, `git`, `ditto`, `zip`, `tar`, and `shasum`. Publishing
requires an authenticated GitHub CLI (`gh auth login`). The source Unity Editor may stay open; each release builds a separate staging
project with a fresh Library by default. Use `--reuse-stage` (PowerShell: `-ReuseStage`)
to retain an isolated workspace and its caches. See [HDRP build iteration](hdrp-build-iteration.md)
for shader stripping, scripts-only iteration and cached release builds.

Windows has a PowerShell-native Windows-only builder that requires `unity`, Python 3,
and `git`; archive creation and SHA-256 checksums use the installed .NET runtime.
Windows Mono player support is bundled with the Windows Editor, so install the
matching editor before running the build:

```powershell
$UnityVersion = (Select-String ProjectSettings\ProjectVersion.txt `
    -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
unity install $UnityVersion --yes --accept-eula
.\scripts\native-builds.ps1 -Product brocoli
```

Open the project afterwards in the automated mode expected by the repository's
live Unity tooling with `.\scripts\unity-open.ps1`.

## Build all native players

```bash
./scripts/native-builds.sh --product brocoli
```

Native builds default to URP on every desktop platform. To explicitly build HDRP,
use `.\scripts\native-builds.ps1 -Product brocoli -RenderPipeline hdrp` on Windows or
`./scripts/native-builds.sh --product brocoli --targets windows --pipeline hdrp`. Direct Unity build
entry points accept `-renderPipeline urp|hdrp`. Windows HDR10 output works with
either rendering pipeline.

Builds include common scenes and only the selected pipeline's rendering scenes.
Incompatible quality tiers are excluded for the build and the authored settings
are restored afterwards. The Editor's active quality tier also switches to a
compatible tier during the build: leaving an HDRP tier active disables URP shader
stripping, causing hundreds of thousands of unnecessary variants to compile.
The original active tier is restored afterwards. Shared HDRP-only Resources are
excluded before importing URP staging projects. URP player compilation excludes the game's HDRP front end;
the HDRP runtime DLLs are filtered from the player. The HDRP package remains
installed for editing and explicit HDRP builds, so Unity can still import or compile
its package scripts. Shader log entries with zero remaining variants do not add
compiled HDRP shader programs to the player.

Use `--development` only for local diagnostics. The build script produces and
packages:

- `BROcoli-windows-x86_64.zip`
- `BROcoli-macos-universal.zip`
- `BROcoli-linux-x86_64.tar.gz`
- `SHA256SUMS` and `build-info.txt`

Artifacts are written under `build/native/brocoli/artifacts/`. The macOS build is a
universal Intel/Apple-silicon app. Windows and macOS retain the native HDR
configuration; Linux uses Vulkan with OpenGL Core as a fallback.

Pass `--targets` to build a subset, for example `--targets windows`. The
artifacts directory is cleared first, so it always holds exactly the players
the last run selected, and `build-info.txt` records that selection. A subset
that omits macOS does not need a macOS host.

On Windows, `native-builds.ps1` always selects the Windows player and accepts
`-Development` for a diagnostic build. It produces the same Windows archive,
`build-info.txt`, and `SHA256SUMS` as the shell builder, so the existing release
verification and publishing scripts can consume its output.

`build-info.txt` also carries a `build_id` of the form
`<UTC timestamp>-<short commit>`. Rebuilding the same commit produces a new
id, which is what lets a rolling release name the specific build it is serving.

## Select the binary's contents

`-Product` / `--product` is required. Use `brocoli` for BROcoli alone, another
installed game package's suffix for that game alone, or `launcher` for the
launcher and all installed games. A single-game binary starts at its own menu
and hides the All Games button. The old default startup-game config is removed.

The portable entry point also builds WebGL:

```bash
python scripts/release-build.py --product brocoli --targets windows --pipeline hdrp
python scripts/release-build.py --product launcher --targets windows
python scripts/release-build.py --product brocoli --targets webgl
python scripts/release-build.py --product brocoli --stage-only
```

Direct builds require an empty output folder (`--output <path>`). Packaging
wrappers safely replace their generated outputs under `build/native/<product>/`.
The generated `BuildContent.json` records the exact imported package allowlist.
`release-audit.json` records shipped assemblies and exclusion checks; native
players also contain `build-content.json`. Preserve these with release artifacts.

Every release copies only the selected game packages and shared dependencies to
a fresh project before Unity opens it. The launcher package is absent for a
single game. Both autoplay packages are absent from every release, including a
launcher release: no autoplay sources enter player compilation or linking. All
game-owned assets must stay inside their game package so Resources follow the
same exclusion. Tests exercise another synthetic game as well as BROcoli.

A release attempted through the source project's Build Settings or a custom
BuildPipeline caller fails with instructions to stage it. Development builds
remain available in the source Editor. The build gate also rejects forbidden
player assemblies, and the post-build audit examines Mono/IL2CPP metadata for
autoplay driver types. Autoplay development players require the dedicated
adapter build command described in the autoplay documentation.

Native players apply shared performance settings before loading a scene, for both
URP and HDRP: VSync off, no software FPS cap, one queued GPU frame, rendering every
frame, 120 Hz physics with a four-step catch-up budget, dynamic input updates, and
240 Hz polling for devices that Unity polls. Changing quality re-applies frame
pacing without changing rendering quality. Existing saved Unity quality choices
can override the build's default tier; these no longer re-enable VSync at startup.
Web/mobile and the Editor retain their own settings.

The player logs its effective settings as `[NativePerformance]`. Launch with
`-frameTimingReport` to also report five-second frame-time samples, focus, and batch
mode. Measure with a visible game window: an occluded or hidden window can skip GPU
work, so its loop rate is not a gameplay FPS benchmark. A 240 Hz display does not
guarantee 240 rendered FPS when the scene exceeds its CPU/GPU budget.

## Publish a GitHub Release

Commit the release contents, create and push a tag, then run:

```bash
git tag v1.2.3
git push origin v1.2.3
./scripts/native-release.sh v1.2.3
```

The publisher requires the tag to point at the current clean checkout. It
builds all three players, verifies that the packages came from that exact
commit, pass their recorded SHA-256 checksums, and are not development builds,
then creates the GitHub Release with generated notes. Useful options include
`--draft`, `--prerelease`, `--notes-file <path>`, and `--skip-build`. A tagged
release must carry all three players.

## Publish the rolling development release

`./scripts/dev-release.sh` maintains one prerelease that is meant to be
overwritten. It publishes only the Windows player by default:

```bash
./scripts/dev-release.sh
```

Each run rebuilds the selected players, force-moves the `nightly` tag to
HEAD, removes every asset currently attached to the release, and uploads the
new ones. The release URL and the per-asset download URLs stay the same, so a
link to the dev build keeps working while the build behind it changes. Publish
more platforms with `--targets windows,linux`; the assets that platform set no
longer covers are dropped from the release rather than left behind stale.

Published players carry the channel in their name, so a downloaded file still
says where it came from: `BROcoli-windows-x86_64-nightly.zip`. The packaged
artifacts under `build/native/brocoli/artifacts/` keep their generic names, because the
same build can go to another channel; the renamed copies are staged under
`build/native/publish/` with a `SHA256SUMS` whose entries are the verified sums
under the published names. The release is titled after its tag and its notes
name the `build_id`, so two downloads of the same URL can be told apart.

The publisher still requires a clean checkout, and requires HEAD to be pushed
to its origin branch, because the tag must name a commit others can fetch.
Unlike the tagged release it accepts `--development` players and records that
in the release notes. `--tag <name>` publishes a different rolling channel, and
`--skip-build` reuses the already-packaged artifacts.

Do not point this at a tag that a tagged release already uses: the tag is
force-moved and the assets are replaced.

## Streamline diagnostics

Windows URP and HDRP players share the Streamline bridge. Run the shared package's
`Tools~/Streamline/setup.py --pipeline both --unity-plugin-api <Editor/Data/PluginAPI>`
after native changes. The player build writes `streamline-project-id.txt` beside
the executable from Unity's product GUID; ship it with the NVIDIA DLLs. NGX needs
this project identity even though `slInit` itself can succeed without it.

The bridge checks NVIDIA Authenticode signatures on all NVIDIA DLLs, and applies
the SDK's additional Streamline signature check only to `sl.*.dll`. NGX's signed
`nvngx_dlss*.dll` files do not carry that additional Streamline signature.

NVIDIA settings > Copy Debug captures fresh counters and actual current-session
`bridge.log`, Streamline/NGX `.log` and `.txt` files, and the Unity player log.
Session folders are under `%LOCALAPPDATA%/BudgetGameDev/Streamline/<executable>/<pid>-<uptime>`.
Logging starts before signature checks. Copies include up to the last 256 KiB per
file and 2 MiB total, with file paths and truncation/omission notices. No SDK log
file is represented as evidence of GPU execution merely because it exists.

For automatic capture while reproducing a player issue, launch with
`-nvidiaDiagnostics C:/absolute/path/nvidia-debug.txt`. This opt-in export refreshes
every five seconds and on normal exit. For the HDRP quality 9 preset, also pass
`-screen-quality "HDRP RT Ultra"`; a previously saved quality preference otherwise
can select a lower tier. Test gameplay in a focused window and check SR dispatches,
SDK extra-present evidence, and fresh Reflex reports, not only feature availability.
`-nvidiaDiagnosticsScene Brocoli_Dungeon_Common` additionally enters the included dungeon scene after startup for a reproducible rendering check; it requires `-nvidiaDiagnostics`.

Add `-nvidiaDiagnosticsBuffers` to capture one pair of DLSS input/output PNGs.
These are raw linear color buffers clamped to PNG range, before final HDR output
mapping. `-nvidiaDiagnosticsSpatialOnly` keeps the negotiated rendering resolution
but bypasses the native SR dispatch for comparison; use it only with diagnostics.

HDRP must configure dynamic resolution before it calculates camera, depth and
lighting constants. Its later IUpscaler negotiation preserves the actual hardware
resolution, including driver rounding (for example, 70% instead of the requested
66.7%). Changing that size afterward produces blue missing regions and stripes
in the input image even when the native DLSS dispatch is bypassed. URP negotiates
its input size earlier and continues to use the optimal DLSS dimensions directly.

Desktop players keep VSync disabled, use the monitor's native borderless display
mode and refresh rate, and leave rendered FPS uncapped across quality changes.
The simulation catch-up limit is 333 ms: the old 33 ms clamp discarded game time
on ordinary HDRP frames below 30 FPS, producing slow motion independently of DLSS,
frame generation or Reflex. Physics still runs at 120 Hz.

The top-right performance overlay is on by default. Settings > Performance
Overlay toggles it in both the main menu and pause menu. Its ten-second rolling
statistics show rendered FPS, average frame time, P99 frame time, and 1% low FPS
(the reciprocal of the mean of the slowest 1% of frame durations), plus a
frame-time graph and the current refresh/VSync/cap state. These are application
frames, excluding generated presents. Reflex defaults to On + Boost; explicit
saved user preferences remain respected.

With `-frameTimingReport`, the player logs frame pacing and `simulationRatio`
every five seconds. During focused, unpaused gameplay this should be about 1;
loading hitches over the catch-up limit and deliberate pausing lower it.

The performance overlay also samples Windows resource counters once per second
on a background thread: CPU usage for the system and game (normalized across
logical processors), game GPU usage and dedicated video memory, system RAM
usage/capacity and the game's resident RAM, plus the busiest physical disk's
active percentage and total physical-disk read/write MiB/s. GPU usage follows
[Task Manager's per-process busiest-engine convention](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/).
Unavailable or stale counters display N/A. Other desktop platforms attempt to
report game CPU and resident RAM; the Windows-only counters display N/A.
Disk and system RAM include other apps.

The frame-time graph scrolls at up to 60 Hz with a shaded line and grid. Each
50 ms bin preserves its highest frame time, rather than averaging hitches away.
The axis expands immediately for a spike and eases down after it leaves the
ten-second history. Focus changes clear the graph and frame statistics.

VRAM has its own percent-used/available/system-used/game-used section. NVIDIA's
driver-provided NVML supplies total, available and system-used dedicated memory
for the uniquely matching rendering adapter; PDH supplies the game's dedicated
allocations. NVML's system usage includes driver-reserved memory. NVML also
supplies GPU temperature, independently of the Streamline feature toggles.
Unsupported adapters or ambiguous duplicate adapter names report N/A for NVML
readings instead of choosing another GPU's values.

Disk temperature uses the read-only Windows StorageDeviceTemperatureProperty
query at five-second intervals: the hottest physical drive's composite sensor
(or hottest valid sensor when no composite exists). CPU, RAM and system/board
temperatures come from fresh Libre Hardware Monitor readings when exposed;
otherwise they remain N/A. The verified machine exposes GPU and NVMe
temperatures without elevation.

Overlay values use green/yellow/red indicative colors. Rendered FPS and 1% low
are green at 60 FPS or above, yellow at 30–59 FPS, and red below 30; frame times
and graph segments use the corresponding 16.67/33.33 ms boundaries. CPU/GPU/disk
utilization and RAM/VRAM pressure turn yellow at 80% and red at 95%, indicating
remaining capacity, not hardware faults (a fully utilized GPU can be expected).
Memory byte values share the corresponding system RAM/VRAM pressure color.
GPU temperature turns yellow at 80°C and red at 90°C; disk temperature at
60°C and 70°C. These are UI heuristics, not manufacturer thermal limits.
Capacity and disk transfer rates stay neutral; unavailable/stale values are gray.

Game disk space shows the containing volume's used percentage, used/total GiB,
and free GiB, sampled on the background worker. Windows resolves the actual game
data directory, including mounted volumes and UNC paths. This is whole-volume
occupancy, separate from disk active time and transfer rates. It turns yellow
at 80% used and red at 95%; inaccessible volumes show N/A. Free space is the
volume's total free space, before any per-user storage quota.

Settings > System Readiness (main menu and pause menu) opens a short, unsaved
gameplay benchmark. The same player loads a deterministic dungeon, automatically
walks a NavMesh route with damage disabled, warms up for five seconds, and
measures twenty seconds without changing graphics/display/NVIDIA preferences.
The original scene and an in-memory run checkpoint are restored afterward;
the original save slots are never used for benchmark progress. The save barrier
blocks writes, deletion, slot selection and migration before any scene unload,
and stays armed through cancellation/failure until restoration finishes.
Autosaves stay suspended while the readiness page is open. Quitting during the
test leaves the on-disk saves as they were before the benchmark.

Results cover the entire measured period, with average rendered FPS, mean and
P99 frame time, 1% low, route distance, and averages/peaks of fresh OS samples.
The readiness baseline is 60 rendered FPS; generated presents are excluded.
CPU/RAM/VRAM/disk capacity pressure and available GPU/disk temperatures receive
nominal/caution/attention statuses and contextual recommendations. Missing
sensors are marked Not measured. High GPU usage alone is not a fault; aggregate
CPU usage cannot diagnose a single-thread bottleneck. This short check does not
certify component health, thermal stability or long-session FPS.
Focus loss, pausing, changed settings, an incomplete route, or insufficient
samples abort assessment. ESC/Cancel returns safely. COPY RESULTS copies the
report; `-readinessReport <path>` also exports completed/cancelled results for QA.

The readiness page's SENSORS button lists all readings discovered by Libre
Hardware Monitor 0.9.6: temperatures, clocks, loads, voltages, fans, power,
memory, storage, network and supported attached controllers. Each entry shows
its device, type, unit and availability. Missing device groups, provider errors,
actual process elevation and PawnIO driver registration are shown separately.
Administrator access can unlock low-level providers, but cannot replace missing
drivers or hardware support. CPU temperatures of zero and out-of-range values
are invalid, and readings older than ten seconds are excluded from assessments.

Run `python scripts/build-hardware-sensors.py` before a Windows release build.
It publishes a self-contained .NET 8 x64 helper with locked NuGet dependencies
and third-party notices. Release staging carries the payload; Unity copies it
after the build into `BROcoli_Data/StreamingAssets/HardwareSensors` so its DLLs
are never imported as Unity plugins. The helper inherits the player's token,
probes in a separate process every two seconds, and exits with its parent.
The game requests no elevation, installs no driver, and changes no sensor
controls. A hung provider times out after thirty seconds. COPY RESULTS on the
sensor view copies the complete inventory; the overlay and readiness assessment
use fresh CPU/RAM/board temperatures from the same stream.

The sensor helper also attempts read-only NVMe SMART/Health Information Log
queries and Windows storage failure prediction (ATA SMART summary where the
driver exposes it), every ten seconds. Physical drive IDs/model names and volume
extents distinguish game and Windows drives from other/unmapped devices. The
Sensors page and readiness report show the source, unavailable/access-denied
responses, critical-warning mask, spare/threshold, estimated endurance consumed,
media errors, error-log entries, unsafe shutdowns, power-on hours and units written.
NVMe lifetime counters preserve all 128 bits; raw NVMe/vendor payloads are retained
in the helper JSON (`HardwareSensors.exe --smart-once`). Vendor-specific ATA
attributes are not assigned universal thresholds; available summary coverage is
explicit. USB/RAID controllers may hide SMART even with administrator access.

Critical warnings, a drive-predicted failure, spare below the drive's threshold,
or >=100% estimated rated endurance consumed require attention. >=80% endurance
or historical media/data-integrity errors produce caution; 80% is a UI heuristic.
Endurance consumed is not proof of imminent failure. Unsafe-shutdown and generic
error-log counts alone do not fail health. Recommendations prioritize backups
and vendor diagnostics for SMART warnings; disk cleanup only addresses capacity.
Unavailable/stale results never become nominal. Only fresh snapshots (<=30 s old)
observed during measurement count, and the worst observed per-drive status is
retained through the report. No SMART enabling, self-test, repair, or write command
is issued. Definitions follow Microsoft's NVMe health log and storage query APIs:
https://learn.microsoft.com/en-us/windows/win32/fileio/working-with-nvme-devices
https://learn.microsoft.com/en-us/windows/win32/api/nvme/ns-nvme-nvme_health_info_log

Clock telemetry adds CPU MHz (LHM core/effective average when valid, otherwise
explicitly labeled Windows-reported power-state frequency), matched GPU core and
memory MHz, NVML maximum core frequency and clock-limiting reason bits. Windows
reported MHz may remain nominal instead of reflecting effective boost clocks.
System memory shows live clocks when exposed, plus separately labeled SMBIOS
configured DDR rate (MT/s) and its derived configured clock (rate / 2 in MHz).
Each DIMM's configured rate is compared with that DIMM's firmware capability;
these are configuration observations, not measured bandwidth or memory tests.

Readiness reports whole-run average/range clocks, disk read/write MiB/s, and
active-transfer latency from Windows PDH. All-disk throughput/latency includes
background processes; no synthetic file writes or maximum-speed test is run.
Repeated low clocks (<50% reference) under >=80% CPU or >=90% GPU load, combined
with <60 rendered FPS, produce caution; CPU OS limits below 80% reference also
count. At least three samples are required. GPU thermal-limiting flags independently
produce caution; idle and normal power-limit flags do not automatically fail.
Repeated >=20 ms transfers at >=10 IOPS plus low FPS produce an I/O caution,
not a physical disk-health diagnosis. Idle I/O is Not exercised; unloaded clocks
are Observed. Hardware SMART/temperature assessments remain separate.

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
are restored afterwards. URP player compilation excludes the game's HDRP front end;
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

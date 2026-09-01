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

The scripts also require `git`, `ditto`, `zip`, `tar`, and `shasum`. Publishing
requires an authenticated GitHub CLI (`gh auth login`). Close the Unity Editor
before starting a multi-target build so one batch Editor can switch platforms
safely.

Windows has a PowerShell-native Windows-only builder that requires only `unity`
and `git`; archive creation and SHA-256 checksums use the installed .NET runtime.
Windows Mono player support is bundled with the Windows Editor, so install the
matching editor, then close it before running the build:

```powershell
$UnityVersion = (Select-String ProjectSettings\ProjectVersion.txt `
    -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
unity install $UnityVersion --yes --accept-eula
.\scripts\native-builds.ps1
```

Open the project afterwards in the automated mode expected by the repository's
live Unity tooling with `.\scripts\unity-open.ps1`.

## Build all native players

```bash
./scripts/native-builds.sh
```

Use `--development` only for local diagnostics. The build script produces and
packages:

- `BROcoli-windows-x86_64.zip`
- `BROcoli-macos-universal.zip`
- `BROcoli-linux-x86_64.tar.gz`
- `SHA256SUMS` and `build-info.txt`

Artifacts are written under `build/native/artifacts/`. The macOS build is a
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

Individual player builds are also available in Unity under
`Tools > Build > Native`.

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
artifacts under `build/native/artifacts/` keep their generic names, because the
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

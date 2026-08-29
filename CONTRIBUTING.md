# Contributing

Thank you for your interest in contributing to this project!

## Contributor License Agreement (CLA)

> By submitting a pull request or contribution, you agree to the [Contributor License Agreement (CLA)](./CLA.md).

This means you permit us to use, license, and relicense your contributions — including under commercial terms — as outlined in the CLA.

You **do not need to sign anything** — submitting a PR or contribution implies agreement to the CLA terms.

## Verification

Run the complete local gate before pushing:

```bash
./ci.sh
```

It runs pinned C# and Python formatting checks, Python and shell linting, the
host Python and Node unit tests, local Semgrep static-analysis rules, the
300-line source-size ratchet, Unity EditMode tests, a release WebGL player
build, and desktop and iOS-profile smoke probes. `Assets/csc.rsp` promotes every
C# compiler warning to an error, so the player build is also the authoritative
compilation check.

Install the prerequisites once:

- .NET SDK 8 or newer (`dotnet`)
- Astral `uv`
- ShellCheck
- `shfmt`
- Unity CLI (`unity`)
- Unity matching `ProjectSettings/ProjectVersion.txt`
- Chrome or Chromium

The pinned CSharpier, Ruff, and Semgrep versions are restored on demand. Apply all
formatters with `./format.sh`.

### Test coverage

`ci.sh` runs the `scripts/tests` Python suite, the Node WebGL template and
service-worker suites, and the Unity EditMode test assemblies, then launches the
built WebGL player in headless Chrome with desktop and iOS browser profiles. The
`scripts/autoplay-*.sh` desktop-player harness remains opt-in because it requires a
separate desktop player build.

Enable the repository-managed pre-push hook with:

```bash
./scripts/install-git-hooks.sh
```

The hook runs `./ci.sh` when a push updates `staging` or `production`, and blocks
that push if any gate fails. When every gate passes it then runs `./cd.sh` for
each of those branches, publishing the player it just built. Pushes exclusively
to other branches or tags skip both; `dev` is deliberately ungated, so this runs
once at promotion rather than on day-to-day work. Git hooks are local and are not
activated merely by cloning the repository, which is why the installer is
required once per clone.

### 300-line source-file limit

New first-party source files hard-fail above 300 physical lines. The project already
had 35 larger files when the rule was introduced, so
`.quality/loc-baseline.tsv` records their current ceilings. Those files may shrink
but may not grow, and the baseline entry must be removed once a file reaches 300
lines. This is a ratchet, not a general exemption.

### Unity compilation and player verification

`ci.sh` uses `scripts/unity-webgl-build.sh` to create `build/WebGL` on the host. The
wrapper reuses a connected automated Editor when one is available and otherwise
uses `unity build` with the editor version from
`ProjectSettings/ProjectVersion.txt`. Against a connected Editor it queues the
pipeline's asynchronous `build` command and polls `build_status` for the
BuildReport, because the pipeline will not hold the Editor's main thread for the
length of a player build; the batch-mode path still runs
`WebGLBuildScript.Build`. Both produce a release WebGL player from the enabled
build-settings scenes, and `scripts/check-webgl-build.cjs` plus the two smoke
profiles verify the artifact either way. `scripts/unity-test-check.sh` picks its
Editor the same way, through the shared
`scripts/unity-editor-connection.sh` helper: an open Editor holds the project
lock, so the gate drives the attached instance rather than starting a second one
that cannot open the project. The build resolves packages, imports assets,
compiles scripts, and creates the player exercised by both smoke profiles.
If the project is already open, launch that Editor with `unity-open` so the gate can
drive the build safely.

The standalone compile-only check remains available as
`./scripts/unity-build-check.sh` or `.\scripts\unity-build-check.ps1`, but it does
not replace the full local gate.

## Deployment

`./cd.sh <staging|production>` publishes the player `./ci.sh` built. Staging goes
to the `BranchStaging` and `BranchMain` folders on `gh-pages`; production goes to
the Pages root and keeps both staging folders. It generates `version.json` and
the staging manifest exactly as the hosted job did, then replaces `gh-pages` with
a single root commit, because every deploy rewrites roughly 90MB of player and
keeping that history would grow the repository without bound. The source commit
behind any deploy stays recorded in `version.json`.

`cd.sh` refuses to publish anything `ci.sh` has not passed for. A green run
writes `build/ci-pass.json` recording the commit, the working tree, and the built
player; `cd.sh` recomputes all three and stops unless they still match, so
neither a stale pass, an edit made afterwards, nor a hand-run `./cd.sh` can reach
Pages. `ci.sh` deletes that receipt before its first gate, so an interrupted or
failing run leaves no pass behind.

The GitHub Actions Pages build is now a manually dispatched fallback for when
this host is unavailable or a clean-room build is wanted. Nothing deploys on
push, which also keeps two writers from racing for `gh-pages`.

Do not use `dotnet build` as the repository verification step. Unity generates the
gitignored `Assembly-CSharp*.csproj` files locally, so they are absent from a clean
checkout and their package references can be stale or machine-specific. A local
`dotnet build` may provide optional quick feedback after Unity has regenerated those
files, but a successful result does not replace the Unity compilation check.

`Packages/manifest.json` declares the direct Unity dependencies and the tracked
`Packages/packages-lock.json` pins the resolved dependency graph. Preserve both when
diagnosing compilation failures. An error under `Library/PackageCache` can indicate
an incompatible package/API combination as well as a damaged local cache; deleting
`Library/` is a cache reset, while changing or regenerating the lockfile is a package
update that must be reviewed and verified separately.

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

It runs pinned C#, Python, JavaScript, and PowerShell formatting checks; strict
Python, JavaScript, shell, and PowerShell linting; a Python type check; the host
Python and Node unit tests; local Semgrep static-analysis rules; the
300-line source-size ratchet, Unity EditMode tests, the game-runtime coverage
ratchet, a release WebGL player build, and desktop and iOS-profile smoke probes. `Assets/csc.rsp` promotes every
C# compiler warning to an error at the compiler's maximum warning level. Unity
tests reject unexpected log messages, player builds reject BuildReport warnings,
and the smoke probes reject application and Unity runtime warnings and errors. The
smoke harness documents two exact Chromium engine diagnostics it excludes: Unity's
handled WebGL2 format probes and headless audio's lack of a user gesture.

Install the prerequisites once:

- .NET SDK 8 or newer (`dotnet`)
- Node.js 20.19 or newer (`node` and `npx`)
- Astral `uv`
- ShellCheck
- `shfmt`
- PowerShell 7 (`pwsh`)
- Unity CLI (`unity`)
- Unity 6.5 matching the exact editor version in `ProjectSettings/ProjectVersion.txt`
- Chrome or Chromium

On macOS, `./scripts/bootstrap-macos.sh` installs those prerequisites together
with the Editor and modules this project builds with, then runs the two
per-clone installers below; `.\scripts\bootstrap-windows.ps1` does the same on
Windows. See [docs/machine-setup.md](docs/machine-setup.md) for what they do,
what a clone already carries, and how to set up another machine by hand.

The pinned CSharpier, Ruff, mypy, ESLint, Prettier, Semgrep, and PSScriptAnalyzer
versions are restored on demand.

### Static analysis by language

Each language the repository ships is linted, formatted, and statically analysed
by a pinned tool, so a defect is caught by the gate rather than in review:

| Language | Lint | Format | Also |
|---|---|---|---|
| C# | Unity compiler warnings-as-errors | CSharpier | Semgrep rules |
| Python | Ruff (36 rule families, including Bandit) | Ruff | mypy, Semgrep rules |
| Shell | ShellCheck at `style`, plus five optional checks | `shfmt` | Semgrep rules |
| PowerShell | PSScriptAnalyzer | PSScriptAnalyzer's formatter | Semgrep generic rules |
| JavaScript | ESLint | Prettier | — |

`PSScriptAnalyzerSettings.psd1` holds the PowerShell rule set and documents each
exclusion. It pins `PSUseCompatibleSyntax` to Windows PowerShell 5.1 *and*
pwsh 7, because the `unity-open` shims launch these scripts with
`powershell.exe` while the gate runs them under `pwsh`. Run the PowerShell gate
alone with:

```bash
pwsh -NoProfile -File scripts/powershell-check.ps1
```

It installs its own pinned PSScriptAnalyzer on first run. Lint warnings fail the gate; do not suppress a warning unless the
repository configuration documents why the rule is inapplicable. Apply all
formatters with `./format.sh`.

### Test coverage

`ci.sh` runs the `scripts/tests` Python suite, the Node WebGL template and
service-worker suites, and the Unity EditMode test assemblies, then launches the
built WebGL player in headless Chrome with desktop and iOS browser profiles. The
autoplay desktop-player harness remains opt-in because it requires a separate
desktop player build. See [Autonomous autoplay](docs/autoplay.md) for its agent
behaviour, scenarios, telemetry, and run commands.

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

The same script installs the two line-ending settings every clone needs:

```
core.autocrlf false
core.safecrlf true
```

`.gitattributes` normalizes text to LF and is the single source of truth for it.
`core.autocrlf=false` keeps checkout driven by that file rather than by the
client — Git for Windows ships `autocrlf=true` in its system config, so on
Windows this is an override, not a restatement. `core.safecrlf=true` makes git
refuse a conversion it could not reverse, such as a binary payload committed
under a text extension, instead of silently dropping the CR bytes; git leaves it
off by default.

Neither can live in `.gitattributes`, which is why they are installed per clone
alongside the hooks path. Check them with `git config --local --get core.autocrlf`
and `--get core.safecrlf`.

### The unity-open command

Repository tooling drives an Editor that was started with `-automated`, so open
the project with:

```bash
./scripts/install-unity-open.sh
unity-open
```

The installer symlinks `scripts/unity-open.sh` into `~/.local/bin` — override the
directory with an argument or `UNITY_OPEN_BIN_DIR` — and refuses to replace an
unrelated `unity-open` already there unless given `--force`. Like the git hooks,
the link is local to the machine, so each clone runs the installer once. The
command opens this clone by default and accepts another project path as its
only argument; it exits successfully when an automated Editor is already
attached, and refuses to act when the project is open without `-automated`.

Windows has the same pair and exit codes:

```powershell
.\scripts\install-unity-open.ps1
unity-open
```

On Windows, `unity-open` probes DX12 support on the default display adapter and
adds `-force-d3d12` when available, enabling native HDR in the Editor's Game view.
If DX12 is unavailable or the probe fails, it leaves Unity's graphics API default
alone. All launches still include `-automated`. An already-running Editor is
never restarted or switched automatically.

Use `unity-open -GraphicsApi Direct3D11` for a DX11 launch,
`unity-open -GraphicsApi Direct3D12` to explicitly force DX12, or
`unity-open -GraphicsApi Default` to skip detection and use Unity's default.
The macOS/Linux shell launcher continues to use Unity's default graphics API.

Windows symlinks need Developer Mode or an elevated shell, so that installer
writes forwarding shims rather than a link: `unity-open.cmd` for PowerShell and
cmd, and an extensionless `unity-open` for Git Bash. Both name this clone's
`scripts\unity-open.ps1` by absolute path, so re-run the installer after moving
the clone.

### 300-line source-file limit

First-party source files hard-fail above 300 physical lines. No files are currently
grandfathered. `.quality/loc-baseline.tsv` remains as the ratchet mechanism: any
entry may only decrease and must be removed once its file reaches 300 lines.

### Game-runtime line coverage

Every line of the shipping game runtime must be covered by an EditMode test.
The gate measures three assemblies — `BudgetGameDev.Games.Brocoli`,
`BudgetGameDev.Shared` and `BudgetGameDev.Hub` — and nothing else: Editor
assemblies are authoring tooling, test assemblies are the measuring instrument,
and everything outside `BudgetGameDev.*` belongs to Unity or a third party.

The target is 100%, reached by a ratchet. `.quality/coverage-baseline.tsv`
records how many uncovered lines each file still carries; an entry may only
shrink, and it must be deleted once its file reaches 100%. A file with no entry
must be fully covered, so new runtime code arrives with tests or the gate fails.

Coverage is never bought by suppressing it. `[ExcludeFromCodeCoverage]` and
`[ExcludeFromCoverage]` are rejected outright in the measured assemblies: when a
line is hard to reach, add the seam — an interface, an injected dependency, a
method that takes its state as an argument — that lets a test reach it. The gate
also fails if a measured assembly or runtime file is missing from the report, so
code cannot escape by becoming invisible to instrumentation.

Regenerate the baseline after a deliberate change with:

```bash
python3 scripts/check_coverage.py build/Coverage --write-baseline
```

Unlike the other Unity gates, `scripts/unity-coverage-check.sh` cannot reuse a
connected Editor: instrumentation is switched on when the Editor boots and the
Code Coverage package exposes no command on an attached instance. The gate
therefore starts its own batch-mode Editor and refuses to run while any Editor
holds the project. Close the open Editor before a promotion push, and reopen it
with `unity-open` afterwards.

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

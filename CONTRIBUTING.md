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

It runs pinned C# and Python formatting checks, Python and shell linting, local
Semgrep static-analysis rules, the 300-line source-size ratchet, and Unity
compilation. `Assets/csc.rsp` promotes every C# compiler warning to an error, and
the Unity check also rejects any first-party compiler warnings found in its log.

Install the prerequisites once:

- .NET SDK 8 or newer (`dotnet`)
- Astral `uv`
- ShellCheck
- `shfmt`
- Unity matching `ProjectSettings/ProjectVersion.txt`

The pinned CSharpier, Ruff, and Semgrep versions are restored on demand. Apply all
formatters with `./format.sh`.

### Test coverage status

The Unity Test Framework package is installed, but the repository currently has no
Edit Mode or Play Mode test assemblies, so `ci.sh` does not claim a unit- or
integration-test gate. The `scripts/autoplay-*.sh` player harness provides opt-in
runtime/E2E smoke coverage; it is not part of the default pre-push gate because it
requires a built desktop player and takes substantially longer than compilation.

Enable the repository-managed pre-push hook with:

```bash
./scripts/install-git-hooks.sh
```

The hook runs `./ci.sh` when a push updates `staging` or `production`,
and blocks that push if any gate fails. Pushes exclusively to other branches or tags
skip the gate. Git hooks are local and are not activated merely by cloning the
repository, which is why the installer is required once per clone.

### 300-line source-file limit

New first-party source files hard-fail above 300 physical lines. The project already
had 35 larger files when the rule was introduced, so
`.quality/loc-baseline.tsv` records their current ceilings. Those files may shrink
but may not grow, and the baseline entry must be removed once a file reaches 300
lines. This is a ratchet, not a general exemption.

### Unity compilation

When the project Editor is open, `ci.sh` recompiles through the connected Editor.
Otherwise it uses the batch-mode checker. On Windows PowerShell, the standalone
batch checker is `.\scripts\unity-build-check.ps1`. The check uses the editor
version in `ProjectSettings/ProjectVersion.txt`, resolves Unity packages, imports
assets, and compiles for the WebGL target. It does not create the deployable WebGL
player; the separate CI build job performs that full build.

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

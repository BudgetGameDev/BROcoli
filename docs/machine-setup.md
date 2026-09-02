# Setting up another machine

Everything a clone can carry is already checked in; the rest is per-machine
state that no repository can hold for you — installed tools, an installed Unity
Editor, a license, and the symlinks and git config that make local commands
work. This page covers the second kind.

## One command

```bash
./scripts/bootstrap-macos.sh
```

```powershell
.\scripts\bootstrap-windows.ps1
```

It is idempotent, so re-running it on a configured machine only reports state.
Add `--dry-run` to see every action first. It stops short of anything that needs
a human — a Homebrew install, a Unity sign-in, replacing an unrelated
`unity-open` already on `PATH` — and prints those as a list of leftovers at the
end. It exits non-zero only when it cannot continue at all, which in practice
means the Unity CLI is still not on `PATH` after its install step.

The steps below describe the macOS script; [Windows](#windows) records where its
counterpart differs. Each step can also be run by hand:

1. **Host tools.** Installs `node`, `shellcheck`, `shfmt`, and Google Chrome with
   Homebrew. `dotnet` and `uv` are only reported, because both ship their own
   installers and are usually managed outside Homebrew. `CONTRIBUTING.md` lists
   the full prerequisite set and the versions the gate expects.
2. **Unity CLI.** Installs the beta channel from Unity's CDN when `unity` is
   missing:

   ```bash
   curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
   ```

   The installer writes `~/.unity/env` and puts `~/.unity/bin` on `PATH`, which
   an already-open shell will not see until it is restarted.
3. **Editor and modules.** Reads the version from
   `ProjectSettings/ProjectVersion.txt` and installs it with the three modules
   this repository builds with: `webgl` for the CI player build, and
   `windows-mono` and `linux-mono` for `scripts/native-builds.sh`. macOS player
   support is part of the Editor itself.
4. **Licensing.** Reports `unity license status`. A machine with no license
   needs `unity auth login` once, interactively; nothing else here can be
   automated on your behalf.
5. **Repository commands.** Runs `./scripts/install-git-hooks.sh` and
   `./scripts/install-unity-open.sh`. Both are local to the machine — a clone
   activates neither on its own, which is why each is installed once per clone.
   The hooks installer also sets `core.autocrlf=false` and `core.safecrlf=true`,
   per-clone git config that `.gitattributes` cannot carry. On Windows the first
   overrides the `autocrlf=true` Git for Windows puts in its system config. See
   `CONTRIBUTING.md` for what each protects against.
6. **Agent integration.** Verifies the checked-in wiring described below.

## What the clone already carries

The agentic Unity setup is version-controlled, so a fresh clone inherits it and
there is nothing to install for Claude Code:

- `.mcp.json` registers the `unity mcp` stdio server as a project-scoped MCP
  server, which is what gives an agent the `unity-editor-mcp` tools. A
  project-scoped server stays inert until this clone approves it by name, so
  approve it when Claude Code prompts, or record the choice in the untracked
  `.claude/settings.local.json`:

  ```json
  { "enabledMcpjsonServers": ["unity-editor-mcp"] }
  ```

  Until then `claude mcp list` reports it as pending and the tools are absent
  rather than broken.
- `.claude/skills/unity-cli/` is the Unity CLI agent skill, installed with
  `unity skill install claude-code --local`. Refresh it deliberately with that
  same command plus `--yes`, and review the diff like any other change.
- `com.unity.pipeline` is pinned in `Packages/manifest.json`. It is the package
  that lets `unity status`, `unity command`, and the MCP server drive a running
  Editor, so an Editor opened on this project exposes its commands with no
  further setup.
- `AGENTS.md` and `CLAUDE.md` carry the rules every agent loads, including the
  requirement to open the Editor only through `unity-open`.

Other clients are per-machine, since their configuration lives in a user-global
file rather than the repository. Register one with:

```bash
./scripts/bootstrap-macos.sh --agent-client codex
```

which runs `unity mcp configure codex --yes --project-path <clone>` and
`unity skill install codex --yes`. `unity mcp configure --list` and
`unity skill install --list` show every supported client and where each writes.
The Codex epic worker (`scripts/run-epic-codex-worker.sh`) works without this —
it reads `AGENTS.md` and calls the `unity` CLI directly — so add it only when
you want the MCP tools inside that client.

## Verifying the result

```bash
unity --version                  # CLI on PATH
unity license status             # a license is active
unity-open                       # opens this clone with -automated
unity status                     # the Editor reports state "ready"
./ci.sh                          # the complete host gate
```

`unity status` reporting a ready Editor is the check that matters for agentic
work: it means the CLI, the Editor, and the Pipeline package are all talking to
each other. `unity doctor` prints a broader diagnostic when one of them is not.

## Restarting a wedged Editor

A long-lived Editor sometimes reaches a state that no amount of retrying gets it
out of. What that looks like:

- The Game view is black on Play although the scene is loaded and its camera
  still renders: `capture_game_view source=camera` returns the scene while
  `source=screen` comes back pure black.
- An exception whose every frame is Unity's own editor code, with nothing from
  this repository in the stack -- a `NullReferenceException` in
  `SerializedObjectBindingToBaseField.OnFieldAttached` while
  `EditorApplicationLayout.SetStopmodeLayout` restores the window layout on
  leaving play mode, for instance.
- A test run that aborted mid-flight: `InvalidOperationException: This cannot be
  used during play mode` out of `SaveModifiedSceneTask` or
  `RestoreSceneSetupTask`, then "Test tree is not available" and "An unexpected
  error happened while running tests". The runner skips its cleanup tasks, so
  whatever play mode left behind stays behind.
- `unity status` or `unity cmd editor_status` no longer answering.

None of that is a bug in the game to be debugged in place. Close the Editor and
open a fresh automated one:

```powershell
unity cmd save_all               # only if the Editor still answers
unity cmd menu path="File/Exit"  # ask it to quit
unity status                     # the instance is gone
unity-open                       # a fresh automated Editor
unity cmd editor_status          # "ready"
```

When it is too wedged to quit on request, stop the process instead. `unity
status --format json` reports the pid for this project, `Temp/UnityLockfile` in
the clone disappears once the process is really gone, and `unity-open` refuses
to start while another Editor still holds the project.

```powershell
Stop-Process -Id <pid>
```

```bash
kill <pid>
```

Two things that look like the same failure but are not. A batch-mode gate such
as `./scripts/unity-test-check.sh` exits 1 with "no results written" whenever an
Editor holds the project lock: run those tests through the open Editor
(`unity cmd run_tests mode=editor`) or close it first. And a running Editor
serves the assemblies it last compiled, so `unity cmd recompile` after editing
C# is what keeps a test run from quietly exercising the old code.

## Windows

```powershell
.\scripts\bootstrap-windows.ps1
```

It runs the same six steps in the same order, is idempotent the same way, and
takes `-DryRun` and `-AgentClient` where the shell script takes `--dry-run` and
`--agent-client`. It also leaves the same kinds of leftovers for a human, and
exits non-zero only when the Unity CLI is still missing after its install step.
What differs is per-platform, not per-script:

- **Package manager.** `winget` replaces Homebrew and installs `node`,
  `shellcheck`, `shfmt`, and Google Chrome. It edits the persisted `PATH`, which
  an already-open shell never re-reads, so the script says when to open a new
  one. `dotnet` and `uv` are reported rather than installed, as on macOS.
- **Python.** Windows ships a `python3.exe` App Execution Alias that only
  advertises the Microsoft Store, so the script checks that `python3` actually
  runs rather than that it is on `PATH`. `ci.sh` requires a real interpreter.
- **Shell.** `ci.sh`, `format.sh`, and the gate wrappers are shell scripts, so
  the Windows host runs them under the bash that ships with Git for Windows. The
  script locates that bash, uses it for `./scripts/install-git-hooks.sh`, and
  reports it as a missing prerequisite when it is absent.
- **Unity CLI.** Installed from the same CDN with the PowerShell installer
  (`install.ps1`), which writes `%LOCALAPPDATA%\Unity\bin` onto `PATH`.
- **Editor modules.** Only `webgl`. Windows player support ships with the
  Windows Editor, and `scripts/native-builds.ps1` builds `StandaloneWindows64`
  and nothing else, so the two Mono players macOS installs are not needed here.
  `docs/native-releases.md` covers the Windows build path in full.
- **`unity-open`.** `.\scripts\install-unity-open.ps1` writes forwarding shims
  instead of a symlink, for the reason `CONTRIBUTING.md` records.

`unity upgrade` reports success without replacing the binary on Windows. Upgrade
the CLI by re-running the installer one-liner instead.

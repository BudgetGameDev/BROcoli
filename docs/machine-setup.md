# Setting up another machine

Everything a clone can carry is already checked in; the rest is per-machine
state that no repository can hold for you — installed tools, an installed Unity
Editor, a license, and the symlinks and git config that make local commands
work. This page covers the second kind.

## One command

```bash
./scripts/bootstrap-macos.sh
```

It is idempotent, so re-running it on a configured machine only reports state.
Add `--dry-run` to see every action first. It stops short of anything that needs
a human — a Homebrew install, a Unity sign-in, replacing an unrelated
`unity-open` already on `PATH` — and prints those as a list of leftovers at the
end. It exits non-zero only when it cannot continue at all, which in practice
means the Unity CLI is still not on `PATH` after its install step.

The script performs these steps, each of which can be run by hand:

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
6. **Agent integration.** Verifies the checked-in wiring described below.

## What the clone already carries

The agentic Unity setup is version-controlled, so a fresh clone inherits it and
there is nothing to install for Claude Code:

- `.mcp.json` registers the `unity mcp` stdio server as a project-scoped MCP
  server, which is what gives an agent the `unity-editor-mcp` tools.
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

## Windows

There is no bootstrap script for Windows. Install the CLI with the PowerShell
one-liner from the Unity CLI skill, install the Editor the same way
(`unity install <version> --yes --accept-eula`), and open the project with
`.\scripts\unity-open.ps1`. `docs/native-releases.md` covers the Windows build
path in full.

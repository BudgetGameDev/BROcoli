#!/usr/bin/env bash
# Bring a fresh macOS machine to the tooling state this repository expects.
# Usage: ./scripts/bootstrap-macos.sh [--dry-run] [--agent-client NAME]...
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$PROJECT_PATH"

UNITY_CLI_INSTALLER="https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh"
# The WebGL gate needs webgl; scripts/native-builds.sh needs the two Mono
# players it cross-compiles. macOS player support ships with the Editor.
EDITOR_MODULES="webgl windows-mono linux-mono"
BREW_FORMULAE="node shellcheck shfmt"
DRY_RUN=0
AGENT_CLIENTS=()
SUMMARY=()

usage() {
    cat <<'USAGE'
Usage: ./scripts/bootstrap-macos.sh [--dry-run] [--agent-client NAME]...

Installs the host tools, the Unity CLI, the Editor and modules this project
builds with, and the repository's per-clone hooks and commands.

Options:
  --dry-run             Print every action without changing the machine.
  --agent-client NAME   Also register the Unity MCP server and CLI skill with
                        this AI client (codex, cursor, vscode, …). Claude Code
                        reads the checked-in .mcp.json and .claude/skills.
  -h, --help            Show this help.
USAGE
}

while (($# > 0)); do
    case "$1" in
        --dry-run)
            DRY_RUN=1
            shift
            ;;
        --agent-client)
            AGENT_CLIENTS+=("${2:?--agent-client requires a value}")
            shift 2
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

step() {
    echo ""
    echo "==> $1"
}

note() {
    SUMMARY+=("$1")
}

run() {
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "would run: $*"
        return 0
    fi
    "$@"
}

if [ "$(uname -s)" != "Darwin" ]; then
    echo "bootstrap: this script sets up macOS; see docs/machine-setup.md for other hosts." >&2
    exit 2
fi

step "Host tools"
if command -v brew >/dev/null 2>&1; then
    for formula in $BREW_FORMULAE; do
        if brew list --formula "$formula" >/dev/null 2>&1; then
            echo "$formula: present"
        else
            run brew install "$formula"
        fi
    done
    if [ -d "/Applications/Google Chrome.app" ]; then
        echo "google-chrome: present"
    else
        run brew install --cask google-chrome
    fi
else
    note "Homebrew is missing: install it from https://brew.sh, then re-run this script."
fi

# .NET and uv ship their own installers and are commonly managed outside
# Homebrew, so report them rather than adopting whatever is already there.
for tool in dotnet uv; do
    if command -v "$tool" >/dev/null 2>&1; then
        echo "$tool: present"
    else
        note "$tool is missing: see the prerequisites in CONTRIBUTING.md."
    fi
done

step "Unity CLI"
if command -v unity >/dev/null 2>&1; then
    echo "unity: $(unity --version 2>/dev/null)"
else
    echo "Installing the Unity CLI beta channel from $UNITY_CLI_INSTALLER"
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "would run: curl -fsSL $UNITY_CLI_INSTALLER | UNITY_CLI_CHANNEL=beta bash"
    else
        curl -fsSL "$UNITY_CLI_INSTALLER" | UNITY_CLI_CHANNEL=beta bash
        export PATH="$HOME/.unity/bin:$PATH"
    fi
    note "The installer adds ~/.unity/bin to PATH from ~/.unity/env; open a new shell to pick it up."
fi

if ! command -v unity >/dev/null 2>&1; then
    note "unity is still not on PATH; the remaining Unity steps were skipped."
    printf '\n%s\n' "bootstrap: incomplete"
    printf '  - %s\n' "${SUMMARY[@]}"
    exit 1
fi

step "Unity Editor and modules"
EDITOR_VERSION="$(awk '$1 == "m_EditorVersion:" { print $2; exit }' ProjectSettings/ProjectVersion.txt)"
echo "Project Editor version: $EDITOR_VERSION"
if unity editors path "$EDITOR_VERSION" >/dev/null 2>&1; then
    echo "$EDITOR_VERSION: installed"
else
    run unity install "$EDITOR_VERSION" --yes --accept-eula
fi

for module in $EDITOR_MODULES; do
    if unity modules list "$EDITOR_VERSION" --format tsv 2>/dev/null |
        awk -F'\t' -v id="$module" '$1 == id && $4 == "Installed" { found = 1 } END { exit !found }'; then
        echo "$module: installed"
    else
        run unity install-modules --editor-version "$EDITOR_VERSION" \
            --module "$module" --yes --accept-eula
    fi
done

step "Unity licensing"
if unity license status >/dev/null 2>&1; then
    echo "A Unity license is active."
else
    note "No active Unity license: run 'unity auth login', then 'unity license status'."
fi

step "Repository commands"
run ./scripts/install-git-hooks.sh
# Exit 3 means an unrelated unity-open already owns that name on PATH. That is
# the user's file to keep or replace, so report it instead of failing the run.
INSTALL_STATUS=0
run ./scripts/install-unity-open.sh || INSTALL_STATUS=$?
if [ "$INSTALL_STATUS" -eq 3 ]; then
    note "A different unity-open is already on PATH: replace it with './scripts/install-unity-open.sh --force'."
elif [ "$INSTALL_STATUS" -ne 0 ]; then
    exit "$INSTALL_STATUS"
fi

step "Agent integration"
# Claude Code picks both of these up from the clone, so they need no install.
for checked_in in .mcp.json .claude/skills/unity-cli/SKILL.md; do
    if [ -e "$checked_in" ]; then
        echo "$checked_in: present in the clone"
    else
        note "$checked_in is missing from the clone; Claude Code loses the Unity integration."
    fi
done
echo "com.unity.pipeline is pinned in Packages/manifest.json, so a connected Editor exposes its commands."

for client in ${AGENT_CLIENTS+"${AGENT_CLIENTS[@]}"}; do
    run unity mcp configure "$client" --yes --project-path "$PROJECT_PATH"
    run unity skill install "$client" --yes
done

echo ""
if [ "${#SUMMARY[@]}" -eq 0 ]; then
    echo "bootstrap: complete"
else
    echo "bootstrap: finish these by hand"
    printf '  - %s\n' "${SUMMARY[@]}"
fi

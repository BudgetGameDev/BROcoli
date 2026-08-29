#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: ./scripts/run-epic-codex-worker.sh --issue NUMBER [options]

Launch the selected Codex epic worker. Claude workers must be spawned as
in-session project-scoped subagents and must never use this launcher.

Options:
  --model MODEL       Codex model (default: gpt-5.6-sol)
  --effort EFFORT     Reasoning effort (default: high)
  -h, --help          Show this help.
EOF
}

issue=""
model="gpt-5.6-sol"
effort="high"

while (($# > 0)); do
    case "$1" in
        --issue)
            issue="${2:?--issue requires a value}"
            shift 2
            ;;
        --model)
            model="${2:?--model requires a value}"
            shift 2
            ;;
        --effort)
            effort="${2:?--effort requires a value}"
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

issue="${issue#\#}"
if [[ -z "$issue" || "$issue" == *[!0-9]* ]]; then
    echo "--issue must be a GitHub issue number." >&2
    exit 2
fi

case "$effort" in
    low | medium | high | xhigh | max) ;;
    *)
        echo "Unsupported Codex effort '$effort'." >&2
        exit 2
        ;;
esac

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

epic_context=""
if [[ -n "${EPIC_SPEC_PATH:-}" ]]; then
    epic_context=" Read and follow the active epic specification at $EPIC_SPEC_PATH."
fi
prompt="Implement only GitHub issue #$issue. Read and follow the complete repository-root epic-worker.md contract before doing any work.$epic_context"

exec codex exec --cd "$repo_root" --model "$model" \
    --config "model_reasoning_effort=\"$effort\"" \
    --sandbox danger-full-access --config 'approval_policy="never"' "$prompt"

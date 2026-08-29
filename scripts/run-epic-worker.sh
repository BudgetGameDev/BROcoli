#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: ./scripts/run-epic-worker.sh --issue NUMBER [--select-only]

Randomly select one configured epic worker and run it for a single issue.
Workers come from EPIC_WORKER_SPECS as comma-separated PROVIDER:MODEL:EFFORT
entries. Duplicate entries may be used as weights.

Defaults:
  claude:claude-opus-5:high,codex:gpt-5.6-sol:high
EOF
}

issue=""
select_only=false

while (($# > 0)); do
    case "$1" in
        --issue)
            issue="${2:?--issue requires a value}"
            shift 2
            ;;
        --select-only)
            select_only=true
            shift
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

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
default_workers="claude:claude-opus-5:high,codex:gpt-5.6-sol:high"
IFS=',' read -r -a workers <<<"${EPIC_WORKER_SPECS:-$default_workers}"

if ((${#workers[@]} == 0)); then
    echo "EPIC_WORKER_SPECS must contain at least one worker." >&2
    exit 2
fi

for worker in "${workers[@]}"; do
    IFS=':' read -r provider model effort extra <<<"$worker"
    effort="${effort:-high}"
    if [[ -n "${extra:-}" || -z "${provider:-}" || -z "${model:-}" ]]; then
        echo "Invalid worker '$worker'; expected PROVIDER:MODEL[:EFFORT]." >&2
        exit 2
    fi
    if [[ "$provider" != "claude" && "$provider" != "codex" ]]; then
        echo "Unsupported worker provider '$provider'; use claude or codex." >&2
        exit 2
    fi
    case "$effort" in
        low | medium | high | xhigh | max) ;;
        *)
            echo "Unsupported worker effort '$effort'." >&2
            exit 2
            ;;
    esac
done

random_value="$(od -An -N4 -tu4 /dev/urandom | tr -d ' ')"
selected="${workers[random_value % ${#workers[@]}]}"
IFS=':' read -r provider model effort <<<"$selected"
effort="${effort:-high}"

printf 'Selected epic worker: provider=%s model=%s effort=%s issue=#%s\n' \
    "$provider" "$model" "$effort" "$issue"

if [[ "$select_only" == true ]]; then
    exit 0
fi

prompt="Implement only GitHub issue #$issue. Read and follow the complete repository-root epic-worker.md contract before doing any work."

case "$provider" in
    claude)
        exec env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT -u CLAUDE_CODE_EFFORT_LEVEL \
            claude --model "$model" --effort "$effort" --agent epic-worker \
            --dangerously-skip-permissions --print "$prompt"
        ;;
    codex)
        exec codex exec --cd "$repo_root" --model "$model" \
            --config "model_reasoning_effort=\"$effort\"" \
            --sandbox danger-full-access --config 'approval_policy="never"' "$prompt"
        ;;
esac

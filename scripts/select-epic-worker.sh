#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: ./scripts/select-epic-worker.sh --issue NUMBER

Randomly select exactly one configured epic worker. This script only selects;
it never launches Claude or Codex. Workers come from EPIC_WORKER_SPECS as
comma-separated PROVIDER:MODEL:EFFORT entries. Duplicate entries are weights.

Default pool:
  claude:claude-opus-5:high,codex:gpt-5.6-sol:high
EOF
}

issue=""

while (($# > 0)); do
    case "$1" in
        --issue)
            issue="${2:?--issue requires a value}"
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

default_workers="claude:claude-opus-5:high,codex:gpt-5.6-sol:high"
IFS=',' read -r -a workers <<<"${EPIC_WORKER_SPECS:-$default_workers}"

if ((${#workers[@]} == 0)); then
    echo "EPIC_WORKER_SPECS must contain at least one worker." >&2
    exit 2
fi

claude_worker_model=""
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
    if [[ "$provider" == "claude" && "$effort" != "high" ]]; then
        echo "Claude epic workers run as the high-effort in-session epic-worker." >&2
        exit 2
    fi
    if [[ "$provider" == "claude" ]]; then
        if [[ -n "$claude_worker_model" && "$claude_worker_model" != "$model" ]]; then
            echo "All Claude pool entries must use the same model." >&2
            exit 2
        fi
        claude_worker_model="$model"
    fi
    case "$effort" in
        low | medium | high | xhigh | max) ;;
        *)
            echo "Unsupported worker effort '$effort'." >&2
            exit 2
            ;;
    esac
done

if [[ -n "${EPIC_CLAUDE_WORKER_MODEL:-}" && -n "$claude_worker_model" &&
    "$EPIC_CLAUDE_WORKER_MODEL" != "$claude_worker_model" ]]; then
    echo "Claude pool model does not match EPIC_CLAUDE_WORKER_MODEL." >&2
    exit 2
fi

random_value="$(od -An -N4 -tu4 /dev/urandom | tr -d ' ')"
selected="${workers[random_value % ${#workers[@]}]}"
IFS=':' read -r provider model effort <<<"$selected"
effort="${effort:-high}"

printf 'provider=%s model=%s effort=%s issue=#%s\n' \
    "$provider" "$model" "$effort" "$issue"

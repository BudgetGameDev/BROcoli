#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: ./run-epic.sh [options] [-- CLAUDE_ARGS...]

Launch the epic manager and configure the worker pool it load-balances across.

Options:
  --manager-model MODEL       Manager model (default: claude-opus-5)
  --manager-effort EFFORT     Manager effort (default: high)
  --worker PROVIDER:MODEL[:EFFORT]
                              Add a worker to the random pool. Repeatable.
                              The first --worker replaces the default pool.
  -h, --help                  Show this help.

Default workers:
  claude:claude-opus-5:high
  codex:gpt-5.6-sol:high

Examples:
  ./run-epic.sh
  ./run-epic.sh --manager-model claude-fable-5
  ./run-epic.sh --worker claude:claude-opus-5:high \
    --worker codex:gpt-5.6-sol:high
EOF
}

manager_model="${EPIC_MANAGER_MODEL:-claude-opus-5}"
manager_effort="${EPIC_MANAGER_EFFORT:-high}"
default_workers="claude:claude-opus-5:high,codex:gpt-5.6-sol:high"
worker_specs="${EPIC_WORKER_SPECS:-$default_workers}"
worker_override=false
claude_args=()

while (($# > 0)); do
    case "$1" in
    --manager-model)
        manager_model="${2:?--manager-model requires a value}"
        shift 2
        ;;
    --manager-effort)
        manager_effort="${2:?--manager-effort requires a value}"
        shift 2
        ;;
    --worker)
        if [[ "$worker_override" == false ]]; then
            worker_specs=""
            worker_override=true
        fi
        worker_specs="${worker_specs:+$worker_specs,}${2:?--worker requires a value}"
        shift 2
        ;;
    -h | --help)
        usage
        exit 0
        ;;
    --)
        shift
        claude_args+=("$@")
        break
        ;;
    *)
        claude_args+=("$1")
        shift
        ;;
    esac
done

if [[ -z "$manager_model" || -z "$worker_specs" ]]; then
    echo "Manager model and worker pool must not be empty." >&2
    exit 2
fi

case "$manager_effort" in
low | medium | high | xhigh | max) ;;
*)
    echo "Unsupported manager effort '$manager_effort'." >&2
    exit 2
    ;;
esac

IFS=',' read -r -a configured_workers <<<"$worker_specs"
for worker in "${configured_workers[@]}"; do
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

exec env -u CLAUDE_CODE_EFFORT_LEVEL \
    EPIC_MANAGER_MODEL="$manager_model" \
    EPIC_MANAGER_EFFORT="$manager_effort" \
    EPIC_WORKER_SPECS="$worker_specs" \
    claude --model "$manager_model" --effort "$manager_effort" "${claude_args[@]}"

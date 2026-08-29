#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: ./run-epic.sh EPIC_FILE [options] [-- CLAUDE_ARGS...]

Launch the reusable epic manager for the selected epic specification.

Options:
  --manager-model MODEL       Manager model (default: claude-opus-5)
  --manager-effort EFFORT     Manager effort (default: high)
  --worker PROVIDER:MODEL[:EFFORT]
                              Add a worker to the random pool. Repeatable.
                              The first --worker replaces the default pool.
                              Claude entries use one model at high effort.
  -h, --help                  Show this help.

Default workers:
  claude:claude-opus-5:high   Spawned as an in-session Claude subagent
  codex:gpt-5.6-sol:high      Launched through codex exec

Examples:
  ./run-epic.sh arpg-epic.md
  ./run-epic.sh future-epic.md --manager-model claude-opus-5
  ./run-epic.sh future-epic.md \
    --worker claude:claude-opus-5:high \
    --worker codex:gpt-5.6-sol:high
EOF
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
invocation_dir="$PWD"
manager_contract="$repo_root/epic-manager.md"
manager_model="${EPIC_MANAGER_MODEL:-claude-opus-5}"
manager_effort="${EPIC_MANAGER_EFFORT:-high}"
default_workers="claude:claude-opus-5:high,codex:gpt-5.6-sol:high"
worker_specs="${EPIC_WORKER_SPECS:-$default_workers}"
worker_override=false
epic_file=""
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
        -*)
            echo "Unknown option: $1" >&2
            usage >&2
            exit 2
            ;;
        *)
            if [[ -n "$epic_file" ]]; then
                echo "Specify exactly one epic file." >&2
                exit 2
            fi
            epic_file="$1"
            shift
            ;;
    esac
done

if [[ -z "$epic_file" ]]; then
    echo "An epic file is required." >&2
    usage >&2
    exit 2
fi

if [[ "$epic_file" != /* ]]; then
    epic_file="$invocation_dir/$epic_file"
fi
if [[ ! -f "$epic_file" || ! -r "$epic_file" ]]; then
    echo "Epic file is not a readable file: $epic_file" >&2
    exit 2
fi
if [[ ! -f "$manager_contract" || ! -r "$manager_contract" ]]; then
    echo "Manager contract is not readable: $manager_contract" >&2
    exit 2
fi

epic_dir="$(cd "$(dirname "$epic_file")" && pwd -P)"
epic_path="$epic_dir/$(basename "$epic_file")"
case "$epic_path" in
    "$repo_root"/*) ;;
    *)
        echo "Epic file must be inside the repository: $repo_root" >&2
        exit 2
        ;;
esac

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

claude_worker_model=""
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
    if [[ "$provider" == "claude" ]]; then
        if [[ "$effort" != "high" ]]; then
            echo "Claude epic workers run as the high-effort in-session epic-worker." >&2
            exit 2
        fi
        if [[ -n "$claude_worker_model" && "$claude_worker_model" != "$model" ]]; then
            echo "All Claude pool entries must use the same model." >&2
            exit 2
        fi
        claude_worker_model="$model"
    fi
done

claude_worker_model="${claude_worker_model:-$manager_model}"
epic_relative="${epic_path#"$repo_root"/}"
manager_prompt="/goal Run the epic defined in $epic_relative. First read and follow repository-root epic-manager.md as the reusable manager contract, then read the epic specification. Begin now and continue until its completion condition or a genuine external blocker."

cd "$repo_root"
exec env -u CLAUDE_CODE_EFFORT_LEVEL \
    CLAUDE_CODE_SUBAGENT_MODEL="$claude_worker_model" \
    EPIC_MANAGER_MODEL="$manager_model" \
    EPIC_MANAGER_EFFORT="$manager_effort" \
    EPIC_CLAUDE_WORKER_MODEL="$claude_worker_model" \
    EPIC_WORKER_SPECS="$worker_specs" \
    EPIC_MANAGER_CONTRACT="$manager_contract" \
    EPIC_SPEC_PATH="$epic_path" \
    claude --model "$manager_model" --effort "$manager_effort" \
    "${claude_args[@]}" "$manager_prompt"

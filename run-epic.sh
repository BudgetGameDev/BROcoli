#!/usr/bin/env bash
set -euo pipefail

exec env -u CLAUDE_CODE_EFFORT_LEVEL \
  CLAUDE_CODE_SUBAGENT_MODEL=claude-opus-4-8 \
  claude --model claude-fable-5 --effort high "$@"

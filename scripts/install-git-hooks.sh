#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_PATH"

git config --local core.hooksPath .githooks
echo "Git hooks enabled from .githooks."
echo "The pre-push CI gate applies to staging and production."

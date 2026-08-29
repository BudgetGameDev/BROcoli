#!/usr/bin/env bash
# Apply the repository's pinned source formatters.
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_PATH"

for tool in dotnet uv shfmt; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "format: missing required tool '$tool'. See CONTRIBUTING.md." >&2
        exit 2
    fi
done

export DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet tool restore
dotnet csharpier format Assets/Scripts Assets/Editor Assets/Tests
uvx ruff@0.12.11 check scripts --fix
uvx ruff@0.12.11 format scripts
shfmt -w -i 4 -ci ci.sh format.sh scripts/*.sh .githooks/pre-push

echo "format: complete"

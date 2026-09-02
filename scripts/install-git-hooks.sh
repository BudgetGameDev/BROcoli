#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_PATH"

git config --local core.hooksPath .githooks
echo "Git hooks enabled from .githooks."
echo "The pre-push CI gate applies to staging and production."

# Line-ending settings .gitattributes cannot carry, installed per clone for the
# same reason as the hooks path: a clone activates neither on its own.
#
# autocrlf=false keeps checkout driven by .gitattributes rather than by the
# client. Git for Windows ships autocrlf=true in its system config, so on Windows
# this is an override rather than a restatement of the default. The attributes
# already win today; setting it explicitly means that stays true for any path
# their coverage stops reaching.
#
# safecrlf makes git refuse a conversion it could not reverse -- a binary payload
# committed under a text extension -- instead of silently dropping the CR bytes.
# Git leaves it off by default.
git config --local core.autocrlf false
git config --local core.safecrlf true
echo "Line endings: core.autocrlf=false, core.safecrlf=true."

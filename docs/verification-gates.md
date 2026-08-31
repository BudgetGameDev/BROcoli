# Verification gates

`AGENTS.md` states the rule that must never be broken: **`dev` is ungated.** This
guide is the reasoning and the mechanics behind it. Read it before changing CI,
the pre-push hook, the GitHub workflow, or the deploy scripts.

For how to run the gates as a contributor, see
[CONTRIBUTING.md](../CONTRIBUTING.md); this guide does not repeat it.

## `dev` is ungated on purpose

This is a hard requirement, not a default to be improved on:

- The pre-push hook runs `./ci.sh` only when a push updates `staging` or
  `production`. Do not widen it to `dev`, to other branches, or to a
  "fast subset" of gates on every push.
- Do not add a GitHub Actions workflow that runs quality checks, tests, or
  builds on `dev`, or on pull requests targeting it. The one hosted job is a
  manually dispatched Pages build kept as a fallback; deploys normally come from
  `./cd.sh` on the host, and nothing runs on push.
- Do not add commit hooks, watchers, or scheduled jobs that verify `dev`.

The cost of this is real and accepted: formatting, lint, and source-size
regressions accumulate on `dev` and surface together at promotion, where the
gate catches them. Fix them there. That backlog is not evidence the setup is
broken, and it is not a reason to gate `dev`.

Running `./ci.sh`, or any individual check, by hand at any time is fine. What is
forbidden is wiring one to run automatically on `dev`.

## Why it is built this way

The gate is expensive: it runs a full Unity EditMode suite, a second
instrumented run of that suite to measure coverage, a WebGL player build, and
two browser smoke tests, which takes minutes rather than seconds.
Paying that on every `dev` push would make the branch unusable for the small,
frequent commits it exists to carry. Promotion to `staging` or `production` is
the point where correctness has to be established, so that is where the cost is
spent.

Deploys come from the host rather than a hosted runner for the same reason: the
Mac mini has already built and smoke-tested the exact artifact a hosted job
would rebuild, so `./cd.sh` publishes that one instead of paying twice.

## What holds the guarantee together

- `./ci.sh` runs every gate and, only on success, writes a receipt.
- The receipt binds a green run to a commit, a working tree, and a built player.
- `./cd.sh` recomputes all three and refuses to publish if any has changed, so
  neither a stale pass, a later edit, nor a hand-run `./cd.sh` can reach Pages.
- `ci.sh` clears the receipt before its first gate, so an interrupted or failing
  run leaves no pass behind.

## When you touch the gates

- Gate roots are listed in `ci.sh` and `format.sh`, the static-analysis paths in
  `.semgrep.yml`, the source-size roots in `scripts/check_source_size.py`, and
  the measured coverage assemblies in `scripts/check_coverage.py` and
  `scripts/unity-coverage-check.sh`. Moving source between trees means updating
  all of them together.
- `.quality/loc-baseline.tsv` is the source-size ratchet and is currently empty.
  Any future entry may only decrease and must be removed once its file reaches
  the 300-line limit.
- `.quality/coverage-baseline.tsv` records the uncovered lines each game-runtime
  file still carries. Entries may only decrease; remove one once its file
  reaches 100%. A runtime file with no entry must be fully covered.
- The coverage gate runs its own batch-mode Editor, because instrumentation is
  enabled at Editor boot and the Code Coverage package offers no command on an
  attached instance. It is the one Unity gate that cannot reuse a connected
  Editor, and it refuses to run while one holds the project.
- Coverage suppression attributes are rejected rather than counted. Raising
  coverage means adding a seam that makes the line reachable, so the two
  baselines pull in opposite directions on purpose: files shrink toward 300
  lines while their covered fraction grows toward 100%.
- The hosted workflow caches `Library` on a hash of the source trees. A new
  top-level source directory must be added to that key, or the cache will
  survive changes it should invalidate.

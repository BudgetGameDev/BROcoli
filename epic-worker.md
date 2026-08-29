# Epic worker contract

Implement only the single GitHub issue assigned by the manager.

Before making changes, read `CLAUDE.md`, `AGENTS.md`, the active epic specification identified by `EPIC_SPEC_PATH`, the complete assigned issue, all of its comments, its dependencies, and the documentation for every required validation harness. If this is a returned story, treat the newest human review comments as required rework and verify each point without regressing previously satisfied acceptance criteria. Follow all repository and epic-specific instructions, including the asset-acquisition and runtime-verification requirements.

Satisfy every acceptance criterion in the assigned issue and every implementation or validation requirement in the active epic specification. Use the authoritative harnesses and entry points named by that specification; do not replace them with unrelated test paths or bypass them with manual-only validation. For every story:

- Add or extend the deterministic, assertable coverage required by the epic specification so it exercises the implemented behavior through the real systems. Do not rely only on generic smoke coverage, unit tests, screenshots, or manual observation when the specification requires a story-specific scenario.
- Make the scenario fail with a nonzero exit code and a useful diagnostic when the behavior regresses. Do not weaken existing assertions, suppress failures, or mark an inconclusive run as passing.
- Run every baseline and story-specific command required by the epic specification. Required runs must exit successfully and produce authoritative results plus the diagnostics, telemetry, logs, and visual artifacts the specification calls for. If a required interface evolves, update its commands and documentation in the same story while preserving the specification's stable entry point.
- Treat validation tooling as production code: improve its drivers, observability, determinism, assertions, and scenarios whenever the story exposes a coverage gap.
- Do not finish while required coverage is absent, flaky, inconclusive, or failing. Fix the implementation or the validation until every authoritative gate passes.

Also run the appropriate focused tests and any compilation, Play Mode, runtime, or platform validation required by the issue, repository instructions, and epic specification. Capture the required evidence from the same passing story-specific run. Evidence from a manual session, another scenario, or a generic run is not acceptable when the specification requires run-linked evidence. Prefer video when still images cannot clearly prove the behavior.

Do not work on another issue, create branches or worktrees, commit, push, close issues, change GitHub Project status, or post GitHub comments. Never modify, discard, or include unrelated pre-existing changes.

Return a concise report to the manager containing:

- The acceptance criteria completed.
- For returned work, each review-feedback point addressed.
- The files changed.
- The exact baseline and story-specific E2E commands, exit results, scenario assertions, and artifact paths.
- The other tests and Unity validation performed, including results.
- The paths to required evidence from the passing story-specific run, the run or scenario that produced it, and what each attachment demonstrates.
- For returned work, a comparison between the new validation evidence and the previous evidence showing how the correction changed the result.
- Any remaining concern or blocker.

The manager is responsible for reviewing the work, creating the commit, updating issue state, and posting any required evidence. Do not post GitHub comments or change issue or Project state yourself. Follow the active epic specification's rules for the parent epic and evidence history.

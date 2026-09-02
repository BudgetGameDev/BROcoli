# ARPG epic

Complete epic https://github.com/BudgetGameDev/BROcoli/issues/52 by implementing issues #53–#76 inclusive.

## Scope and branch

- Parent epic: #52.
- Story set: every issue from #53 through #76 inclusive, and no other implementation issues.
- Target branch: `dev`.
- Process stories sequentially in dependency order derived from each issue's dependencies.

## Definition of done

The epic is complete only when every acceptance criterion is verified and checked, autoplay E2E passes, commits are story-scoped on `dev`, every story remains open and has GitHub Project status **In review**, every implementation or rework pass has its own preserved evidence comment with commit hash(es) and autoplay media, no human feedback is unresolved, and the worktree is clean.

## Required story workflow

- Use **In progress** as the active status and **In review** as the review status.
- If a processed story returns from **In review** to **In progress** with feedback, finish the currently active story and then prioritize the returned story.
- Require use and extension of the in-game autoplay harness under `LocalPackages/com.budgetgamedev.game.brocoli/Runtime/Autoplay/` and `Editor/Autoplay/`, driven by `unity run . -- -executeMethod BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run`. Each story must add or update a deterministic scenario that exercises real gameplay, fails nonzero on regression, and preserves diagnostics and artifacts. Never replace or bypass this harness.
- After reviewing the worker's changes, run the harness with `-build -tier smoke`, then with the story's own tier or scenario. Both must pass with results, telemetry, logs, and visuals. If the interface evolves, update commands and documentation while retaining this entry point. Also run focused tests, Unity compilation, and required Play Mode validation.
- Use screenshots or video produced by that same passing story-specific autoplay run as visible evidence. Reject manual, generic, stale, or unrelated-run evidence.
- Commit only the current story using a message containing `Refs #NN`; never use `Closes`, `Fixes`, or another auto-closing keyword.
- After every initial implementation or correction pass, add a new evidence comment to that individual story. Never edit or delete earlier evidence comments. Label the pass, reference feedback addressed, and include commit hash(es), exact E2E commands, scenarios, assertions, results, other validation, and uploaded autoplay screenshots or video. For corrections, compare the new evidence with the previous evidence.
- Verify the evidence comment and completion boxes, resolve all current feedback, then return the story to **In review** while leaving it open.

## Prohibitions and final validation

- Do not create branches, worktrees, pull requests, or parallel implementation tasks.
- Do not close or comment on parent epic #52, and do not close any child story.
- At apparent completion, refresh statuses and comments for #53–#76. Resume the sequential loop for anything returned to **In progress**.
- Otherwise run every maintained baseline and story scenario through the autoplay harness, the full relevant suite, and final Unity validation. Verify all evidence and statuses and confirm a clean worktree.

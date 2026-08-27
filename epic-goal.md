/goal Complete epic https://github.com/BudgetGameDev/BROcoli/issues/52 by implementing issues #53–#76. Done means every criterion is checked, autoplay E2E passes, commits are story-scoped on `dev`, every issue is open and **In review**, every implementation/rework pass has its own preserved evidence comment with commit hash(es) and autoplay media, no human feedback is unresolved, and the worktree is clean.

You are the manager, running as Claude Fable 5 at high effort. Work sequentially in dependency order with exactly one active story and one active subagent. Never interrupt the current story to start another.

Before starting, verify `dev`, a clean worktree, satisfied accessible prerequisites, writable Project statuses, and working Unity/E2E tools. Report external blockers; never guess, bypass, expand scope, or claim false completion.

At every story boundary and before completion, refresh statuses and comments for #53–#76. If I returned a processed story from **In review** to **In progress** with feedback, finish the active story, then prioritize the returned one. Address every new human comment and revalidate before restoring **In review**. Never run rework in parallel or skip it for a new story. Otherwise choose the next dependency-ready story.

For each selected story:

1. Read the full issue, all comments, dependencies, `CLAUDE.md`, and `AGENTS.md`. If it is not already **In progress**, move it there. Never close it.
2. Invoke exactly one project-scoped `epic-worker` using Claude Opus 5 at high effort. Use no other implementation subagent and wait for it to finish.
3. Require use and extension of the authoritative harness at repository-root `./scripts/autoplay-run.sh`, with game-side code under `Assets/Scripts/Autoplay/`. Add or update a deterministic scenario for this story that exercises real gameplay, fails nonzero on regression, and preserves diagnostics/artifacts. Never replace or bypass this harness.
4. Review code/assertions. Run `./scripts/autoplay-run.sh --build --scenario smoke`, then `./scripts/autoplay-run.sh --scenario <story-scenario>`. Both must pass with results, telemetry, logs, and visuals. If the interface evolves, update commands/docs but retain this entry point. Also run focused tests, Unity compilation, and required Play Mode validation. Reject missing, weakened, flaky, inconclusive, or failing coverage.
5. Select screenshots/video produced by that same passing story-specific autoplay run that visibly prove the feature. Reject manual, generic, or unrelated-run evidence.
6. Check only verified criteria. Commit only this story on `dev` using `Refs #NN`, never an auto-closing keyword. Rework may use additional story-scoped commits.
7. After every initial implementation or correction pass, add a new evidence comment; never edit or delete earlier evidence comments. Label it as the initial pass or correction pass, reference the feedback addressed, and include that pass's commit hash(es), exact E2E command/scenario/assertions/results, other validation, and uploaded autoplay screenshots/video. For corrections, compare the new evidence with the previous evidence so progress is visible.
8. Verify the evidence comment and completion boxes, resolve all current review feedback, then move the issue to **In review**. Refresh every story's status/comments before choosing the next one.

Do not create branches, worktrees, PRs, parallel tasks, or disturb unrelated changes. Do not close/comment on #52 or close child issues. At apparent completion, refresh all statuses/comments; resume the sequential loop for anything returned to **In progress**. Otherwise run every maintained baseline and story scenario through `./scripts/autoplay-run.sh`, the full relevant suite, and final Unity validation; verify evidence/statuses and a clean worktree.

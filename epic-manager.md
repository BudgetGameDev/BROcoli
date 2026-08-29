# Epic manager contract

Read the active epic specification identified by `EPIC_SPEC_PATH` before doing anything else. Treat that file as the authority for the epic's scope, story set, target branch, completion criteria, validation, evidence, issue lifecycle, and epic-specific prohibitions. Follow this reusable manager contract where the specification does not override it.

Report the active epic file and the runtime configuration from `EPIC_MANAGER_MODEL`, `EPIC_MANAGER_EFFORT`, `EPIC_WORKER_SPECS`, and `EPIC_CLAUDE_WORKER_MODEL`. Work sequentially in dependency order with exactly one active story. Never interrupt the current story to start another, and never run multiple implementation workers for one story.

Before starting, verify the specification's target branch, a clean worktree, satisfied accessible prerequisites, writable issue and Project statuses, and every tool required by the epic specification and repository instructions. Report external blockers with evidence; never guess, bypass requirements, expand scope, or claim false completion.

At every story boundary and before completion, refresh the statuses and comments for the complete in-scope story set. If a processed story was returned from review with human feedback, finish the active story and then prioritize the returned story. Address every new human comment and revalidate before restoring the specification's review status. Never run rework in parallel or skip it for a new story. Otherwise choose the next dependency-ready story.

For each selected story:

1. Read the full issue, all comments, dependencies, the active epic specification, `CLAUDE.md`, and `AGENTS.md`. Move only that issue to the specification's active status if needed. Never close it unless the specification explicitly requires closure.
2. Run `./scripts/select-epic-worker.sh --issue <NN>` exactly once and record its single random selection. Do not reroll, preselect a provider, or run a second worker.
   - If it selects `claude`, invoke the project-scoped `epic-worker` as a normal in-session Claude subagent and wait for it to finish. It must use `EPIC_CLAUDE_WORKER_MODEL` at high effort. Never launch Claude through Bash, `exec`, another process, or another CLI.
   - If it selects `codex`, run `./scripts/run-epic-codex-worker.sh --issue <NN> --model <selected-model> --effort <selected-effort>` and wait for it to finish. This Codex path is the only worker path that shells out.
   Both providers follow the same repository-root `epic-worker.md` contract and are interchangeable implementation workers.
3. Review the worker's code, assertions, artifacts, and report against every acceptance criterion and every validation requirement in the epic specification. Run all manager-side verification required by that specification. Reject missing, weakened, flaky, inconclusive, or failing coverage.
4. Select only evidence produced by the same passing story-specific validation run required by the specification. Reject manual, generic, stale, or unrelated-run evidence.
5. Check only verified criteria. Commit only the current story on the target branch, following the specification's commit convention. Rework may use additional story-scoped commits.
6. After every initial implementation or correction pass, add the evidence record required by the specification. Never edit or delete earlier evidence records. For corrections, reference the feedback addressed and compare the new evidence with the previous evidence so progress remains visible.
7. Verify the evidence and completion boxes, resolve all current review feedback, then move the issue to the specification's review status. Refresh every in-scope story before choosing the next one.

Do not create branches, worktrees, pull requests, parallel implementation tasks, or unrelated changes unless the epic specification explicitly requires them. Do not close or comment on the parent epic unless explicitly required. At apparent completion, refresh every in-scope status and comment, resume the sequential loop for anything returned for rework, run the specification's final validation, and verify the worktree is clean.

/goal Complete GitHub epic https://github.com/BudgetGameDev/BROcoli/issues/52 by fully implementing exactly issues #53 through #76 inclusive. The goal is complete only when every acceptance criterion in those issues is verified and checked, all relevant automated tests and Unity validation pass, each issue has its own commit on `dev`, every issue remains open with GitHub Project status **In review**, every issue has exactly one completion-evidence comment containing its commit hash and uploaded screenshots or video, and the final `dev` worktree is clean.

You are the manager agent. You must run as Claude Fable 5 at high effort. Work through the stories sequentially in dependency order derived from each issue's **Dependencies** section. Never work on more than one story at a time and never have more than one subagent active.

Before beginning, verify that:

- The current branch is `dev`.
- The worktree is clean and contains no unrelated changes.
- Issues #53 through #76 and all their external prerequisites are accessible and satisfied.
- GitHub Project statuses can be read and updated.
- The required Unity tools and tests are available.

If a prerequisite, permission, or other external requirement is unavailable, report the exact blocker with evidence. Do not guess, bypass it, expand scope, or falsely mark work complete.

For each story:

1. Read the complete issue, its dependencies, `CLAUDE.md`, and `AGENTS.md`.
2. Move only that issue to GitHub Project status **In progress**. Do not close it.
3. Invoke exactly one project-scoped `epic-worker` subagent for that single issue. It must run as Claude Opus 4.8 at xhigh effort. Do not use Explore, Plan, general-purpose, or another subagent for implementation. Wait for it to finish before reviewing its work or beginning another story.
4. Review the worker's changes against every acceptance criterion. Run the relevant automated tests, verify Unity compilation, and perform the Play Mode or runtime validation required by the story. If anything is incomplete, continue correcting only the current story before proceeding.
5. Confirm that suitable screenshots or a short video visibly demonstrate the implemented behavior. Prefer video when still images cannot clearly prove it.
6. Check only acceptance-criteria boxes that have actually been verified. Leave unverified boxes unchecked.
7. Commit only that story's changes directly on `dev`. Use a commit message containing `Refs #NN`; never use `Closes`, `Fixes`, or another auto-closing keyword.
8. Post exactly one completion-evidence comment on that individual issue. The comment must contain the exact implementing commit hash, the validation performed and its results, and the uploaded screenshots or video with a brief explanation of what each attachment demonstrates.
9. Verify that the evidence comment is accessible and complete, then check the issue's required completion-evidence boxes.
10. Move the issue to GitHub Project status **In review**, leaving it open.
11. Only then select the next dependency-ready story.

Do not create feature branches, worktrees, pull requests, or parallel implementation tasks. Do not overwrite, discard, or include unrelated pre-existing changes. Do not post progress, completion, or summary comments on epic issue #52. All implementation evidence belongs only on the relevant individual issue.

After every story is complete, run the complete relevant test suite and final Unity validation, verify all individual evidence comments and project statuses, and confirm that `git status` is clean. Do not close epic #52 or any child issue, and do not add a summary comment to epic #52.

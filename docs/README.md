# Project documentation

| Document | Read it when |
| --- | --- |
| [adding-a-game.md](adding-a-game.md) | Adding, removing, or restructuring a game package |
| [asset-acquisition.md](asset-acquisition.md) | Creating or generating a game asset that ships in a game package |
| [licensed-assets.md](licensed-assets.md) | Importing, encrypting, or restoring a restricted third-party asset |
| [machine-setup.md](machine-setup.md) | Setting up a new machine, the Unity CLI, or agent integration, or restarting a wedged Editor |
| [native-releases.md](native-releases.md) | Building Windows, macOS, and Linux players or publishing a GitHub Release |
| [verification-gates.md](verification-gates.md) | Changing CI, the pre-push hook, the workflow, or the deploy scripts |

These are deliberately kept out of `AGENTS.md`, which every agent loads. It keeps
the rules that must never be broken; the reasoning, mechanics and step-by-step
procedures live here and are read only when a task actually involves them.

## Staging builds

Builds from the `staging` branch are published to the canonical
[BROcoli staging build](https://budgetgamedev.github.io/BROcoli/BranchStaging/).
The same build is also published to the legacy
[`BranchMain` path](https://budgetgamedev.github.io/BROcoli/BranchMain/) during the
branch migration so existing links continue to work.

The `production` branch is the only branch that publishes the production build at
the [GitHub Pages root](https://budgetgamedev.github.io/BROcoli/). Staging deployments
must only update the `BranchStaging` and `BranchMain` folders on `gh-pages`; production
deployments preserve both folders while updating the root.

The `main` branch is retired from the tracked build and deployment configuration. New
integration work should target `staging`; the workflow and repository-managed
pre-push hook no longer target `main`.

## Production promotion

Promote a locally verified `staging` revision with a normal pull-request merge into
`production`; do not force-replace the production branch. The pre-push hook owns the
quality gate. The resulting GitHub workflow only builds the WebGL artifact required
by Pages and deploys it to the root of `gh-pages`; documentation, test-only, and
local-tooling changes skip the Pages build. Before deleting the retired `release`
branch, verify that the production workflow succeeds, the root `version.json`
references the promoted production commit, and both staging URLs still respond.

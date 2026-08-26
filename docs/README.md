# Project documentation

## Staging builds

Builds from the `staging` branch are published to the canonical
[BROcoli staging build](https://budgetgamedev.github.io/BROcoli/BranchStaging/).
The same build is also published to the legacy
[`BranchMain` path](https://budgetgamedev.github.io/BROcoli/BranchMain/) during the
branch migration so existing links continue to work.

The `release` branch remains the only branch that publishes the production build at
the [GitHub Pages root](https://budgetgamedev.github.io/BROcoli/). Staging deployments
must only update the `BranchStaging` and `BranchMain` folders on `gh-pages`; release
deployments preserve both folders while updating the root.

The `main` branch is retired from the tracked build and deployment configuration. New
integration work should target `staging`; the workflow and repository-managed
pre-push hook no longer target `main`.

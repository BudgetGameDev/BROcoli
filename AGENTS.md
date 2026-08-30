# Agent instructions

## Unity Editor automation

Always open the Unity Editor in automated mode by using the `unity-open` command
available on the shell `PATH`. Do not launch the Unity Editor directly through Unity
Hub, an editor executable, or another command.

## Project structure

Games are local Unity packages under `LocalPackages/`, mounted by `file:`
references in `Packages/manifest.json`. `Assets/` holds project-wide concerns
only — render pipeline settings, TextMesh Pro, the WebGL template, and build and
licensing tooling. Never put game content there.

Read [docs/adding-a-game.md](docs/adding-a-game.md) before adding or removing a
game or package, adding a scene or a `Resources/` file, or changing a package's
dependencies. Scene names and `Resources/` paths are global across the whole
build and have naming rules, the shared package may not name a specific game,
and a game's Unity dependencies belong in its own `package.json`.

## Verification gates

`dev` is ungated on purpose. Never wire CI, a pre-push gate, a workflow, a
commit hook, a watcher, or a scheduled job to verify `dev` or pull requests
targeting it — not even a "fast subset". This is a hard requirement, not a
default to be improved on.

Formatting, lint, and source-size regressions accumulate on `dev` and are caught
together at promotion to `staging` or `production`. Fix them there. That backlog
is not evidence the setup is broken. Running `./ci.sh`, or any individual check,
by hand at any time is fine.

Read [docs/verification-gates.md](docs/verification-gates.md) before changing
CI, the pre-push hook, the GitHub workflow, or the deploy scripts.

## Asset acquisition

Read [docs/asset-acquisition.md](docs/asset-acquisition.md) before creating or
generating any game asset: model, texture, sprite, sound, music, shader, VFX,
material, particle effect, font, UI element, or environment kit. It requires
searching for an existing free, license-compatible asset before making one, and
it records what must be verified and written down for each asset acquired. That
guide links to the per-category guides for 3D models, 2D art and kits, and audio.

Read it only when a task actually needs a new asset. Most work does not, and
these rules are long enough that loading them by default would crowd out the
task at hand.

Each game must keep one player-facing credits file under its namespaced
`Resources/<Game>/` directory as the canonical inventory of dependencies,
licenses, usage terms, and attributions. BROcoli's canonical record is
`LocalPackages/com.budgetgamedev.game.brocoli/Resources/Brocoli/Credits.txt`, which
is displayed by its Credits menu. Update that file in the same change whenever
an asset or dependency is acquired, updated, or removed; per-asset license files
and encrypted-package metadata remain the detailed provenance records.

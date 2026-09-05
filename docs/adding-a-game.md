# Adding a game

This project hosts many games. Each one is a local Unity package under
`LocalPackages/`, mounted through `Packages/manifest.json`, and discovered at
runtime by the hub. Adding or removing a game is a manifest edit — no central
registry file to update.

## Layout

```
LocalPackages/
  com.budgetgamedev.shared/        reusable runtime services + game registry contracts
  com.budgetgamedev.hub/           brand-neutral launcher UI
  com.budgetgamedev.game.brocoli/  a game
Packages/manifest.json             "com.budgetgamedev.game.x": "file:../LocalPackages/com.budgetgamedev.game.x"
Assets/                            project-wide only: URP settings, TMP, WebGL template, build tooling
```

`Assets/` holds nothing game-specific, and nothing game-specific may be added
there. It is for project-wide concerns only: render pipeline settings, TextMesh
Pro, the WebGL template, and build and licensing editor tooling. A game that
leaves the manifest takes its code, scenes, resources and licensed assets with
it, which only works while none of it lives in `Assets/`.

## Steps

1. **Create the package.** Copy the shape of `com.budgetgamedev.game.brocoli`:

   ```
   LocalPackages/com.budgetgamedev.game.<id>/
     package.json          name, version, displayName, unity, dependencies
                           displayName ends in "Game", e.g. "BROcoliGame"
     README.md CHANGELOG.md LICENSE.md
     Runtime/  <Asm>.asmdef  csc.rsp
     Editor/   <Asm>.Editor.asmdef  csc.rsp
     Tests/Editor/ <Asm>.Tests.asmdef  csc.rsp
     Scenes/       <Id>_MainMenu.unity, <Id>_*.unity
     Resources/<Id>/   game-owned resources, namespaced against collisions
   ```

   Depend on `com.budgetgamedev.shared`, plus any
   Unity packages the game needs. Declaring them here rather than in the project
   manifest is what makes them arrive and leave with the game.

2. **Mount it.** Add to `Packages/manifest.json`:

   ```json
   "com.budgetgamedev.game.<id>": "file:../LocalPackages/com.budgetgamedev.game.<id>"
   ```

   and add the package name to `testables` so its tests run.

3. **Register it.** `Assets > Create > Budget GameDev > Game Definition`, saved as
   `Resources/GameRegistry/<id>.asset` inside the package. Set the id, display
   name, description, icon and sort order; drag the main menu scene into **Main
   Menu Scene** and the rest into **Additional Scenes**.

4. **Sync scenes.** `Budget GameDev > Sync Build Scenes`. This runs automatically
   on import and before a build.

5. **Offer a way back.** Call `GameSession.ReturnToLauncher()` from the game's own
   main menu, showing the button only when `GameSession.LauncherAvailable` is true.

## Conventions that keep games from colliding

These matter because Unity flattens several namespaces across the whole project:

- **Scene names are global.** Prefix every scene with the game id
  (`Brocoli_Dungeon`), because `SceneManager.LoadScene` resolves by name.
- **Resources paths are global.** Put game resources under
  `Resources/<Id>/…` and load them as `"<Id>/…"`.
- **Assembly names and namespaces** follow `BudgetGameDev.Games.<Name>`.
- **Game package `displayName`s end in `Game`** (`"BROcoliGame"`), so game
  packages are recognizable at a glance in the Package Manager next to the hub
  and shared packages.
- **Tags, layers, physics and quality settings are project-wide.** Packages
  cannot own them; coordinate changes across games.
- **A game's Unity dependencies belong in its own `package.json`**, never moved
  into `Packages/manifest.json`. Declared on the package they resolve
  transitively and disappear when it is unloaded; declared on the project they
  linger after the game is gone.

## What stays shared

Put something in `com.budgetgamedev.shared` only if it names no game. Where
shared code needs game-specific values, inject them — see
`GameAudioSettings.Configure` and `IPauseController` for the two existing
patterns.

The same holds one level up: **the hub never references a game's code, scenes or
assemblies.** It reaches games only through the `GameDefinition` assets it
discovers at runtime. A direct reference would compile the game into the
launcher, so removing that game from the manifest would stop the project
building — which is exactly the coupling this layout exists to prevent.

## Selecting release content

Use `python scripts/release-build.py --product <id> --targets windows` for a
single game, or `--product launcher` for the launcher with all installed games.
The game id is the suffix of its package name. The selected game's main menu is
scene zero. The launcher always presents its picker; `LauncherConfig.txt` and
its startup-scene override have been removed.

Releases use a fresh staging project containing only the selected game packages
and their shared dependencies. Excluded game, hub and autoplay packages are
absent before Unity imports anything, so their code and Resources never enter
the compiler or linker. A dependency from a selected package to an excluded
package fails the build rather than silently restoring it. Do not reference the
hub assembly from game code; the shared assembly owns the GameDefinition,
GameCatalog and GameSession contracts (their existing namespace is preserved).

Autoplay belongs in `com.budgetgamedev.autoplay` (reusable core) and
`com.budgetgamedev.autoplay.<id>` (adapter). Game runtime code never references
the adapter. These packages are excluded from every release, including launcher
releases. See [native releases](native-releases.md) for commands and audit files.

## Removing a game

Delete its line from `manifest.json` (and `testables`). Its scenes leave Build
Settings on the next sync, its Unity dependencies stop resolving, its licensed
payloads disappear, and the launcher stops listing it.

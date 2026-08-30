# Adding a game

This project hosts many games. Each one is a local Unity package under
`LocalPackages/`, mounted through `Packages/manifest.json`, and discovered at
runtime by the hub. Adding or removing a game is a manifest edit — no central
registry file to update.

## Layout

```
LocalPackages/
  com.budgetgamedev.shared/        reusable runtime services (no game knowledge)
  com.budgetgamedev.hub/           brand-neutral launcher + registry
  com.budgetgamedev.game.brocoli/  a game
Packages/manifest.json             "com.budgetgamedev.game.x": "file:../LocalPackages/com.budgetgamedev.game.x"
Assets/                            project-wide only: URP settings, TMP, WebGL template, build tooling
```

`Assets/` holds nothing game-specific. A game that leaves the manifest takes its
code, scenes, resources and licensed assets with it.

## Steps

1. **Create the package.** Copy the shape of `com.budgetgamedev.game.brocoli`:

   ```
   LocalPackages/com.budgetgamedev.game.<id>/
     package.json          name, version, displayName, unity, dependencies
     README.md CHANGELOG.md LICENSE.md
     Runtime/  <Asm>.asmdef  csc.rsp
     Editor/   <Asm>.Editor.asmdef  csc.rsp
     Tests/Editor/ <Asm>.Tests.asmdef  csc.rsp
     Scenes/       <Id>_MainMenu.unity, <Id>_*.unity
     Resources/<Id>/   game-owned resources, namespaced against collisions
   ```

   Depend on `com.budgetgamedev.hub` and `com.budgetgamedev.shared`, plus any
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
   main menu.

## Conventions that keep games from colliding

These matter because Unity flattens several namespaces across the whole project:

- **Scene names are global.** Prefix every scene with the game id
  (`Brocoli_Dungeon`), because `SceneManager.LoadScene` resolves by name.
- **Resources paths are global.** Put game resources under
  `Resources/<Id>/…` and load them as `"<Id>/…"`.
- **Assembly names and namespaces** follow `BudgetGameDev.Games.<Name>`.
- **Tags, layers, physics and quality settings are project-wide.** Packages
  cannot own them; coordinate changes across games.

## What stays shared

Put something in `com.budgetgamedev.shared` only if it names no game. Where
shared code needs game-specific values, inject them — see
`GameAudioSettings.Configure` and `IPauseController` for the two existing
patterns.

## Removing a game

Delete its line from `manifest.json` (and `testables`). Its scenes leave Build
Settings on the next sync, its Unity dependencies stop resolving, its licensed
payloads disappear, and the launcher stops listing it.

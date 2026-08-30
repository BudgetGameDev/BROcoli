# Budget GameDev Game Hub

A brand-neutral launcher that lets one Unity project ship many games. The hub
lists the installed games, and hands control to the one the player picks.

## How a game registers

A game package ships one `GameDefinition` asset under any
`Resources/GameRegistry/` folder. That is the entire protocol — the hub never
references a game's code or scenes directly.

1. `Assets > Create > Budget GameDev > Game Definition`, saved to
   `<your package>/Resources/GameRegistry/<id>.asset`.
2. Fill in the id, display name, description, optional icon and sort order.
3. Drag the game's own main menu scene into **Main Menu Scene**, and every other
   scene it needs into **Additional Scenes**.
4. Run `Budget GameDev > Sync Build Scenes` (a build does this automatically).

Because discovery is a Resources scan, dropping the package from
`Packages/manifest.json` removes the game from the launcher with no other edit.

## Booting straight into a game

`LauncherConfig.txt` at the project root — beside `.env` — is a committed,
line-based config read when the launcher opens. With every setting commented out
— the default — the launcher shows its game list.

```
startupScene = Brocoli_Dungeon
```

opens that scene instead, once, the first time the launcher opens in a run. Use it
for a single-game build or to skip the picker while iterating on one game. Leaving
the game still returns to the list, so the other games stay reachable.

The value is a scene name, and the scene must be in the build. A misspelled or
removed name is reported and ignored rather than obeyed, so a stale config can
never leave a build unable to open. When a registered game declares the scene,
that game's configuration is applied first, so it starts exactly as if it had been
picked by hand. The file documents its own settings; see it for the full contract.

Nothing outside `Assets/` reaches a player, so an editor script mirrors the root
file into `Assets/Generated/Resources/` (git-ignored) and the launcher reads that
copy. The mirror is regenerated on editor load, on entering play mode, and before
every build, so the root file stays the only one worth editing. `Budget GameDev >
Sync Launcher Config` forces it by hand.

## Flow

`GameLauncher` (scene `GameLauncher`) builds a scrollable list of
`GameCatalog.All` and calls `GameSession.Launch`, which applies the game's audio
configuration and loads its main menu. Games call
`GameSession.ReturnToLauncher()` to come back.

The launcher deliberately carries no branding: each row shows only the game's
own name, icon and blurb, and every game brands itself in its own main menu.

## Dependencies

`com.budgetgamedev.shared`, uGUI, and the UI module.

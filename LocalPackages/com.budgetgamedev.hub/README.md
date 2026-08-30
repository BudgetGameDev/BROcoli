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

## Flow

`GameLauncher` (scene `GameLauncher`) builds a scrollable list of
`GameCatalog.All` and calls `GameSession.Launch`, which applies the game's audio
configuration and loads its main menu. Games call
`GameSession.ReturnToLauncher()` to come back.

The launcher deliberately carries no branding: each row shows only the game's
own name, icon and blurb, and every game brands itself in its own main menu.

## Dependencies

`com.budgetgamedev.shared`, uGUI, and the UI module.

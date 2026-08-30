# Pause Menu & Game-Over UI Setup

## Runtime UI

### PauseMenu.cs (`LocalPackages/com.budgetgamedev.game.brocoli/Runtime/UI/PauseMenu.cs`)

Handles the pause menu:

- Press **Escape** to toggle pause.
- Pauses gameplay with `Time.timeScale = 0`.
- Provides Resume and Main Menu buttons.

### GameOverOverlay.cs (`LocalPackages/com.budgetgamedev.game.brocoli/Runtime/UI/GameOverOverlay.cs`)

Builds the game-over interface inside the active `Game` scene when a run ends:

- Displays the final score, wave, and infinite-mode state.
- Provides controller/keyboard navigation.
- Restarts the `Game` scene or returns to `MainMenuScene`.
- Shows the GitHub call-to-action through `GameOverCTAManager`.

The overlay is created on demand, so it does not require a separate scene or
manual scene wiring.

## Pause Menu Scene Setup

1. Create a `PauseMenuCanvas` in `Game.unity`.
2. Add a full-screen panel and disable it by default.
3. Add the paused title, Resume button, and Main Menu button.
4. Add `PauseMenu` to a manager object and assign the panel.
5. Wire Resume to `PauseMenu.Resume` and Main Menu to
   `PauseMenu.GoToMainMenu`.

Use a `CanvasScaler` in “Scale With Screen Size” mode with a 1920×1080
reference resolution.

# Pause Menu & End Game UI Setup

## Scripts Created

### 1. PauseMenu.cs (`Assets/Scripts/PauseMenu.cs`)
Handles the pause menu functionality:
- Press **Escape** to toggle pause
- Pauses game using `Time.timeScale = 0`
- Resume and Main Menu buttons

### 2. EndGameUI.cs (`Assets/Scripts/EndGameUI.cs`)
Handles the end game screen buttons:
- **Restart** - Loads the "Game" scene
- **Main Menu** - Loads "MainMenuScene"

---

## Unity Editor Setup Required

### For Pause Menu (in Game.unity scene):

1. **Create PauseMenuCanvas:**
   - Right-click in Hierarchy → UI → Canvas
   - Name it "PauseMenuCanvas"
   - Set Canvas Scaler to "Scale With Screen Size" (1920x1080 reference)

2. **Create Panel (background):**
   - Right-click PauseMenuCanvas → UI → Panel
   - Name it "PausePanel"
   - Set Image color to semi-transparent black (0,0,0,0.7)
   - **Disable it by default** (uncheck Active in Inspector)

3. **Add Title Text:**
   - Right-click PausePanel → UI → Text - TextMeshPro
   - Set text to "PAUSED"
   - Center it at the top

4. **Add Resume Button:**
   - Right-click PausePanel → UI → Button - TextMeshPro
   - Set button text to "Resume"
   - Position above center

5. **Add Main Menu Button:**
   - Right-click PausePanel → UI → Button - TextMeshPro
   - Set button text to "Main Menu"
   - Position below Resume button

6. **Add PauseMenu Script:**
   - Create empty GameObject named "PauseMenuManager"
   - Add PauseMenu.cs component
   - Drag PausePanel to "Pause Menu UI" field

7. **Wire up buttons:**
   - Select Resume Button → OnClick() → Add PauseMenuManager → PauseMenu.Resume
   - Select Main Menu Button → OnClick() → Add PauseMenuManager → PauseMenu.GoToMainMenu

---

### For End Game Screen (in EndGame.unity scene):

1. **Create UI Canvas (if not exists):**
   - The scene already has a Canvas with "Game Over..." text
   - Add Canvas Scaler → Scale With Screen Size

2. **Create Buttons Panel:**
   - Create empty child under Canvas
   - Add Vertical Layout Group

3. **Add Restart Button:**
   - Right-click panel → UI → Button - TextMeshPro
   - Set text to "Restart"

4. **Add Main Menu Button:**
   - Right-click panel → UI → Button - TextMeshPro
   - Set text to "Main Menu"

5. **Add EndGameUI Script:**
   - Add EndGameUI.cs to Canvas or create empty manager object
   - Wire up buttons:
     - Restart Button → OnClick() → EndGameUI.Restart
     - Main Menu Button → OnClick() → EndGameUI.GoToMainMenu

6. **Remove/disable auto-return on keypress:**
   - The existing Canvas has a TextMeshPro component that returns on any key press
   - Either remove that script or modify the behavior

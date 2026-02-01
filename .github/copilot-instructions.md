# Copilot Instructions for BROcoli

Unity 2D survival game (WebGL-first, PWA-enabled) where a broccoli with a corona mask survives enemy waves.

## Primary Scenes
Work primarily in these three scenes (`Assets/Scenes/`):
- **MainMenuScene** - Title screen, play buttons (desktop vs mobile), PWA install prompt
- **Game** - Main gameplay with player, enemies, waves, pause menu
- **EndGame** - Game over screen with restart/main menu buttons

## Responsive Design (CRITICAL)
- **Landscape orientation only** - Game enforces horizontal layout via `ForceLandscapeAspect.cs`
- **Must scale correctly** on both desktop and mobile browsers
- UI uses `CanvasScaler` with "Scale With Screen Size" (1920x1080 reference, match 0.5)
- `VirtualController` repositions joystick/buttons based on portrait vs landscape detection
- Always test UI anchors and layouts at multiple aspect ratios (16:9, 18:9, 4:3)

## Architecture Overview

### Core Systems
- **Game State**: `GameStates` tracks score, time, and experience globally. Found via `FindFirstObjectByType<GameStates>()`
- **Player Stats**: `PlayerStats` component manages health, damage, speed, XP, level-ups. Uses `Bar` UI component for health/XP bars
- **Enemy System**: Abstract `EnemyBase` class extended by `EnemyScript` (melee) and `ShootingEnemyScript` (ranged). Enemies auto-find player via tag
- **Wave System**: `WaveGenerator` → spawns `EnemySpawner` instances per wave. Spawner uses exponential difficulty scaling

### Key Patterns

**Singleton Pattern** - Use for managers:
```csharp
public class MyManager : Singleton<MyManager> { }           // Lazy creation
public class MyPersistent : SingletonPersistent<MyPersistent> { }  // DontDestroyOnLoad
```

**Boost System** - Inheritance-based power-ups in `Assets/Scripts/Boost/`:
```csharp
public class MyBoost : BoostBase {
    public override float Amount => 10f;
    public override void Apply(PlayerStats stats) => stats.ApplyBoost(this);
}
```
Boosts auto-destroy after `_lifetime` seconds and trigger on player collision.

**Enemy Creation** - Extend `EnemyBase`:
- Set `TimeToStartSpawning` / `TimeToEndSpawning` for wave-based appearance
- Override `FixedUpdate()` for movement, call `base.FixedUpdate()` for separation forces
- Use `TakeDamage(damage, knockbackDirection)` for hits with knockback

## Scene Structure
- `MainMenuScene` → `Game` → `EndGame`
- Scene loading: `SceneManager.LoadScene("SceneName")` or by build index
- Pause uses `Time.timeScale = 0` (see `PauseMenu.cs`)

## Input & Platform
- **New Input System** with `TouchAction.inputactions` for cross-platform
- `VirtualController` auto-detects mobile via JavaScript interop (`IsMobileBrowser()`)
- `PlayerPrefs.GetInt("ShowVirtualController")`: 0=hide, 1=show, -1=auto-detect
- PWA support via `PWAHelper` static class for install prompts, fullscreen

## WebGL/Mobile Specifics
JavaScript interop pattern for WebGL:
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
[DllImport("__Internal")]
private static extern int IsMobileBrowser();
#endif
```

## Audio
Procedural audio components prefixed with `Procedural*Audio` (e.g., `ProceduralGunAudio`, `ProceduralFootstepAudio`). Attach to GameObjects that need audio feedback.

## UI Components
- `Bar` component wraps Unity `Slider` for health/XP bars
- Always call `EnsureEventSystemActive()` in pause menus (see `PauseMenu.cs` critical comment)
- Use TextMeshPro for all text elements

## Project Conventions
- C# scripts in `Assets/Scripts/`, organized by feature (Boost/, Player/, MainMenu/)
- Prefabs use `[SerializeField]` for inspector references, avoid public fields
- Player uses "Player" tag for enemy targeting

## Build & Deploy
- Primary target: WebGL (hosted at `budgetgamedev.github.io/BROcoli`)
- **Verify compilation**: Run `dotnet build .\unity-2.slnx` before committing
- Open in Unity Editor, build via File → Build Settings → WebGL
- PWA manifest and service worker in `Assets/WebGLTemplates/`
- Check Unity console logs in `%LOCALAPPDATA%\Unity\Editor\Editor.log` for runtime errors

## Verification & Testing (IMPORTANT)
After implementing UI or visual changes, **verify by running in Unity Editor and capturing a screenshot**:

1. **Open scene in Unity**: Use Unity Editor CLI or UI automation
   ```powershell
   # Example: Open Unity with specific scene
   & "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -projectPath . -openScene "Assets/Scenes/Game.unity"
   ```

2. **Enter Play Mode**: Use Unity's `-executeMethod` to run custom editor scripts
   ```csharp
   // Editor script example (Assets/Scripts/Editor/)
   [MenuItem("Tools/Enter Play Mode")]
   public static void EnterPlayMode() => EditorApplication.EnterPlaymode();
   ```

3. **Capture screenshot**: Use `ScreenCapture.CaptureScreenshot()` or editor automation
   ```csharp
   ScreenCapture.CaptureScreenshot("screenshot.png");
   ```

4. **Automated verification**: Create editor scripts in `Assets/Scripts/Editor/` for:
   - Scene validation (required GameObjects exist)
   - UI element positioning checks
   - Component reference validation

5. **Screenshot feedback loop**: After capturing a screenshot:
   - Save to project root or a known location (e.g., `screenshot.png`)
   - Attach the screenshot to the conversation for agent verification
   - Agent should analyze the screenshot to confirm:
     - UI elements are positioned correctly
     - Text is readable and not clipped
     - Layout scales properly (test at different resolutions)
     - Visual elements match the intended design
   - If issues are found, iterate on the implementation

**When to verify** (walk the verification tree based on complexity):
- **Simple code changes** (logic, bug fixes): `dotnet build` is sufficient
- **UI changes** (layout, positioning): Screenshot verification recommended
- **Cross-platform changes** (mobile/desktop): Full screenshot verification at multiple resolutions
- **New features**: Build + run + screenshot + manual testing

Prefer using Unity Test Framework (`com.unity.test-framework`) for automated scene/integration tests when possible.

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
- Open in Unity Editor, build via File → Build Settings → WebGL
- PWA manifest and service worker in `Assets/WebGLTemplates/`

## Build Verification (CRITICAL - Before Completing Any Task)

**The agent MUST verify builds succeed before marking any coding task complete.**

### Verification Tiers (Choose Based on Change Complexity)

| Change Type | Verification Required | Time |
|-------------|----------------------|------|
| **Trivial** (comments, formatting, docs) | None | 0s |
| **Simple** (single file logic change) | `dotnet build unity-2.slnx` | ~3s |
| **Moderate** (multiple files, new classes) | `dotnet build unity-2.slnx` | ~3s |
| **Complex** (Unity APIs, scenes, prefabs) | `./scripts/unity-build-check.sh` | ~30s |
| **Integration** (new packages, assets) | `./scripts/unity-build-check.sh` | ~30s |

### Quick Verification Commands

**For most code changes** (fast, catches 90% of errors):
```bash
dotnet build unity-2.slnx
```
*Note: Requires `Library/` folder to exist. If this fails with package errors, run `./scripts/unity-build-check.sh` first.*

**For Unity-specific changes** (scenes, prefabs, Input System, URP):
```bash
./scripts/unity-build-check.sh
```

### When `dotnet build` Works vs Doesn't

`dotnet build unity-2.slnx` **DOES catch:**
- Syntax errors
- Missing semicolons, braces
- Type mismatches
- Missing `using` statements for standard C#
- Method signature errors
- Most compile-time errors in YOUR code

`dotnet build unity-2.slnx` **DOES NOT catch:**
- Missing Unity package references (InputSystem, URP, etc.)
- Scene/prefab reference errors
- Unity-specific API availability
- Asset import errors

**Rule of thumb:** If you touched anything Unity-specific (scenes, Input System, shaders, URP), use the full Unity build check.

## Unity CLI Compilation (Headless/Batch Mode)

**IMPORTANT:** `dotnet build unity-2.slnx` does NOT work for Unity projects with package dependencies (InputSystem, URP, etc.). Use Unity's batch mode instead.

### Quick Verification
Use the provided script:
```bash
./scripts/unity-build-check.sh
```

### Unity Editor Paths by Platform

**macOS:**
```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
# Example: /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity
```

**Windows:**
```powershell
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
# Example: "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
```

**Linux:**
```bash
~/Unity/Hub/Editor/<version>/Editor/Unity
# Example: ~/Unity/Hub/Editor/6000.3.6f1/Editor/Unity
```

### Batch Mode Compilation Command

**macOS/Linux:**
```bash
/path/to/Unity -batchmode -projectPath /path/to/unity-2 -buildTarget WebGL -logFile /tmp/unity_build.log -quit
```

**Windows (PowerShell):**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -projectPath . -buildTarget WebGL -logFile "$env:TEMP\unity_build.log" -quit
```

### Reading Build Logs

**Check for success:**
```bash
# Success indicator
grep "Exiting batchmode successfully" /tmp/unity_build.log

# Check for script errors in YOUR code (not package cache)
grep "Assets/Scripts.*error CS" /tmp/unity_build.log

# Check for warnings
grep "Assets/Scripts.*warning CS" /tmp/unity_build.log
```

**Log file locations:**
- macOS/Linux: Use `-logFile /tmp/unity_build.log` or `-logFile -` for stdout
- Windows: Use `-logFile "$env:TEMP\unity_build.log"` 
- Default (no -logFile): `%LOCALAPPDATA%\Unity\Editor\Editor.log` (Windows) or `~/Library/Logs/Unity/Editor.log` (macOS)

### Interpreting Build Results

| Log Message | Meaning |
|-------------|---------|
| `Exiting batchmode successfully now!` | ✅ Build succeeded |
| `Scripts have compiler errors.` | ❌ Compilation failed |
| `error CS####:` in `Assets/Scripts/` | ❌ Error in YOUR code - fix it |
| `error CS####:` in `Library/PackageCache/` | ⚠️ Package cache corruption - see Clean Rebuild |

## Clean Rebuild Process (LAST RESORT ONLY)

**⚠️ Clean rebuilds take 2-5 minutes. Only use when all other options are exhausted!**

### Troubleshooting Order (Try These First!)

1. **First:** Run `dotnet build unity-2.slnx` - catches most code errors in ~3 seconds
2. **Second:** Run `./scripts/unity-build-check.sh` - full Unity compilation in ~30 seconds
3. **Third:** Check if the error is in YOUR code (`Assets/Scripts/`) or package cache (`Library/PackageCache/`)
4. **Fourth:** If error is in your code, FIX IT - don't clean rebuild!
5. **LAST RESORT:** Clean rebuild only if errors are in `Library/PackageCache/` with no code changes

### Symptoms that ACTUALLY require a clean rebuild:
- Errors in `Library/PackageCache/` (not your code) AND you made no code changes
- "The type or namespace name 'X' does not exist" for Unity packages after package updates
- Build worked before, you reverted all changes, still fails
- Corrupt meta files or asset database

### Symptoms that DO NOT require clean rebuild (fix code instead!):
- Errors in `Assets/Scripts/` - these are YOUR bugs, fix them!
- Missing references after renaming/moving files - update the references
- New compile errors after your changes - your code has bugs

### ⚠️ SAFETY GUARDRAILS (CRITICAL)

**NEVER run `rm -rf` or `Remove-Item -Recurse` on paths outside the Unity project directory.**

Before running any destructive delete command:
1. **Verify you are in the project root**: Run `pwd` and confirm it is the correct project
2. **Only delete these folders** (all are gitignored and regenerable):
   - `Library/` - Unity's cache (safe to delete entirely)
   - `Library/PackageCache/` - Downloaded packages
   - `Library/Bee/` - Build cache
   - `Library/ScriptAssemblies/` - Compiled scripts
   - `Temp/` - Temporary build files
   - `Logs/` - Editor logs
3. **Use relative paths only**: `rm -rf Library/` NOT `rm -rf /Users/.../Library/`
4. **Never delete**: `Assets/`, `Packages/manifest.json`, `ProjectSettings/`, or any code

**Safe delete patterns:**
```bash
# ✅ SAFE - relative paths within project
rm -rf Library/
rm -rf Temp/
rm -f Packages/packages-lock.json

# ❌ DANGEROUS - never use absolute paths or parent traversal
rm -rf /Users/user/Library/        # WRONG - system Library!
rm -rf ../                          # WRONG - parent directory
rm -rf ~/Library/                   # WRONG - user Library folder
```

### Step 1: Clean the Library Folder

**macOS/Linux:**
```bash
# First, verify you're in the project directory
pwd  # Should show: .../unity-2

# Then clean (relative paths only)
rm -rf Library/
rm -f Packages/packages-lock.json
```

**Windows (PowerShell):**
```powershell
# First, verify you're in the project directory
Get-Location  # Should show: ...\unity-2

# Then clean (relative paths only)
Remove-Item -Recurse -Force Library
Remove-Item -Force Packages\packages-lock.json -ErrorAction SilentlyContinue
```

### Step 2: Rebuild (Allow Extra Time)

First build after cleaning takes **2-5 minutes** as Unity must:
1. Download all packages from Unity Package Registry
2. Import all assets
3. Compile all scripts

```bash
# macOS/Linux - with progress monitoring
/path/to/Unity -batchmode -projectPath . -buildTarget WebGL -logFile /tmp/unity_rebuild.log -quit &
tail -f /tmp/unity_rebuild.log | grep -E "(Package|Compil|error|Import)"
```

```powershell
# Windows - run and wait
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -projectPath . -buildTarget WebGL -logFile "$env:TEMP\unity_rebuild.log" -quit
Get-Content "$env:TEMP\unity_rebuild.log" -Tail 50
```

### Step 3: Verify Success

```bash
# Check for compiled assemblies
ls -la Library/ScriptAssemblies/Assembly-CSharp*

# Should see:
# Assembly-CSharp.dll (your game code)
# Assembly-CSharp.pdb (debug symbols)
# Assembly-CSharp-Editor.dll (editor scripts)
```

### Partial Clean (Faster, Less Thorough)

If full clean is too slow, try cleaning only specific folders:

```bash
# Just clear compilation cache (keeps packages)
rm -rf Library/Bee
rm -rf Library/ScriptAssemblies

# Clear package cache only (keeps asset imports)
rm -rf Library/PackageCache
rm -f Packages/packages-lock.json
```

### CI/CD Considerations

For automated builds, always start with a clean Library or use Unity's cache server:

```bash
# CI script example
if [ ! -d "Library/PackageCache" ]; then
    echo "First build - expect 3-5 minute package download"
fi

/path/to/Unity -batchmode -projectPath . -buildTarget WebGL -logFile build.log -quit
EXIT_CODE=$?

if grep -q "Exiting batchmode successfully" build.log; then
    echo "✅ Build passed"
    exit 0
else
    echo "❌ Build failed"
    grep "error CS" build.log
    exit 1
fi
```

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

## Agent Workflow (CRITICAL)

**Always use a manager/worker subagent pattern for all tasks:**

1. **Top-level Manager Agent** - Remains long-running and orchestrates the overall workflow:
   - Receives the user request and breaks it down into discrete tasks
   - Maintains context and tracks progress across subtasks
   - Delegates work to task worker subagents
   - Synthesizes results and handles cross-task dependencies
   - Reports final status to user

2. **Task Worker Subagents** - Spin off for each discrete unit of work:
   - Receive specific, scoped tasks from the manager
   - Execute the task independently (e.g., implement feature, fix bug, write test)
   - Return results/status to the manager agent
   - Terminate upon task completion

**Workflow Pattern:**
```
User Request
    ↓
[Manager Agent] ← persists throughout session
    ├── [Subagent: Task 1] → completes → returns result
    ├── [Subagent: Task 2] → completes → returns result
    └── [Subagent: Task 3] → completes → returns result
    ↓
Manager synthesizes results → User Response
```

**Guidelines:**
- Manager should never directly implement features; always delegate to subagents
- Each subagent should have a clear, single responsibility
- Subagents can be spun up in parallel for independent tasks
- Manager handles error recovery and re-delegation if a subagent fails
- Use subagents even for "simple" tasks to maintain consistency

**Manager TODO Tracking (CRITICAL):**
The manager agent MUST maintain and update a TODO list throughout the session to track progress:

```
## Current TODO
- [ ] Task 1: Description (status: pending/in-progress/blocked)
- [x] Task 2: Description (status: completed)
- [ ] Task 3: Description (status: pending)

## Completed
- [x] Task 2: Brief result summary
```

- Update the TODO list after each subagent completes or fails
- Include task status: pending, in-progress, completed, blocked, failed
- Note dependencies between tasks
- Summarize results from completed tasks
- This helps maintain context across long sessions and prevents losing track of work

**Code File Size Limit:**
All changed `.cs` files MUST be maximum 300 lines of code. If a file exceeds 300 LOC, refactor it into smaller files (each max 300 LOC).

## Direct Scene Editing (CRITICAL)

**The agent MUST directly edit Unity scene files - never ask the human to make scene changes manually.**

Unity scene files (`.unity`) are YAML-based text files that can be edited directly:
- `Assets/Scenes/Game.unity` - Main gameplay scene
- `Assets/Scenes/MainMenuScene.unity` - Title/menu scene
- `Assets/Scenes/EndGame.unity` - Game over scene

**What the agent should do:**
- Add/remove/modify GameObjects by editing the `.unity` file directly
- Adjust component properties (transforms, references, settings)
- Add new UI elements, sprites, or prefab instances
- Wire up component references and event handlers
- Modify RectTransform anchors, positions, and sizes

**What the agent should NOT do:**
- ❌ "Please open Unity and add a Button to the Canvas"
- ❌ "You'll need to manually drag the prefab into the scene"
- ❌ "Go to the Inspector and change the value to X"

**Instead, the agent should:**
- ✅ Edit the `.unity` file to add the Button GameObject with all required components
- ✅ Add the prefab reference directly in the scene YAML
- ✅ Modify the serialized property value in the scene file

**Scene file structure basics:**
```yaml
--- !u!1 &123456789          # GameObject (ClassID 1)
GameObject:
  m_Name: MyObject
  m_Component:
  - component: {fileID: 987654321}  # Reference to component

--- !u!224 &987654321        # RectTransform (ClassID 224)
RectTransform:
  m_AnchoredPosition: {x: 0, y: 0}
```

**Tips for scene editing:**
- Use existing GameObjects as templates for new ones
- Generate unique `fileID` values (use large random numbers)
- Maintain proper component references between GameObjects
- Verify changes compile with `dotnet build .\unity-2.slnx`

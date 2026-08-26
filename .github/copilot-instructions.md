# Copilot Instructions for BROcoli

Unity 2D survival game (WebGL-first, PWA-enabled) where a broccoli with a corona mask survives enemy waves.

## Primary Scenes
Work primarily in these two scenes (`Assets/Scenes/`):
- **MainMenuScene** - Title screen, play buttons (desktop vs mobile), PWA install prompt
- **Game** - Main gameplay with player, enemies, waves, pause menu

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
- `MainMenuScene` → `Game`; run results use an in-scene `GameOverOverlay`
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

**The agent MUST complete the required verification before marking any coding task complete.**

### Verification Tiers (Choose Based on Change Complexity)

| Change Type | Verification Required |
|-------------|----------------------|
| **Trivial** (comments, formatting, docs) | None |
| **C# code** (including single-file logic changes) | `./ci.sh` |
| **Unity content** (scenes, prefabs, shaders, settings) | `./ci.sh` plus the relevant runtime or visual check |
| **Integration** (new packages, assets, build configuration) | `./ci.sh` plus a real target build or focused integration check |

### Authoritative Compilation Check

Use the unified repository gate for all non-trivial source changes:

```bash
./ci.sh
```

From Windows, run the unified gate in Git Bash. For a standalone PowerShell
compilation check only, use:

```powershell
.\scripts\unity-build-check.ps1
```

The gate performs formatting, lint, static analysis, source-size checks, and Unity
compilation. It recompiles through a connected Editor for this project when one is
available; otherwise the batch checker opens the editor with the WebGL target. The
compilation scripts read the required editor version from
`ProjectSettings/ProjectVersion.txt`. The check resolves packages, imports assets,
and compiles scripts, but does not produce a deployable WebGL player; the GitHub
Actions workflow performs the full WebGL build.

All new first-party source files are limited to 300 physical lines. The legacy
ceilings in `.quality/loc-baseline.tsv` may only decrease and must be removed as
files are reduced to 300 lines or fewer.

### Why `dotnet build` Is Not a Repository Verification Step

Unity generates `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` locally.
They are intentionally gitignored, are absent from a fresh clone, and may contain
machine-specific references into `Library/PackageCache`. The checked-in `.slnx`
files are therefore IDE conveniences only after Unity has generated current project
files.

`Packages/manifest.json` declares direct dependencies and the tracked
`Packages/packages-lock.json` pins the resolved graph. That pair, verified by Unity
compilation and the player build, is the package-compatibility contract. Preserve the
lockfile during cache cleanup; regenerating it is a dependency change, not routine
troubleshooting.

Running `dotnet build` can still be useful as optional local editor feedback when
those generated files are current, but it is not reproducible from a clean checkout,
does not validate Unity asset import or serialization, and must never replace the
Unity compilation check above.

### Full WebGL Build

The CI workflow performs the authoritative player build with
`game-ci/unity-builder`. To reproduce a player build locally, build the WebGL target
from Unity rather than treating a successful batch-mode compile check as a player
artifact.

## Unity CLI Compilation (Headless/Batch Mode)

### Quick Verification

Use the provided script:

```bash
./scripts/unity-build-check.sh
```

### Unity Editor Paths by Platform

**macOS:**
```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
~/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
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

### Reading Build Logs

**Check for success:**
```bash
# Success indicator
grep "Exiting batchmode successfully" /tmp/unity_build_check.log

# Check for script errors in YOUR code (not package cache)
grep "Assets/Scripts.*error CS" /tmp/unity_build_check.log

# Check for warnings
grep "Assets/Scripts.*warning CS" /tmp/unity_build_check.log
```

**Log file locations:**
- macOS/Linux script: `/tmp/unity_build_check.log`
- Windows: Use `-logFile "$env:TEMP\unity_build.log"` 
- Default (no -logFile): `%LOCALAPPDATA%\Unity\Editor\Editor.log` (Windows) or `~/Library/Logs/Unity/Editor.log` (macOS)

### Interpreting Build Results

| Log Message | Meaning |
|-------------|---------|
| `Exiting batchmode successfully now!` | ✅ Compilation/import check succeeded |
| `Scripts have compiler errors.` | ❌ Compilation failed |
| `error CS####:` in `Assets/Scripts/` | ❌ Error in YOUR code - fix it |
| `error CS####:` in `Library/PackageCache/` | ❌ Diagnose package/API compatibility first; reset only the generated cache if corruption is established |

## Clean Rebuild Process (LAST RESORT ONLY)

**⚠️ Clean rebuilds take 2-5 minutes. Only use when all other options are exhausted!**

### Troubleshooting Order (Try These First!)

1. **First:** Run `./scripts/unity-build-check.sh` (or the PowerShell equivalent)
2. **Second:** Check whether the error is in project code (`Assets/`) or a package (`Library/PackageCache/`)
3. **Third:** For a package error, verify its API against the versions pinned in `Packages/packages-lock.json`
4. **LAST RESORT:** Delete only generated caches after compatibility and code changes have been ruled out; preserve the package lockfile

### Symptoms that ACTUALLY require a clean rebuild:
- Package files differ from a fresh resolution of the already-pinned lockfile
- Build worked before, you reverted all changes, still fails
- Corrupt meta files or asset database

### Symptoms that DO NOT require clean rebuild (fix code instead!):
- Errors in `Assets/Scripts/` - these are YOUR bugs, fix them!
- Missing references after renaming/moving files - update the references
- New compile errors after your changes - your code has bugs
- Package API errors after changing `manifest.json` or `packages-lock.json` - resolve
  the dependency compatibility issue and review the lockfile diff

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
4. **Never delete during cache cleanup**: `Assets/`, `Packages/manifest.json`,
   `Packages/packages-lock.json`, `ProjectSettings/`, or any code

**Safe delete patterns:**
```bash
# ✅ SAFE - relative paths within project
rm -rf Library/
rm -rf Temp/

# ❌ DANGEROUS - never use absolute paths or parent traversal
rm -rf /Users/user/Library/        # WRONG - system Library!
rm -rf ../                          # WRONG - parent directory
rm -rf ~/Library/                   # WRONG - user Library folder
```

### Step 1: Clean the Library Folder

**macOS/Linux:**
```bash
# First, verify you're in the project directory
pwd  # Should show: .../BROcoli

# Then clean (relative paths only)
rm -rf Library/
```

**Windows (PowerShell):**
```powershell
# First, verify you're in the project directory
Get-Location  # Should show: ...\BROcoli

# Then clean (relative paths only)
Remove-Item -Recurse -Force Library
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
- **Simple code changes** (logic, bug fixes): Unity compilation check
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
New first-party source files MUST be no more than 300 physical lines. A file listed
in `.quality/loc-baseline.tsv` predates this limit: it may shrink but must not grow,
and its entry must be removed as soon as it reaches 300 lines. Other oversized files
hard-fail `./ci.sh`.

## Direct Scene Editing (CRITICAL)

**The agent MUST directly edit Unity scene files - never ask the human to make scene changes manually.**

Unity scene files (`.unity`) are YAML-based text files that can be edited directly:
- `Assets/Scenes/Game.unity` - Main gameplay scene
- `Assets/Scenes/MainMenuScene.unity` - Title/menu scene

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
- Verify changes with `./ci.sh`

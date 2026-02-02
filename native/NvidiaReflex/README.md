# NVIDIA Reflex Plugin for Unity (Streamline SDK)

This folder contains the native plugin for NVIDIA Reflex integration using the **Streamline SDK**.

## Overview

This plugin provides NVIDIA Reflex Low Latency Mode for Unity games on Windows. It uses the 
[Streamline SDK](https://github.com/NVIDIA-RTX/Streamline) which is NVIDIA's recommended approach 
for integrating Reflex in modern game engines.

## Prerequisites

1. **NVIDIA GPU** - GeForce GTX 900 series or newer
2. **Windows 10/11** - Reflex is Windows-only
3. **CMake 3.20+** - For building the native plugin (`winget install cmake`)
4. **Visual Studio 2019+** - MSVC compiler with C++ workload

## Quick Start (New Dev Machine)

Run the build script from PowerShell:

```powershell
cd Assets/Plugins/NvidiaReflex
./build-reflex-plugin.ps1
```

**That's it!** The script automatically:
1. Downloads the pinned Streamline SDK version from GitHub
2. Extracts it to `external/Streamline/` (gitignored)
3. Configures and builds with CMake
4. Copies all required DLLs to `Assets/Plugins/x86_64/`

## Version Management

The SDK version is pinned in `streamline-config.json`:

```json
{
    "version": "2.4.10",
    "repository": "NVIDIA-RTX/Streamline"
}
```

To update the SDK version:
1. Edit `streamline-config.json` and change `version`
2. Run `./build-reflex-plugin.ps1 -ForceDownload`
3. Test thoroughly before committing the version change

## Build Script Options

```powershell
# Standard release build
./build-reflex-plugin.ps1

# Debug build
./build-reflex-plugin.ps1 -Configuration Debug

# Force re-download SDK
./build-reflex-plugin.ps1 -ForceDownload

# Clean rebuild
./build-reflex-plugin.ps1 -Clean

# Only download SDK, skip build
./build-reflex-plugin.ps1 -SkipBuild
```

## File Structure

```
project-root/
├── external/
│   └── Streamline/              <- Downloaded SDK (gitignored)
│       ├── include/
│       │   ├── sl.h
│       │   ├── sl_reflex.h
│       │   └── sl_pcl.h
│       └── bin/x64/
│           ├── sl.interposer.dll
│           ├── sl.reflex.dll
│           └── sl.pcl.dll
│
├── Assets/
│   ├── Plugins/
│   │   ├── NvidiaReflex/
│   │   │   ├── CMakeLists.txt
│   │   │   ├── StreamlineReflexPlugin.cpp
│   │   │   ├── streamline-config.json   <- Version pin (tracked)
│   │   │   ├── build-reflex-plugin.ps1
│   │   │   ├── build/                   <- CMake build (gitignored)
│   │   │   └── README.md
│   │   └── x86_64/
│   │       ├── StreamlineReflexPlugin.dll  (gitignored)
│   │       ├── sl.interposer.dll           (gitignored)
│   │       ├── sl.reflex.dll               (gitignored)
│   │       └── sl.pcl.dll                  (gitignored)
│   │
│   └── Scripts/
│       ├── StreamlineReflexPlugin.cs    <- C# wrapper
│       └── FrameRateOptimizer.cs        <- Integration
```

## How It Works

### Initialization
1. `FrameRateOptimizer.Awake()` calls `StreamlineReflexPlugin.Initialize()`
2. Plugin loads Streamline SDK and checks for NVIDIA GPU
3. If Reflex is supported, enables Low Latency + Boost mode

### Per-Frame Flow
1. `BeginFrame()` - Start of frame, before input
2. `Sleep()` - Reflex CPU-GPU sync (reduces render queue)
3. `MarkSimulationStart()` - Before game logic
4. `MarkSimulationEnd()` - After game logic
5. `MarkRenderSubmitStart()` - Before rendering
6. `MarkRenderSubmitEnd()` - After rendering

## Troubleshooting

### "CMake not found"
```powershell
winget install cmake
```
Or download from https://cmake.org/download/

### "Visual Studio not found"
Install Visual Studio 2019 or 2022 with "Desktop development with C++" workload.

### "Failed to download SDK"
Check internet connection. If corporate firewall blocks GitHub:
1. Download manually from https://github.com/NVIDIA-RTX/Streamline/releases
2. Extract to `external/Streamline/`
3. Run `./build-reflex-plugin.ps1`

### "Reflex not supported"
- Requires NVIDIA GeForce GTX 900 series or newer
- Update to latest NVIDIA drivers (461.09+)
- Must use DirectX 11/12 (not Vulkan/OpenGL in Unity)

### Build errors with Streamline headers
Some Streamline SDK versions have minor header issues. The CMakeLists.txt 
disables warnings-as-errors to handle this.

## References

- [Streamline SDK GitHub](https://github.com/NVIDIA-RTX/Streamline)
- [NVIDIA Reflex Overview](https://developer.nvidia.com/reflex)
- [Streamline Programming Guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuide.md)
- [Reflex Integration Guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideReflex.md)

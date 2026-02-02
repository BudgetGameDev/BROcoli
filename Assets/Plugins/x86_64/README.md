# x86_64 Native Plugins

Place native Windows 64-bit DLLs here.

## NVIDIA Streamline SDK Plugins

These DLLs are built/copied automatically by running:
```powershell
cd native\NvidiaReflex
.\build-reflex-plugin.ps1
```

### Core Plugin
- `StreamlineReflexPlugin.dll` - Unity wrapper for Streamline SDK (Reflex + DLSS)

### Streamline Runtime DLLs
- `sl.interposer.dll` - Streamline SDK core
- `sl.reflex.dll` - Reflex Low Latency
- `sl.pcl.dll` - PC Latency Stats
- `sl.dlss.dll` - DLSS Super Resolution
- `sl.dlss_g.dll` - DLSS Frame Generation

### NVIDIA NGX Model Files
- `nvngx_dlss.dll` (~52MB) - DLSS neural network model
- `nvngx_dlssg.dll` (~7MB) - Frame Generation neural network model

## Feature Requirements

| Feature | GPU Requirement |
|---------|-----------------|
| Reflex Low Latency | Any NVIDIA GPU (GeForce 900+) |
| DLSS Super Resolution | RTX 20, 30, 40 series |
| Frame Generation | RTX 40 series or newer |
| 4x Frame Generation | RTX 50 series |

## Unity Import Settings

Unity should automatically detect these as native plugins for Windows x64.
If not, select the DLL in Unity and set:
- Platform: Windows
- CPU: x86_64
- Load on startup: true

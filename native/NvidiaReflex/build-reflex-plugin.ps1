# Build Streamline Reflex Plugin for Unity
# This script downloads the Streamline SDK and builds the native plugin DLL
#
# Prerequisites:
#   - Visual Studio 2019 or 2022 with C++ workload
#   - CMake 3.20 or newer (can be installed via 'winget install cmake')
#
# Usage:
#   .\build-reflex-plugin.ps1                    # Build Release
#   .\build-reflex-plugin.ps1 -Configuration Debug
#   .\build-reflex-plugin.ps1 -Clean             # Clean and rebuild
#   .\build-reflex-plugin.ps1 -ForceDownload     # Re-download SDK

param(
    [ValidateSet("Debug", "Release", "RelWithDebInfo")]
    [string]$Configuration = "Release",
    
    [switch]$Clean,
    
    [switch]$ForceDownload,
    
    [switch]$SkipBuild,
    
    [string]$VSVersion = "auto"
)

$ErrorActionPreference = "Stop"

# Paths - native/NvidiaReflex is now at project root level
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginDir = $ScriptDir
$ProjectRoot = Resolve-Path "$ScriptDir/../.."
$ExternalDir = "$ProjectRoot/external"
$StreamlineDir = "$ExternalDir/Streamline"
$BuildDir = "$PluginDir/build"
$OutputDir = "$ProjectRoot/Assets/Plugins/x86_64"
$ConfigFile = "$PluginDir/streamline-config.json"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Streamline Reflex Plugin Build Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Load configuration
if (-not (Test-Path $ConfigFile)) {
    Write-Host "ERROR: Configuration file not found: $ConfigFile" -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigFile | ConvertFrom-Json
$sdkVersion = $config.version
$repository = $config.repository

Write-Host "SDK Version: $sdkVersion"
Write-Host "Configuration: $Configuration"
Write-Host "Plugin Dir: $PluginDir"
Write-Host "Streamline Dir: $StreamlineDir"
Write-Host "Output Dir: $OutputDir"
Write-Host ""

# ============================================
# HELPER FUNCTIONS
# ============================================

function Test-Command($Command) {
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Get-StreamlineSDK {
    param(
        [string]$Version,
        [string]$DestDir,
        [switch]$Force
    )
    
    $versionMarker = "$DestDir/.version"
    $currentVersion = ""
    
    if (Test-Path $versionMarker) {
        $currentVersion = Get-Content $versionMarker -Raw
        $currentVersion = $currentVersion.Trim()
    }
    
    # Check if we need to download
    if (-not $Force -and (Test-Path "$DestDir/include/sl.h") -and ($currentVersion -eq $Version)) {
        Write-Host "Streamline SDK v$Version already installed" -ForegroundColor Green
        return $true
    }
    
    Write-Host "Downloading Streamline SDK v$Version..." -ForegroundColor Yellow
    
    # Create external directory
    if (-not (Test-Path $ExternalDir)) {
        New-Item -ItemType Directory -Path $ExternalDir | Out-Null
    }
    
    # Clean existing installation
    if (Test-Path $DestDir) {
        Write-Host "  Removing existing SDK..." -ForegroundColor Gray
        Remove-Item -Recurse -Force $DestDir
    }
    
    # Download URL - use config URL first, then try fallback patterns
    $downloadUrls = @(
        "https://github.com/$repository/releases/download/v$Version/streamline-sdk-v$Version.zip",
        "https://github.com/$repository/releases/download/v$Version/Streamline-$Version.zip",
        "https://github.com/$repository/releases/download/v$Version/streamline-$Version.zip",
        "https://github.com/$repository/releases/download/v$Version/Streamline_SDK_$Version.zip"
    )
    
    $tempZip = "$env:TEMP/Streamline-$Version.zip"
    $tempExtract = "$env:TEMP/Streamline-$Version-extract"
    $downloaded = $false
    
    foreach ($url in $downloadUrls) {
        try {
            Write-Host "  Trying: $url" -ForegroundColor Gray
            
            # Use TLS 1.2
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            
            $webClient = New-Object System.Net.WebClient
            $webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
            $webClient.DownloadFile($url, $tempZip)
            
            if (Test-Path $tempZip) {
                $downloaded = $true
                Write-Host "  Downloaded successfully" -ForegroundColor Green
                break
            }
        } catch {
            Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Gray
        }
    }
    
    if (-not $downloaded) {
        Write-Host ""
        Write-Host "ERROR: Failed to download Streamline SDK v$Version" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please download manually from:" -ForegroundColor Yellow
        Write-Host "  https://github.com/$repository/releases" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Extract to: $DestDir" -ForegroundColor Yellow
        return $false
    }
    
    # Extract
    Write-Host "  Extracting..." -ForegroundColor Gray
    
    if (Test-Path $tempExtract) {
        Remove-Item -Recurse -Force $tempExtract
    }
    
    Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force
    
    # Find the actual SDK root (might be nested in a folder)
    $extractedItems = Get-ChildItem $tempExtract
    $sdkRoot = $tempExtract
    
    if ($extractedItems.Count -eq 1 -and $extractedItems[0].PSIsContainer) {
        $sdkRoot = $extractedItems[0].FullName
    }
    
    # Check if this is a source release (has include/) or needs building
    if (Test-Path "$sdkRoot/include/sl.h") {
        # Binary release - move directly
        Move-Item $sdkRoot $DestDir
    } elseif (Test-Path "$sdkRoot/source") {
        # Source release - we need the prebuilt binaries
        Write-Host ""
        Write-Host "WARNING: Downloaded source release, not binary release" -ForegroundColor Yellow
        Write-Host "Looking for prebuilt binaries in the release..." -ForegroundColor Yellow
        
        # Move what we have
        Move-Item $sdkRoot $DestDir
    } else {
        Move-Item $sdkRoot $DestDir
    }
    
    # Write version marker
    $Version | Out-File -FilePath "$DestDir/.version" -NoNewline
    
    # Cleanup
    Remove-Item -Force $tempZip -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $tempExtract -ErrorAction SilentlyContinue
    
    Write-Host "  Streamline SDK installed to: $DestDir" -ForegroundColor Green
    return $true
}

# ============================================
# PREREQUISITE CHECKS
# ============================================

# Check for CMake - try PATH first, then known install locations
$cmakePath = "cmake"
if (-not (Test-Command "cmake")) {
    # Try common installation paths
    $knownPaths = @(
        "${env:ProgramFiles}\CMake\bin\cmake.exe",
        "${env:ProgramFiles(x86)}\CMake\bin\cmake.exe",
        "$env:LOCALAPPDATA\CMake\bin\cmake.exe"
    )
    
    $found = $false
    foreach ($path in $knownPaths) {
        if (Test-Path $path) {
            $cmakePath = $path
            $found = $true
            Write-Host "Found CMake at: $path" -ForegroundColor Green
            break
        }
    }
    
    if (-not $found) {
        Write-Host "ERROR: CMake not found!" -ForegroundColor Red
        Write-Host "Install CMake via: winget install cmake" -ForegroundColor Yellow
        Write-Host "Or download from: https://cmake.org/download/" -ForegroundColor Yellow
        exit 1
    }
}

$cmakeVersion = & $cmakePath --version | Select-Object -First 1
Write-Host "CMake: $cmakeVersion" -ForegroundColor Green

# Check for Visual Studio
$vsGenerators = @()
if (Test-Path "${env:ProgramFiles}\Microsoft Visual Studio\2022") {
    $vsGenerators += "Visual Studio 17 2022"
}
if (Test-Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019") {
    $vsGenerators += "Visual Studio 16 2019"
}

if ($vsGenerators.Count -eq 0) {
    Write-Host "ERROR: Visual Studio 2019 or 2022 not found!" -ForegroundColor Red
    Write-Host "Install Visual Studio with C++ workload" -ForegroundColor Yellow
    exit 1
}

$generator = $vsGenerators[0]
Write-Host "Using: $generator" -ForegroundColor Green
Write-Host ""

# ============================================
# DOWNLOAD STREAMLINE SDK
# ============================================

$downloadSuccess = Get-StreamlineSDK -Version $sdkVersion -DestDir $StreamlineDir -Force:$ForceDownload

if (-not $downloadSuccess) {
    Write-Host ""
    Write-Host "SDK download failed. Checking for manual installation..." -ForegroundColor Yellow
    
    if (-not (Test-Path "$StreamlineDir/include/sl.h")) {
        Write-Host "ERROR: Streamline SDK not found and download failed" -ForegroundColor Red
        exit 1
    }
}

# ============================================
# BUILD PLUGIN
# ============================================

if ($SkipBuild) {
    Write-Host "Skipping build (--SkipBuild specified)" -ForegroundColor Yellow
} else {
    # Clean if requested
    if ($Clean -and (Test-Path $BuildDir)) {
        Write-Host "Cleaning build directory..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $BuildDir
    }
    
    # Create directories
    if (-not (Test-Path $BuildDir)) {
        New-Item -ItemType Directory -Path $BuildDir | Out-Null
    }
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }
    
    # Configure CMake
    Write-Host "Configuring CMake..." -ForegroundColor Yellow
    Push-Location $BuildDir
    try {
        $streamlineInclude = "$StreamlineDir/include"
        & $cmakePath -G "$generator" -A x64 -DSTREAMLINE_SDK_PATH="$StreamlineDir" ..
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: CMake configuration failed" -ForegroundColor Red
            exit 1
        }
    } finally {
        Pop-Location
    }
    
    # Build
    Write-Host "Building $Configuration..." -ForegroundColor Yellow
    & $cmakePath --build $BuildDir --config $Configuration --parallel
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build successful!" -ForegroundColor Green
}

# ============================================
# COPY STREAMLINE DLLs
# ============================================

Write-Host ""
Write-Host "Copying Streamline DLLs to Unity..." -ForegroundColor Yellow

$streamlineBinDir = "$StreamlineDir/bin/x64"
$requiredDLLs = @(
    "sl.interposer.dll",
    "sl.common.dll",
    "sl.reflex.dll",
    "sl.pcl.dll",
    "sl.dlss.dll",
    "sl.dlss_g.dll"
)

# DLSS NGX DLLs (larger model files)
$dlssNgxDLLs = @(
    "nvngx_dlss.dll",
    "nvngx_dlssg.dll"
)

$missingDLLs = @()

foreach ($dll in $requiredDLLs) {
    $srcPath = "$streamlineBinDir/$dll"
    $dstPath = "$OutputDir/$dll"
    
    if (Test-Path $srcPath) {
        Write-Host "  Copying $dll" -ForegroundColor Gray
        Copy-Item -Force $srcPath $dstPath
    } else {
        $missingDLLs += $dll
    }
}

if ($missingDLLs.Count -gt 0) {
    Write-Host ""
    Write-Host "WARNING: Some Streamline DLLs not found in SDK!" -ForegroundColor Yellow
    Write-Host "Missing DLLs:" -ForegroundColor Yellow
    foreach ($dll in $missingDLLs) {
        Write-Host "  - $dll" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "The SDK release may not include prebuilt binaries." -ForegroundColor Yellow
    Write-Host "Try downloading the binary release from:" -ForegroundColor Cyan
    Write-Host "  https://github.com/$repository/releases" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Copy DLLs to: $OutputDir" -ForegroundColor Yellow
}

# Copy DLSS NGX DLLs (larger model files, optional but needed for DLSS to work)
Write-Host ""
Write-Host "Copying DLSS NGX model files..." -ForegroundColor Yellow

foreach ($dll in $dlssNgxDLLs) {
    $srcPath = "$streamlineBinDir/$dll"
    $dstPath = "$OutputDir/$dll"
    
    if (Test-Path $srcPath) {
        Write-Host "  Copying $dll" -ForegroundColor Gray
        Copy-Item -Force $srcPath $dstPath
    } else {
        Write-Host "  Optional: $dll not found (DLSS may not function)" -ForegroundColor Gray
    }
}

# ============================================
# SUMMARY
# ============================================

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "SDK Version: $sdkVersion"
Write-Host "SDK Location: $StreamlineDir"
Write-Host "Plugin DLL: $OutputDir/GfxPluginStreamline.dll"
Write-Host ""

if ($missingDLLs.Count -gt 0) {
    Write-Host "ACTION REQUIRED: Copy missing Streamline DLLs" -ForegroundColor Yellow
} else {
    Write-Host "All files ready! Open Unity and build for Windows." -ForegroundColor Green
}
Write-Host ""

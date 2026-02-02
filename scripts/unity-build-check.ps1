# Unity CLI Build Verification Script (PowerShell)
# Usage: .\scripts\unity-build-check.ps1
# 
# Windows-native Unity batch mode compilation check.

$ErrorActionPreference = "Stop"

$ProjectPath = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$LogFile = "$env:TEMP\unity_build_check.log"
$UnityVersion = "6000.3.6f1"  # Update this to match your Unity version
$UnityPath = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"

Write-Host "🔧 Unity Build Check" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan
Write-Host "Project: $ProjectPath"
Write-Host "Unity: $UnityPath"
Write-Host ""

# Check if Unity exists
if (-not (Test-Path $UnityPath)) {
    Write-Host "❌ Unity not found at: $UnityPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please either:"
    Write-Host "  1. Install Unity $UnityVersion via Unity Hub"
    Write-Host "  2. Update the `$UnityVersion variable in this script"
    Write-Host ""
    Write-Host "Installed versions:"
    if (Test-Path "C:\Program Files\Unity\Hub\Editor") {
        Get-ChildItem "C:\Program Files\Unity\Hub\Editor" | ForEach-Object { Write-Host "  $_" }
    } else {
        Write-Host "  (none found)"
    }
    exit 1
}

Write-Host "⏳ Running Unity batch mode compilation..." -ForegroundColor Yellow
Write-Host "   (This may take 1-3 minutes on first run, 3-5 minutes after clean)"
Write-Host ""

# Run Unity in batch mode
$process = Start-Process -FilePath $UnityPath -ArgumentList @(
    "-batchmode",
    "-projectPath", $ProjectPath,
    "-buildTarget", "WebGL",
    "-logFile", $LogFile,
    "-quit"
) -Wait -PassThru -NoNewWindow

Write-Host ""
Write-Host "====================" -ForegroundColor Cyan

# Read log file
$logContent = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue

# Check for success
if ($logContent -match "Exiting batchmode successfully") {
    Write-Host "✅ BUILD SUCCEEDED" -ForegroundColor Green
    Write-Host ""
    
    # Check for warnings in our code
    $warnings = Select-String -Path $LogFile -Pattern "Assets/Scripts.*warning CS" -ErrorAction SilentlyContinue
    if ($warnings) {
        Write-Host "⚠️  $($warnings.Count) warning(s) in Assets/Scripts:" -ForegroundColor Yellow
        $warnings | Select-Object -First 10 | ForEach-Object { Write-Host $_.Line }
        Write-Host ""
    }
    
    # Show compiled assemblies
    Write-Host "Compiled assemblies:"
    Get-ChildItem "$ProjectPath\Library\ScriptAssemblies\Assembly-CSharp*" -ErrorAction SilentlyContinue | 
        Select-Object Name, Length, LastWriteTime | Format-Table
    
    exit 0
} else {
    Write-Host "❌ BUILD FAILED" -ForegroundColor Red
    Write-Host ""
    
    # Check if errors are in our code or package cache
    $ourErrors = Select-String -Path $LogFile -Pattern "Assets/Scripts.*error CS" -ErrorAction SilentlyContinue
    $pkgErrors = Select-String -Path $LogFile -Pattern "Library/PackageCache.*error CS" -ErrorAction SilentlyContinue
    
    if ($ourErrors) {
        Write-Host "❌ Errors in YOUR code (Assets/Scripts/):" -ForegroundColor Red
        $ourErrors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
        Write-Host ""
        Write-Host "Fix these errors and try again."
    }
    
    if ($pkgErrors -and -not $ourErrors) {
        Write-Host "⚠️  Errors in Package Cache (not your code):" -ForegroundColor Yellow
        Write-Host "   This usually indicates a corrupted package cache."
        Write-Host ""
        Write-Host "   Run a clean rebuild:" -ForegroundColor Cyan
        Write-Host "   Remove-Item -Recurse -Force Library"
        Write-Host "   Remove-Item -Force Packages\packages-lock.json -ErrorAction SilentlyContinue"
        Write-Host "   .\scripts\unity-build-check.ps1"
        Write-Host ""
        $pkgErrors | Select-Object -First 5 | ForEach-Object { Write-Host $_.Line }
    }
    
    Write-Host ""
    Write-Host "Full log: $LogFile"
    exit 1
}

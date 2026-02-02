# Unity Clean Rebuild Script (PowerShell)
# Usage: .\scripts\unity-clean.ps1 [-Force]
#
# Safely cleans Unity's Library folder to force a fresh rebuild.
# Includes safety guardrails to prevent accidental deletion of system folders.

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Get project path from script location (safe - doesn't rely on pwd)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Split-Path -Parent $ScriptDir

Write-Host "🧹 Unity Clean Script" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host ""

# SAFETY CHECK 1: Verify we're in a Unity project
$ManifestPath = Join-Path $ProjectPath "Packages\manifest.json"
if (-not (Test-Path $ManifestPath)) {
    Write-Host "❌ SAFETY CHECK FAILED: Not a Unity project!" -ForegroundColor Red
    Write-Host "   Expected to find: $ManifestPath"
    Write-Host "   Aborting to prevent accidental deletion."
    exit 1
}

# SAFETY CHECK 2: Verify Library folder exists where expected
$LibraryPath = Join-Path $ProjectPath "Library"
$TempPath = Join-Path $ProjectPath "Temp"
if (-not (Test-Path $LibraryPath) -and -not (Test-Path $TempPath)) {
    Write-Host "⚠️  No Library\ or Temp\ folder found - project may already be clean." -ForegroundColor Yellow
    Write-Host "   Path: $ProjectPath"
    exit 0
}

# SAFETY CHECK 3: Ensure paths are within project
$LibraryFullPath = (Resolve-Path $LibraryPath -ErrorAction SilentlyContinue).Path
if ($LibraryFullPath -and -not $LibraryFullPath.StartsWith($ProjectPath)) {
    Write-Host "❌ SAFETY CHECK FAILED: Library path is outside project!" -ForegroundColor Red
    exit 1
}

Write-Host "Project: $ProjectPath"
Write-Host ""

# Show what will be deleted
Write-Host "📁 Folders to delete:" -ForegroundColor Yellow
if (Test-Path $LibraryPath) {
    $size = (Get-ChildItem $LibraryPath -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $sizeStr = "{0:N2} MB" -f ($size / 1MB)
    Write-Host "   - Library\ ($sizeStr)"
}
if (Test-Path $TempPath) {
    Write-Host "   - Temp\"
}
$PackagesLock = Join-Path $ProjectPath "Packages\packages-lock.json"
if (Test-Path $PackagesLock) {
    Write-Host "   - Packages\packages-lock.json"
}
Write-Host ""

# Confirm with user (unless -Force flag)
if (-not $Force) {
    $confirm = Read-Host "Continue with clean? [y/N]"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Aborted."
        exit 0
    }
}

Write-Host ""
Write-Host "🗑️  Cleaning..." -ForegroundColor Yellow

# Change to project directory for safety
Push-Location $ProjectPath

try {
    # Delete using relative paths ONLY (safety)
    if (Test-Path "Library") {
        Remove-Item -Recurse -Force "Library"
        Write-Host "   ✓ Deleted Library\" -ForegroundColor Green
    }
    if (Test-Path "Temp") {
        Remove-Item -Recurse -Force "Temp"
        Write-Host "   ✓ Deleted Temp\" -ForegroundColor Green
    }
    if (Test-Path "Packages\packages-lock.json") {
        Remove-Item -Force "Packages\packages-lock.json"
        Write-Host "   ✓ Deleted packages-lock.json" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "✅ Clean complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Run: .\scripts\unity-build-check.ps1"
Write-Host "  2. First rebuild will take 2-5 minutes (downloading packages)"

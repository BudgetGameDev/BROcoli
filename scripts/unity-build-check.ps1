# Unity batch-mode compilation verification script (PowerShell)
# Usage: .\scripts\unity-build-check.ps1
#
# Windows-native Unity package resolution, asset import, and compilation check.
# This does not produce a player build; CI performs the full WebGL build.
# Set UNITY_EDITOR_PATH to override editor discovery.

$ErrorActionPreference = "Stop"

$ProjectPath = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$LogFile = "$env:TEMP\unity_build_check.log"
$VersionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"

if (-not (Test-Path $VersionFile)) {
    Write-Host "Missing Unity version file: $VersionFile" -ForegroundColor Red
    exit 1
}

$VersionLine = Select-String -Path $VersionFile -Pattern '^m_EditorVersion: (.+)$' | Select-Object -First 1
if (-not $VersionLine) {
    Write-Host "Could not read m_EditorVersion from: $VersionFile" -ForegroundColor Red
    exit 1
}

$UnityVersion = $VersionLine.Matches[0].Groups[1].Value
$UnityPath = if ($env:UNITY_EDITOR_PATH) {
    $env:UNITY_EDITOR_PATH
} else {
    "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
}

Write-Host "Unity Compilation Check" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host "Project: $ProjectPath"
Write-Host "Version: $UnityVersion"
Write-Host "Unity: $UnityPath"
Write-Host ""

# Check if Unity exists
if (-not (Test-Path $UnityPath)) {
    Write-Host "Unity not found at: $UnityPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please either:"
    Write-Host "  1. Install Unity $UnityVersion via Unity Hub"
    Write-Host "  2. Set UNITY_EDITOR_PATH to the Unity editor executable"
    exit 1
}

Write-Host "Running Unity batch mode compilation..." -ForegroundColor Yellow
Write-Host "   (This may take 1-3 minutes on first run, 3-5 minutes after clean)"
Write-Host ""

# Run Unity in batch mode
Set-Content -Path $LogFile -Value ""
$process = Start-Process -FilePath $UnityPath -ArgumentList @(
    "-batchmode",
    "-projectPath", $ProjectPath,
    "-buildTarget", "WebGL",
    "-logFile", $LogFile,
    "-quit"
) -Wait -PassThru -NoNewWindow

Write-Host ""
Write-Host "==========================" -ForegroundColor Cyan

# Read log file
$logContent = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue

# Check for success
if ($process.ExitCode -eq 0 -and $logContent -match "Exiting batchmode successfully") {
    # Assets/csc.rsp promotes compiler warnings to errors. Keep this log scan as a
    # safeguard for first-party assemblies that may override compiler arguments.
    $warnings = Select-String -Path $LogFile -Pattern "(Assets/Editor|LocalPackages)/.*warning [A-Z]+[0-9]+" -ErrorAction SilentlyContinue
    if ($warnings) {
        Write-Host "COMPILATION FAILED ($($warnings.Count) first-party warning(s))" -ForegroundColor Red
        $warnings | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
        Write-Host ""
        Write-Host "Warnings are treated as errors by the repository CI gate."
        Write-Host "Full log: $LogFile"
        exit 1
    }

    Write-Host "COMPILATION SUCCEEDED (zero first-party warnings)" -ForegroundColor Green
    Write-Host ""

    # Show compiled assemblies
    Write-Host "Compiled assemblies:"
    Get-ChildItem "$ProjectPath\Library\ScriptAssemblies\Assembly-CSharp*" -ErrorAction SilentlyContinue |
        Select-Object Name, Length, LastWriteTime | Format-Table

    exit 0
} else {
    Write-Host "COMPILATION FAILED (Unity exit code $($process.ExitCode))" -ForegroundColor Red
    Write-Host ""

    # Check if errors are in our code or package cache
    $ourErrors = Select-String -Path $LogFile -Pattern "(Assets/Editor|LocalPackages)/.*error [A-Z]+[0-9]+" -ErrorAction SilentlyContinue
    $pkgErrors = Select-String -Path $LogFile -Pattern "Library/PackageCache.*error CS" -ErrorAction SilentlyContinue

    if ($ourErrors) {
        Write-Host "Errors in first-party code:" -ForegroundColor Red
        $ourErrors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
        Write-Host ""
        Write-Host "Fix these errors and try again."
    }

    if ($pkgErrors -and -not $ourErrors) {
        Write-Host "Errors in a resolved Unity package:" -ForegroundColor Red
        Write-Host "   Check API compatibility against Packages\packages-lock.json first."
        Write-Host ""
        Write-Host "   If the pinned package cache is demonstrably corrupt, remove Library\"
        Write-Host "   and rerun this check. Preserve Packages\packages-lock.json."
        Write-Host ""
        $pkgErrors | Select-Object -First 5 | ForEach-Object { Write-Host $_.Line }
    }

    Write-Host ""
    Write-Host "Full log: $LogFile"
    exit 1
}

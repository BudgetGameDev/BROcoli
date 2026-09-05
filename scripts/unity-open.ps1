# Open this Unity project in the automated mode required by repository tooling.
# Usage: .\scripts\unity-open.ps1 [ProjectPath] [-GraphicsApi Auto|Default|Direct3D11|Direct3D12]

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ProjectPath,
    [ValidateSet("Auto", "Default", "Direct3D11", "Direct3D12")]
    [string]$GraphicsApi = "Auto"
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectPath) {
    $ProjectPath = Split-Path -Parent $ScriptDirectory
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)

if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine("unity-open: Unity CLI is required")
    exit 2
}
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Packages\manifest.json"))) {
    [Console]::Error.WriteLine("unity-open: not a Unity project: $ProjectPath")
    exit 2
}

. (Join-Path $ScriptDirectory "unity-editor-connection.ps1")
$editorPid = Get-RunningUnityEditorPid -ProjectPath $ProjectPath
if ($editorPid) {
    if (Test-AutomatedUnityEditor -EditorPid $editorPid) {
        Write-Host "unity-open: automated Editor is already ready (PID $editorPid)"
        exit 0
    }

    [Console]::Error.WriteLine("unity-open: the project is already open without -automated (PID $editorPid)")
    [Console]::Error.WriteLine("Close it safely, then run this command again.")
    exit 2
}

. (Join-Path $ScriptDirectory "unity-editor-graphics.ps1")
$editorArguments = Get-UnityEditorArgumentString -GraphicsApi $GraphicsApi
Write-Host "unity-open: opening $ProjectPath with $editorArguments"
& unity open $ProjectPath --args $editorArguments
exit $LASTEXITCODE

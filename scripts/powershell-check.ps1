# Lint and format-check the repository's PowerShell sources with PSScriptAnalyzer.
# Usage: pwsh -NoProfile -File scripts/powershell-check.ps1 [-Fix]
#
# Without -Fix it reports every diagnostic and every file the formatter would
# rewrite, then exits non-zero. With -Fix it applies the formatter in place,
# which is what ./format.sh calls.

[CmdletBinding()]
param(
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

# Pinned the way ci.sh pins Ruff, ESLint, Prettier, and Semgrep, so the gate
# cannot start reporting differently because a machine picked up a new release.
$AnalyzerVersion = "1.25.0"

$ProjectPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSCommandPath) ".."))
Set-Location -LiteralPath $ProjectPath
$SettingsPath = Join-Path $ProjectPath "PSScriptAnalyzerSettings.psd1"

$module = Get-Module -ListAvailable PSScriptAnalyzer |
    Where-Object { $_.Version -eq [version]$AnalyzerVersion } |
    Select-Object -First 1
if (-not $module) {
    Write-Host "powershell-check: installing PSScriptAnalyzer $AnalyzerVersion"
    Install-Module PSScriptAnalyzer -RequiredVersion $AnalyzerVersion `
        -Scope CurrentUser -Force -AllowClobber -Repository PSGallery
    $module = Get-Module -ListAvailable PSScriptAnalyzer |
        Where-Object { $_.Version -eq [version]$AnalyzerVersion } |
        Select-Object -First 1
}
if (-not $module) {
    [Console]::Error.WriteLine("powershell-check: PSScriptAnalyzer $AnalyzerVersion is unavailable")
    exit 2
}
Import-Module $module -Force

# git is the source of truth for which files belong to the repository, so
# generated trees such as Library/ and build/ can never reach the gate.
$tracked = & git ls-files "*.ps1" "*.psd1"
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("powershell-check: 'git ls-files' failed")
    exit 2
}
$files = @($tracked | Where-Object { $_ } | ForEach-Object { Join-Path $ProjectPath $_ })
if ($files.Count -eq 0) {
    Write-Host "powershell-check: no PowerShell sources tracked"
    exit 0
}

$failed = $false

if ($Fix) {
    foreach ($file in $files) {
        $original = [System.IO.File]::ReadAllText($file)
        $formatted = Invoke-Formatter -ScriptDefinition $original -Settings $SettingsPath
        if ($formatted -ne $original) {
            # WriteAllText with a BOM-less encoding keeps .gitattributes happy;
            # the formatter itself only ever emits the script text.
            [System.IO.File]::WriteAllText($file, $formatted, [System.Text.UTF8Encoding]::new($false))
            Write-Host "formatted: $([System.IO.Path]::GetRelativePath($ProjectPath, $file))"
        }
    }
} else {
    foreach ($file in $files) {
        $original = [System.IO.File]::ReadAllText($file)
        $formatted = Invoke-Formatter -ScriptDefinition $original -Settings $SettingsPath
        if ($formatted -ne $original) {
            [Console]::Error.WriteLine("needs formatting: $([System.IO.Path]::GetRelativePath($ProjectPath, $file))")
            $failed = $true
        }
    }
}

# -Path takes one path at a time, so collect the diagnostics file by file.
$diagnostics = foreach ($file in $files) {
    Invoke-ScriptAnalyzer -Path $file -Settings $SettingsPath
}
if ($diagnostics) {
    $diagnostics |
        Sort-Object ScriptName, Line |
        Format-Table -AutoSize -Wrap Severity, RuleName, ScriptName, Line, Message |
        Out-Host
    $failed = $true
}

if ($failed) {
    if (-not $Fix) {
        [Console]::Error.WriteLine("powershell-check: failed; run ./format.sh to apply formatting")
    }
    exit 1
}

Write-Host "powershell-check: $($files.Count) file(s) clean"
exit 0

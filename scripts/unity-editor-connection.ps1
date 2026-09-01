# Shared discovery for a Unity Editor already attached to a project on Windows.
# Dot-source this file; it defines Get-ConnectedUnityEditorPid,
# Get-RunningUnityEditorPid, and Test-AutomatedUnityEditor.

function Get-ConnectedUnityEditorPid {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $expectedPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    $statusOutput = & unity status --project-path $expectedPath --format json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $statusOutput) {
        return $null
    }

    try {
        $document = $statusOutput | ConvertFrom-Json
    } catch {
        return $null
    }

    foreach ($instance in @($document.data.instances)) {
        if (-not $instance.project -or $instance.state -ne "ready") {
            continue
        }

        $instancePath = [System.IO.Path]::GetFullPath([string]$instance.project).TrimEnd('\', '/')
        if ($instancePath.Equals($expectedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int]$instance.pid
        }
    }

    return $null
}

function Get-RunningUnityEditorPid {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $readyPid = Get-ConnectedUnityEditorPid -ProjectPath $ProjectPath
    if ($readyPid) {
        return $readyPid
    }

    $expectedPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    $pipelineOutput = & unity pipeline list --format json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $pipelineOutput) {
        return $null
    }

    try {
        $document = $pipelineOutput | ConvertFrom-Json
    } catch {
        return $null
    }

    foreach ($instance in @($document.data.instances)) {
        if (-not $instance.isRunning -or -not $instance.projectPath) {
            continue
        }

        $instancePath = [System.IO.Path]::GetFullPath([string]$instance.projectPath).TrimEnd('\', '/')
        if ($instancePath.Equals($expectedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int]$instance.pid
        }
    }

    return $null
}

function Test-AutomatedUnityEditor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$EditorPid
    )

    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $EditorPid" -ErrorAction SilentlyContinue
    if (-not $process) {
        return $false
    }

    return [bool]($process.CommandLine -match '(?i)(^|\s)-automated(\s|$)')
}

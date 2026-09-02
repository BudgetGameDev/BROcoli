# Bring a fresh Windows machine to the tooling state this repository expects.
# Usage: .\scripts\bootstrap-windows.ps1 [-DryRun] [-AgentClient NAME]...

[CmdletBinding()]
param(
    [switch]$DryRun,
    [string[]]$AgentClient = @(),
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ProjectPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) ".."))
Set-Location -LiteralPath $ProjectPath

$UnityCliInstaller = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1"
# The WebGL gate needs webgl. Windows player support ships with the Windows
# Editor, and scripts/native-builds.ps1 only ever builds StandaloneWindows64;
# the macOS and Linux players are cross-compiled on the Mac.
$EditorModules = @("webgl")
$WingetPackages = [ordered]@{
    "node" = "OpenJS.NodeJS.LTS"
    "shellcheck" = "koalaman.shellcheck"
    "shfmt" = "mvdan.shfmt"
    # The PowerShell gate runs under pwsh 7 on every host, so results match the
    # Mac; Windows PowerShell 5.1 is not a substitute.
    "pwsh" = "Microsoft.PowerShell"
}
$Summary = New-Object System.Collections.Generic.List[string]

if ($Help) {
    @'
Usage: .\scripts\bootstrap-windows.ps1 [-DryRun] [-AgentClient NAME]...

Installs the host tools, the Unity CLI, the Editor and modules this project
builds with, and the repository's per-clone hooks and commands.

Options:
  -DryRun              Print every action without changing the machine.
  -AgentClient NAME    Also register the Unity MCP server and CLI skill with
                       this AI client (codex, cursor, vscode, ...). Claude Code
                       reads the checked-in .mcp.json and .claude/skills.
  -Help                Show this help.
'@ | Write-Host
    exit 0
}

function Write-Step { param([string]$Title) Write-Host ""; Write-Host "==> $Title" }
function Add-Note { param([string]$Text) $Summary.Add($Text) }
function Test-Tool { param([string]$Name) [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

function Invoke-Run {
    param([Parameter(Mandatory = $true)][string]$Command, [string[]]$Arguments = @())

    if ($DryRun) {
        Write-Host "would run: $Command $($Arguments -join ' ')"
        return 0
    }
    # Out-Host keeps the command's own output off this function's return value,
    # so callers branching on the exit code get a number rather than an array.
    & $Command @Arguments | Out-Host
    return $LASTEXITCODE
}

if (-not [System.Environment]::OSVersion.Platform.ToString().StartsWith("Win")) {
    [Console]::Error.WriteLine("bootstrap: this script sets up Windows; see docs/machine-setup.md for other hosts.")
    exit 2
}

Write-Step "Host tools"
if (Test-Tool "winget") {
    $installedAny = $false
    foreach ($tool in $WingetPackages.Keys) {
        if (Test-Tool $tool) {
            Write-Host "${tool}: present"
        } else {
            Invoke-Run "winget" @("install", "--id", $WingetPackages[$tool], "--exact",
                "--accept-package-agreements", "--accept-source-agreements",
                "--disable-interactivity") | Out-Null
            $installedAny = $true
        }
    }
    # winget edits the persisted PATH, which an already-open shell never re-reads,
    # so a second run in this same shell would try to install them again.
    if ($installedAny -and -not $DryRun) {
        Add-Note "Newly installed tools land on PATH only in a new shell; open one before running ./ci.sh."
    }

    $chrome = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($chrome) {
        Write-Host "google-chrome: present"
    } else {
        Invoke-Run "winget" @("install", "--id", "Google.Chrome", "--exact",
            "--accept-package-agreements", "--accept-source-agreements",
            "--disable-interactivity") | Out-Null
    }
} else {
    Add-Note "winget is missing: install App Installer from the Microsoft Store, then re-run this script."
}

# .NET and uv ship their own installers and are commonly managed outside winget,
# so report them rather than adopting whatever is already there.
$ReportedTools = [ordered]@{
    "dotnet" = "Microsoft.DotNet.SDK.8"
    "uv" = "astral-sh.uv"
}
foreach ($tool in $ReportedTools.Keys) {
    if (Test-Tool $tool) {
        Write-Host "${tool}: present"
    } else {
        Add-Note "$tool is missing: see the prerequisites in CONTRIBUTING.md, or 'winget install --id $($ReportedTools[$tool])'."
    }
}

# Windows ships a python3.exe App Execution Alias that only advertises the
# Microsoft Store, so being on PATH proves nothing: ci.sh needs one that runs.
$pythonVersion = $null
if (Test-Tool "python3") {
    # The stub reports its advice on stderr and exits non-zero. Discard that
    # stream: merging it here would raise a terminating NativeCommandError.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $probe = (& python3 --version 2>$null) -join ""
        if ($LASTEXITCODE -eq 0 -and $probe -match '^Python 3') {
            $pythonVersion = $probe
        }
    } catch {
        $pythonVersion = $null
    } finally {
        $ErrorActionPreference = $previousPreference
    }
}
if ($pythonVersion) {
    Write-Host "python3: $pythonVersion"
} else {
    Add-Note "python3 is missing (the Microsoft Store alias does not count): 'winget install --id Python.Python.3.13', then turn off the python.exe and python3.exe App execution aliases in Settings."
}

# ci.sh, format.sh and every gate wrapper are shell scripts, so the Windows host
# runs them under the bash that ships with Git for Windows.
$GitBash = @("$env:ProgramFiles\Git\bin\bash.exe", "${env:ProgramFiles(x86)}\Git\bin\bash.exe") |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $GitBash -and (Test-Tool "bash")) {
    $GitBash = (Get-Command "bash").Source
}
if ($GitBash) {
    Write-Host "bash: $GitBash"
} else {
    Add-Note "Git for Windows bash is missing: ./ci.sh and the gate wrappers cannot run without it."
}

Write-Step "Unity CLI"
if (Test-Tool "unity") {
    Write-Host "unity: $(unity --version 2>$null)"
} else {
    Write-Host "Installing the Unity CLI beta channel from $UnityCliInstaller"
    if ($DryRun) {
        Write-Host "would run: download $UnityCliInstaller to a temp file and run it (channel: beta)"
    } else {
        $env:UNITY_CLI_CHANNEL = 'beta'
        # Download the installer to a file and run that, rather than evaluating
        # the response in-process: the script stays on disk to be inspected or
        # kept after a failed run, which a string evaluated in memory never is.
        $installerPath = Join-Path ([System.IO.Path]::GetTempPath()) "unity-cli-install.ps1"
        Invoke-WebRequest -Uri $UnityCliInstaller -OutFile $installerPath -UseBasicParsing
        & $installerPath
        $env:Path = "$env:LOCALAPPDATA\Unity\bin;$env:Path"
    }
    Add-Note "The installer adds %LOCALAPPDATA%\Unity\bin to PATH; open a new shell to pick it up."
}

if (-not (Test-Tool "unity")) {
    Add-Note "unity is still not on PATH; the remaining Unity steps were skipped."
    Write-Host ""
    Write-Host "bootstrap: incomplete"
    $Summary | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Step "Unity Editor and modules"
$EditorVersion = (Select-String -Path "ProjectSettings\ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
Write-Host "Project Editor version: $EditorVersion"
& unity editors path $EditorVersion *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "${EditorVersion}: installed"
} else {
    Invoke-Run "unity" @("install", $EditorVersion, "--yes", "--accept-eula") | Out-Null
}

foreach ($module in $EditorModules) {
    $installed = $false
    $rows = & unity modules list $EditorVersion --format tsv 2>$null
    foreach ($row in @($rows)) {
        $fields = $row -split "`t"
        if ($fields.Count -ge 4 -and $fields[0] -eq $module -and $fields[3] -eq "Installed") {
            $installed = $true
            break
        }
    }

    if ($installed) {
        Write-Host "${module}: installed"
    } else {
        Invoke-Run "unity" @("install-modules", "--editor-version", $EditorVersion,
            "--module", $module, "--yes", "--accept-eula") | Out-Null
    }
}

Write-Step "Unity licensing"
& unity license status *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "A Unity license is active."
} else {
    Add-Note "No active Unity license: run 'unity auth login', then 'unity license status'."
}

Write-Step "Repository commands"
# install-git-hooks.sh only sets core.hooksPath, but it stays the one definition
# of that step, so run it through bash rather than restating it here.
if ($GitBash) {
    Invoke-Run $GitBash @("./scripts/install-git-hooks.sh") | Out-Null
} else {
    Add-Note "Skipped ./scripts/install-git-hooks.sh: it needs Git for Windows bash."
}

# Exit 3 means an unrelated unity-open already owns that name on PATH. That is
# the user's file to keep or replace, so report it instead of failing the run.
$installStatus = Invoke-Run "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", "$ProjectPath\scripts\install-unity-open.ps1")
if ($installStatus -eq 3) {
    Add-Note "A different unity-open is already on PATH: replace it with '.\scripts\install-unity-open.ps1 -Force'."
} elseif ($installStatus -ne 0) {
    exit $installStatus
}

Write-Step "Agent integration"
# Claude Code picks both of these up from the clone, so they need no install.
foreach ($checkedIn in @(".mcp.json", ".claude\skills\unity-cli\SKILL.md")) {
    if (Test-Path -LiteralPath $checkedIn) {
        Write-Host "${checkedIn}: present in the clone"
    } else {
        Add-Note "$checkedIn is missing from the clone; Claude Code loses the Unity integration."
    }
}
Write-Host "com.unity.pipeline is pinned in Packages/manifest.json, so a connected Editor exposes its commands."

# A project-scoped MCP server stays inert until this clone approves it by name,
# so the tools are absent rather than broken when the file is missing.
$localSettings = ".claude\settings.local.json"
$approved = $false
if (Test-Path -LiteralPath $localSettings) {
    $approved = (Get-Content -LiteralPath $localSettings -Raw) -match "unity-editor-mcp"
}
if ($approved) {
    Write-Host "unity-editor-mcp: enabled for this clone in $localSettings"
} else {
    Add-Note "Claude Code has not approved the project MCP server: add {""enabledMcpjsonServers"":[""unity-editor-mcp""]} to $localSettings, or approve it when prompted."
}

foreach ($client in $AgentClient) {
    Invoke-Run "unity" @("mcp", "configure", $client, "--yes", "--project-path", $ProjectPath) | Out-Null
    Invoke-Run "unity" @("skill", "install", $client, "--yes") | Out-Null
}

Write-Host ""
if ($Summary.Count -eq 0) {
    Write-Host "bootstrap: complete"
    exit 0
}
Write-Host "bootstrap: finish these by hand"
$Summary | ForEach-Object { Write-Host "  - $_" }
exit 0

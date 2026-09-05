# Build and package the native Windows player without Unix command dependencies.
# Usage: .\scripts\native-builds.ps1 -Product brocoli|launcher [-Development] [-RenderPipeline urp|hdrp]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9][a-z0-9-]*$")]
    [string]$Product,
    [switch]$Development,
    [ValidateSet("urp", "hdrp")]
    [string]$RenderPipeline = "urp"
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Split-Path -Parent $ScriptDirectory
$ProductName = if ($Product -eq "launcher") { "GameLauncher" } elseif ($Product -eq "brocoli") { "BROcoli" } else { $Product }
$NativeRoot = Join-Path $ProjectPath "build\native\$Product"
$PlayersRoot = Join-Path $NativeRoot "players"
$WindowsPlayerRoot = Join-Path $PlayersRoot "windows"
$ArtifactsRoot = Join-Path $NativeRoot "artifacts"
$BuildLog = Join-Path $PlayersRoot "unity-build.log"
$ExecutablePath = Join-Path $WindowsPlayerRoot "$ProductName.exe"
$ArchivePath = Join-Path $ArtifactsRoot "$ProductName-windows-x86_64.zip"
$VersionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"

function Write-UsageErrorAndExit {
    param([string]$Message)
    [Console]::Error.WriteLine("native-builds: $Message")
    exit 2
}

function Assert-GeneratedPath {
    param(
        [string]$Candidate,
        [string]$ExpectedParent
    )

    $fullCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $fullParent = [System.IO.Path]::GetFullPath($ExpectedParent).TrimEnd('\') + '\'
    if (-not $fullCandidate.StartsWith($fullParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside $ExpectedParent`: $fullCandidate"
    }
}

function Write-Utf8File {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"), $encoding)
}

if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
    Write-UsageErrorAndExit "Unity CLI is required"
}
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-UsageErrorAndExit "Python 3 is required"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-UsageErrorAndExit "Git is required"
}
if (-not (Test-Path -LiteralPath $VersionFile)) {
    Write-UsageErrorAndExit "could not find $VersionFile"
}

$versionMatch = Select-String -LiteralPath $VersionFile -Pattern '^m_EditorVersion: (.+)$' |
    Select-Object -First 1
if (-not $versionMatch) {
    Write-UsageErrorAndExit "could not read the Unity editor version"
}
$UnityVersion = $versionMatch.Matches[0].Groups[1].Value

Assert-GeneratedPath -Candidate $PlayersRoot -ExpectedParent $NativeRoot
Assert-GeneratedPath -Candidate $ArtifactsRoot -ExpectedParent $NativeRoot
if (Test-Path -LiteralPath $PlayersRoot) {
    Remove-Item -LiteralPath $PlayersRoot -Recurse -Force
}
if (Test-Path -LiteralPath $ArtifactsRoot) {
    Remove-Item -LiteralPath $ArtifactsRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $PlayersRoot -Force | Out-Null
New-Item -ItemType Directory -Path $ArtifactsRoot -Force | Out-Null

$releaseArguments = @(
    (Join-Path $ScriptDirectory "release-build.py"),
    "--product", $Product,
    "--targets", "windows",
    "--pipeline", $RenderPipeline,
    "--output", $PlayersRoot
)
if ($Development) { $releaseArguments += "--development" }
& python @releaseArguments
if ($LASTEXITCODE -ne 0) { throw "Isolated player build failed with exit code $LASTEXITCODE" }

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Expected player was not produced: $ExecutablePath"
}
$requiredPayload = @("${ProductName}_Data", "UnityPlayer.dll", "MonoBleedingEdge")
foreach ($name in $requiredPayload) {
    $payloadPath = Join-Path $WindowsPlayerRoot $name
    if (-not (Test-Path -LiteralPath $payloadPath)) {
        throw "Expected player payload was not produced: $payloadPath"
    }
}
$buildSummary = Select-String -LiteralPath $BuildLog -Pattern '^\[Windows HDR10 Build\] Succeeded' |
    Select-Object -Last 1
if (-not $buildSummary) {
    throw "Unity log carried no successful Windows build summary: $BuildLog"
}
if ($buildSummary.Line -notmatch '0 warning\(s\), 0 error\(s\)') {
    throw "Windows build was not clean: $($buildSummary.Line)"
}

Copy-Item -LiteralPath (Join-Path $PlayersRoot "release-audit.json") -Destination $ArtifactsRoot
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $WindowsPlayerRoot,
    $ArchivePath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

$commit = (& git -C $ProjectPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the current Git commit"
}
$shortCommit = (& git -C $ProjectPath rev-parse --short=7 HEAD).Trim()
$dirtyOutput = & git -C $ProjectPath status --porcelain
$dirty = if ($dirtyOutput) { "true" } else { "false" }
$developmentValue = if ($Development) { "true" } else { "false" }
$builtAt = [DateTime]::UtcNow
$builtAtText = $builtAt.ToString("yyyy-MM-ddTHH:mm:ssZ", [Globalization.CultureInfo]::InvariantCulture)
$buildId = $builtAt.ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture) + "-$shortCommit"
$buildInfoPath = Join-Path $ArtifactsRoot "build-info.txt"
Write-Utf8File -Path $buildInfoPath -Lines @(
    "build_id=$buildId",
    "commit=$commit",
    "unity=$UnityVersion",
    "targets=windows",
    "product=$Product",
    "render_pipeline=$($RenderPipeline.ToLowerInvariant())",
    "development=$developmentValue",
    "dirty=$dirty",
    "built_at=$builtAtText"
)

$archiveHash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$buildInfoHash = (Get-FileHash -LiteralPath $buildInfoPath -Algorithm SHA256).Hash.ToLowerInvariant()
$auditHash = (Get-FileHash -LiteralPath (Join-Path $ArtifactsRoot "release-audit.json") -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumsPath = Join-Path $ArtifactsRoot "SHA256SUMS"
Write-Utf8File -Path $checksumsPath -Lines @(
    "$archiveHash  $ProductName-windows-x86_64.zip",
    "$buildInfoHash  build-info.txt",
    "$auditHash  release-audit.json"
)

$archiveSize = (Get-Item -LiteralPath $ArchivePath).Length
Write-Host ""
Write-Host "native-builds: packaged release artifacts in $ArtifactsRoot"
Write-Host ("{0:N1} MiB  {1}" -f ($archiveSize / 1MB), $ArchivePath)

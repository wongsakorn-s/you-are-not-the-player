[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = 'artifacts/playtest',
    [string] $GodotPath = 'godot',
    [switch] $SkipTests,
    [switch] $SkipZip,
    [switch] $SkipVerify
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectDirectory = Join-Path $repoRoot 'src/Game.Client.Godot'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$stagingDirectory = Join-Path $outputRoot 'YOU ARE NOT THE PLAYER'
$artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if ($outputRoot -ne $artifactRoot -and
    -not $outputRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside '$artifactRoot'."
}

$godotCommand = Get-Command $GodotPath -ErrorAction SilentlyContinue
if ($null -eq $godotCommand) {
    if (-not (Test-Path -LiteralPath $GodotPath)) {
        throw "Godot was not found. Pass -GodotPath '<path-to-godot.exe>' or add Godot 4.7.2 .NET to PATH."
    }

    $godotExecutable = [IO.Path]::GetFullPath($GodotPath)
} else {
    $godotExecutable = $godotCommand.Source
}

# The plain Windows binary detaches from the console: it returns exit code 0
# immediately and writes its output nowhere, so a failed export looks identical
# to a successful one and the files are still being written when the script
# moves on. The console build is the same engine attached to stdout.
$consoleExecutable = Join-Path (Split-Path -Parent $godotExecutable) 'godot_console.exe'
if (Test-Path -LiteralPath $consoleExecutable) {
    $godotExecutable = $consoleExecutable
}

$presetPath = Join-Path $projectDirectory 'export_presets.cfg'
if (-not (Test-Path -LiteralPath $presetPath)) {
    throw "Missing export preset: $presetPath"
}

# Godot's export plugin refuses to build the C# side unless a solution sits
# beside the project, and it reports that refusal as a warning with exit code
# zero. The export then ships an engine with no managed assemblies in it, which
# crashes on the first frame - on the tester's machine, not here.
$clientSolution = Join-Path $projectDirectory 'Game.Client.Godot.sln'
if (-not (Test-Path -LiteralPath $clientSolution)) {
    throw "Missing $clientSolution. Godot needs a solution in the project directory or it exports no C#."
}

$templateDirectory = Join-Path $env:APPDATA 'Godot/export_templates/4.7.2.stable.mono'
if (-not (Test-Path -LiteralPath $templateDirectory)) {
    throw "Godot export templates 4.7.2 .NET are missing. Install them from Godot Editor > Editor > Manage Export Templates, then rerun this script."
}

function Invoke-Checked {
    param(
        [string] $FilePath,
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

if (-not $SkipTests) {
    $solutionPath = Join-Path $repoRoot 'Game.sln'
    Invoke-Checked 'dotnet' @('build', $solutionPath, '--configuration', $Configuration)
    Invoke-Checked 'dotnet' @('test', $solutionPath, '--configuration', $Configuration, '--no-build')
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

$executablePath = Join-Path $stagingDirectory 'YOU ARE NOT THE PLAYER.exe'
Invoke-Checked $godotExecutable @(
    '--headless',
    '--path', $projectDirectory,
    '--export-release',
    'Windows Desktop',
    $executablePath
)

# Godot exits zero on an export that produced nothing usable, so the exit code
# is not evidence. These three are.
foreach ($required in @(
        $executablePath,
        (Join-Path $stagingDirectory 'YOU ARE NOT THE PLAYER.pck'),
        (Join-Path $stagingDirectory 'data_Game.Client.Godot_windows_x86_64'))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Export did not produce '$required'. Run the same command with godot_console.exe and read the output."
    }
}

if (-not $SkipVerify) {
    # Play a night in the thing that is about to be handed to somebody. It loads
    # the content files, runs a full shift and writes a report, so a package that
    # cannot start cannot pass. The exported binary writes no console output, so
    # the report file is the signal.
    $probeReport = Join-Path $outputRoot 'export-verification.md'
    if (Test-Path -LiteralPath $probeReport) {
        Remove-Item -LiteralPath $probeReport -Force
    }

    # Start-Process joins ArgumentList on spaces without quoting anything, and
    # this repository lives in a path with spaces in it.
    $probe = Start-Process -FilePath $executablePath -Wait -PassThru -NoNewWindow -ArgumentList @(
        '--headless', '--', '--night-report', "`"$probeReport`"", '--night-seed', '3')
    if ($probe.ExitCode -ne 0) {
        throw "The exported build exited with $($probe.ExitCode) instead of playing a night."
    }

    if (-not (Test-Path -LiteralPath $probeReport)) {
        throw "The exported build started but never finished a night. Package not written."
    }

    Write-Host "Verified: the exported build played a full night."
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'playtest/README.md') -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'playtest/feedback-template.md') -Destination $stagingDirectory

$commit = (& git -C $repoRoot rev-parse --short HEAD).Trim()
$buildInfo = @(
    'YOU ARE NOT THE PLAYER — First Fun Playtest',
    "Commit: $commit",
    "Configuration: $Configuration",
    "Built (UTC): $([DateTime]::UtcNow.ToString('u'))",
    'Scenario seed: 481516',
    '',
    'This build is for usability and wording feedback. Do not redistribute.'
)
$buildInfo | Set-Content -LiteralPath (Join-Path $stagingDirectory 'build-info.txt') -Encoding utf8

if (-not $SkipZip) {
    $zipPath = Join-Path $outputRoot "you-are-not-the-player-playtest-$commit.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $stagingDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

    # Compress-Archive skips files it cannot read and says nothing about it.
    $zipLength = (Get-Item -LiteralPath $zipPath).Length
    if ($zipLength -lt 10MB) {
        throw "The package is only $([Math]::Round($zipLength / 1MB, 2)) MB, which is too small to contain the game."
    }

    Write-Host "Playtest package: $zipPath ($([Math]::Round($zipLength / 1MB, 1)) MB)"
}

Write-Host "Exported executable: $executablePath"

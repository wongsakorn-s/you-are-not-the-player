[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = 'artifacts/playtest',
    [string] $GodotPath = 'godot',
    [switch] $SkipTests,
    [switch] $SkipZip
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

$presetPath = Join-Path $projectDirectory 'export_presets.cfg'
if (-not (Test-Path -LiteralPath $presetPath)) {
    throw "Missing export preset: $presetPath"
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
    Write-Host "Playtest package: $zipPath"
}

Write-Host "Exported executable: $executablePath"

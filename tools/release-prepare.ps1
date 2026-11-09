#Requires -Version 7.0
<#
.SYNOPSIS
  Prepares a release: sets the new version in Directory.Build.props, turns the "Unreleased"
  section of CHANGELOG.md into the section of the new version, and writes the release notes.

.DESCRIPTION
  The new version is -Version if given. Otherwise, if the version in Directory.Build.props has no
  tag yet (the first release), that version is released; else -Bump raises it.
  Fails if "Unreleased" is empty or the tag exists already. In GitHub Actions it writes
  version=<x.y.z> to $env:GITHUB_OUTPUT. Nothing is committed; the release workflow does that.
#>
param(
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump = 'patch',
    [string]$Version = '',
    [string]$NotesFile = '',
    [string]$Date = [DateTime]::UtcNow.ToString('yyyy-MM-dd'),
    [string]$Root = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Root) { $Root = Join-Path $PSScriptRoot '..' }
# Relative paths are meant from the current PowerShell location, not .NET's current directory.
$Root = [System.IO.Path]::GetFullPath($Root, (Get-Location).Path)
if (-not $NotesFile) { $NotesFile = Join-Path $Root 'dist\release-notes.md' }
$NotesFile = [System.IO.Path]::GetFullPath($NotesFile, (Get-Location).Path)
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Read-Normalized([string]$Path) {
    $raw = [System.IO.File]::ReadAllText($Path)
    $newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    return @{ Text = $raw.Replace("`r`n", "`n"); Newline = $newline }
}

function Write-WithNewline([string]$Path, [string]$Text, [string]$Newline) {
    [System.IO.File]::WriteAllText($Path, $Text.Replace("`n", $Newline), $utf8)
}

# Current and new version.
$propsPath = Join-Path $Root 'Directory.Build.props'
$props = Read-Normalized $propsPath
$versionMatch = [regex]::Match($props.Text, '<ModVersion>\s*(\d+\.\d+\.\d+)\s*</ModVersion>')
if (-not $versionMatch.Success) { throw "No <ModVersion>x.y.z</ModVersion> in $propsPath" }
$current = [version]$versionMatch.Groups[1].Value

$tags = @(git -C $Root tag --list 'v*' 2>$null)
if ($Version) {
    $Version = $Version.Trim().TrimStart('v')
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "A version looks like 1.2.3, not '$Version'" }
    $new = [version]$Version
}
elseif ($tags -notcontains "v$($current.ToString(3))") {
    $new = $current
}
else {
    $new = switch ($Bump) {
        'major' { [version]::new($current.Major + 1, 0, 0) }
        'minor' { [version]::new($current.Major, $current.Minor + 1, 0) }
        default { [version]::new($current.Major, $current.Minor, $current.Build + 1) }
    }
}

$newText = $new.ToString(3)
if ($tags -contains "v$newText") { throw "Version $newText was released already (tag v$newText exists)" }
if ($new -lt $current) { throw "Version $newText is lower than the current version $current" }

# CHANGELOG.md: "Unreleased" becomes the new version; a new, empty "Unreleased" stays on top.
$changelogPath = Join-Path $Root 'CHANGELOG.md'
$changelog = Read-Normalized $changelogPath
$text = $changelog.Text
$heading = [regex]::Match($text, '(?m)^## \[Unreleased\][^\n]*\n')
if (-not $heading.Success) { throw "CHANGELOG.md has no '## [Unreleased]' heading" }
$start = $heading.Index + $heading.Length
$next = [regex]::Match($text.Substring($start), '(?m)^## ')
$end = if ($next.Success) { $start + $next.Index } else { $text.Length }
$notes = $text.Substring($start, $end - $start).Trim()
if (-not $notes) { throw "Nothing under '## [Unreleased]' in CHANGELOG.md. Add the changes there first." }

$text = $text.Substring(0, $heading.Index) + "## [Unreleased]`n`n## [$newText] - $Date`n`n$notes`n`n" + $text.Substring($end)
Write-WithNewline $changelogPath ($text.TrimEnd() + "`n") $changelog.Newline

$propsText = $props.Text.Substring(0, $versionMatch.Index) + "<ModVersion>$newText</ModVersion>" +
    $props.Text.Substring($versionMatch.Index + $versionMatch.Length)
Write-WithNewline $propsPath $propsText $props.Newline

$download = @"
## Download

- **WingspanAccessInstaller.exe** installs or updates Wingspan Access and BepInEx and finds the game
  by itself. Windows may warn because the file is not signed: choose "More info", then "Run anyway".
- **WingspanAccess-$newText.zip** is for a manual installation: extract it into the Wingspan folder,
  the one with Wingspan.exe.
"@
New-Item -ItemType Directory -Force -Path (Split-Path $NotesFile) | Out-Null
Write-WithNewline $NotesFile ("$notes`n`n" + $download.Replace("`r`n", "`n") + "`n") "`n"

if ($env:GITHUB_OUTPUT) {
    "version=$newText" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
Write-Host "Prepared release $newText (version was $current). Notes: $NotesFile"

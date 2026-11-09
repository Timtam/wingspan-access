#Requires -Version 7.0
<#
.SYNOPSIS
  Packs a release into dist\: WingspanAccess-<version>.zip, WingspanAccessInstaller.exe and
  SHA256SUMS.txt.

.DESCRIPTION
  The ZIP holds BepInEx (unchanged, without its changelog), a minimal BepInEx.cfg and the plugin
  folder BepInEx\plugins\WingspanAccess with the mod, prism.dll, the installer, a README and the
  licenses. Extracted into the game folder, it is a complete installation; the installer uses the
  same ZIP. Run tools\build-release.ps1 first (or build the mod and the installer yourself).
  PowerShell 7 is required so that the ZIP uses forward slashes in its paths.
#>
param(
    [string]$Configuration = 'Release',
    [string]$RepositorySlug = '',
    [string]$PrismDll = '',
    [string]$Output = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Output) { $Output = Join-Path $root 'dist' }
if (-not $PrismDll) { $PrismDll = Join-Path $root 'third_party\prism\x86\prism.dll' }
# Relative paths are meant from the current PowerShell location, not .NET's current directory.
$Output = [System.IO.Path]::GetFullPath($Output, (Get-Location).Path)
$PrismDll = [System.IO.Path]::GetFullPath($PrismDll, (Get-Location).Path)

$propsText = [System.IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
$versionMatch = [regex]::Match($propsText, '<ModVersion>\s*([^<\s]+)\s*</ModVersion>')
if (-not $versionMatch.Success) { throw 'No <ModVersion> in Directory.Build.props' }
$version = $versionMatch.Groups[1].Value

$bepinex = Join-Path $root 'third_party\bepinex'
$plugin = Join-Path $root "src\WingspanAccess\bin\$Configuration\net6.0\WingspanAccess.dll"
$installer = Join-Path $root "installer\WingspanAccessInstaller\bin\$Configuration\net48\WingspanAccessInstaller.exe"
foreach ($required in @((Join-Path $bepinex 'BepInEx\core\BepInEx.Core.dll'), $plugin, $installer, $PrismDll)) {
    if (-not (Test-Path $required)) { throw "Missing $required. Run tools\build-release.ps1 first." }
}

foreach ($built in @($plugin, $installer)) {
    $builtVersion = (Get-Item $built).VersionInfo.ProductVersion
    if ($builtVersion -ne $version) {
        throw "$(Split-Path $built -Leaf) has version $builtVersion, Directory.Build.props says $version. Build again."
    }
}

# The game is a 32-bit process; a 64-bit prism.dll would not load.
$peBytes = [System.IO.File]::ReadAllBytes($PrismDll)
$peOffset = [BitConverter]::ToInt32($peBytes, 0x3C)
if ([BitConverter]::ToUInt16($peBytes, $peOffset + 4) -ne 0x14C) { throw "$PrismDll is not a 32-bit (x86) DLL" }

# License texts, downloaded once from the pinned sources.
$licenses = Join-Path $root 'third_party\licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
$licenseSources = [ordered]@{
    'LICENSE-BepInEx.txt' = @(
        'https://raw.githubusercontent.com/BepInEx/BepInEx/dbc82f05b8ea34833de5b6a723eb60dd8398e0b1/LICENSE',
        'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE')
    'LICENSE-Prism.txt'   = @(
        'https://raw.githubusercontent.com/ethindp/prism/v0.18.3/LICENSE',
        'https://raw.githubusercontent.com/ethindp/prism/master/LICENSE')
}
foreach ($name in $licenseSources.Keys) {
    $target = Join-Path $licenses $name
    foreach ($url in $licenseSources[$name]) {
        if (Test-Path $target) { break }
        try {
            Write-Host "Downloading $url"
            Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
        }
        catch {
            Write-Warning "$url failed: $($_.Exception.Message)"
        }
    }
    if (-not (Test-Path $target)) { throw "Could not download $name" }
}

$stage = Join-Path $Output 'package'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Get-ChildItem $Output -Filter 'WingspanAccess-*.zip' -ErrorAction SilentlyContinue | Remove-Item -Force

# BepInEx as published, without its changelog.txt.
foreach ($file in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') {
    $source = Join-Path $bepinex $file
    if (Test-Path $source) { Copy-Item -LiteralPath $source -Destination $stage }
}
Copy-Item -LiteralPath (Join-Path $bepinex 'dotnet') -Destination (Join-Path $stage 'dotnet') -Recurse
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'BepInEx\config') | Out-Null
Copy-Item -LiteralPath (Join-Path $bepinex 'BepInEx\core') -Destination (Join-Path $stage 'BepInEx\core') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'packaging\BepInEx.cfg') -Destination (Join-Path $stage 'BepInEx\config\BepInEx.cfg')

$pluginDir = Join-Path $stage 'BepInEx\plugins\WingspanAccess'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $plugin, $PrismDll, $installer -Destination $pluginDir
Copy-Item -Path (Join-Path $licenses '*') -Destination $pluginDir

$projectUrl = if ($RepositorySlug) { "https://github.com/$RepositorySlug" } else { 'see the README of the source code' }
foreach ($template in 'README.txt', 'THIRD-PARTY-NOTICES.txt') {
    $text = [System.IO.File]::ReadAllText((Join-Path $root "packaging\$template"))
    $text = $text.Replace('{VERSION}', $version).Replace('{PROJECT_URL}', $projectUrl)
    $text = $text -replace "`r?`n", "`r`n"
    [System.IO.File]::WriteAllText((Join-Path $pluginDir $template), $text, [System.Text.UTF8Encoding]::new($false))
}

$zip = Join-Path $Output "WingspanAccess-$version.zip"
Add-Type -AssemblyName System.IO.Compression.ZipFile
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -Recurse -Force $stage

$installerOut = Join-Path $Output 'WingspanAccessInstaller.exe'
Copy-Item -LiteralPath $installer -Destination $installerOut -Force

$sums = foreach ($file in $zip, $installerOut) {
    '{0}  {1}' -f (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
[System.IO.File]::WriteAllText((Join-Path $Output 'SHA256SUMS.txt'), ($sums -join "`n") + "`n")

foreach ($file in $zip, $installerOut) {
    Write-Host ('{0} ({1:N0} KB)' -f $file, ((Get-Item $file).Length / 1KB))
}

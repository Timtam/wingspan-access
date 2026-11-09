<#
.SYNOPSIS
  Downloads the BepInEx build the mod is made for and extracts it to third_party\bepinex.

.DESCRIPTION
  The mod needs BepInEx 6 (IL2CPP, 32-bit) build 793. Building without the game references its
  core DLLs from here, and tools\package.ps1 puts the same files into the release ZIP.
  The download is checked against a pinned SHA-256 hash.
#>
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\third_party\bepinex'),
    [string]$Url = 'https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.793%2Bdbc82f0.zip',
    [string]$Sha256 = '6eb35cec828f16836674231b2ab4ab1db6c9953b7a11b34b8c8badba14218ed2'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Destination = [System.IO.Path]::GetFullPath($Destination)
$zip = Join-Path $Destination 'bepinex.zip'
$marker = Join-Path $Destination 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

if (-not (Test-Path $zip) -or (Get-FileHash $zip -Algorithm SHA256).Hash -ne $Sha256.ToUpperInvariant()) {
    Write-Host "Downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $zip -UseBasicParsing
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($hash -ne $Sha256.ToUpperInvariant()) {
        Remove-Item $zip -Force
        throw "BepInEx download has SHA-256 $hash, expected $Sha256"
    }
}

if (-not (Test-Path $marker)) {
    Write-Host "Extracting to $Destination"
    Expand-Archive -Path $zip -DestinationPath $Destination -Force
}

Write-Host "BepInEx ready in $Destination"

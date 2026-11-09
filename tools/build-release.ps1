#Requires -Version 7.0
<#
.SYNOPSIS
  Builds a release the way the CI does: downloads BepInEx, builds Prism (x86) if needed, builds
  the mod against the reference assemblies in lib\interop and the installer, and packs everything
  into dist\.

.PARAMETER RepositorySlug
  The GitHub repository ("owner/name") the mod and the installer check for updates. The CI passes
  its own repository; without it, the builds have no update source.

.PARAMETER PrismDll
  A prism.dll (x86) to use instead of building third_party\prism\x86\prism.dll.
#>
param(
    [string]$RepositorySlug = $env:GITHUB_REPOSITORY,
    [string]$Configuration = 'Release',
    [string]$PrismDll = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $RepositorySlug) { $RepositorySlug = '' }
if ($PrismDll) { $PrismDll = [System.IO.Path]::GetFullPath($PrismDll, (Get-Location).Path) }

& (Join-Path $PSScriptRoot 'fetch-bepinex.ps1')

if (-not $PrismDll) {
    $PrismDll = Join-Path $root 'third_party\prism\x86\prism.dll'
    if (-not (Test-Path $PrismDll)) {
        & (Join-Path $PSScriptRoot 'build-prism-x86.cmd')
        if ($LASTEXITCODE -ne 0) { throw 'Building Prism failed' }
    }
}

$common = @('-c', $Configuration, '-nologo', "-p:RepositorySlug=$RepositorySlug")

dotnet build (Join-Path $root 'src\WingspanAccess\WingspanAccess.csproj') @common -p:UseGameReferences=false
if ($LASTEXITCODE -ne 0) { throw 'Building the mod failed' }

dotnet build (Join-Path $root 'installer\WingspanAccessInstaller\WingspanAccessInstaller.csproj') @common
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed' }

& (Join-Path $PSScriptRoot 'package.ps1') -Configuration $Configuration -RepositorySlug $RepositorySlug -PrismDll $PrismDll

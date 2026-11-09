<#
.SYNOPSIS
  Regenerates lib\interop: reference assemblies of the game's interop DLLs the mod compiles against.

.DESCRIPTION
  BepInEx generates interop assemblies for the game on its first start (BepInEx\interop). CI has no
  game, so the repository keeps reference assemblies of the few the mod uses: type and member
  signatures only, method bodies removed (JetBrains Refasmer). Run this after a game update that
  changes the interop assemblies, or after the mod starts using another one.
  The DLLs are copied first, because the running game keeps them open.
#>
param(
    [string]$WingspanDir = 'C:\Program Files (x86)\Steam\steamapps\common\Wingspan',
    [string]$Output = (Join-Path $PSScriptRoot '..\lib\interop'),
    [string[]]$Assemblies = @(
        'ACTk.Runtime', 'I2Loc', 'Il2Cppmscorlib', 'Rewired_Core', 'ScriptAssembly', 'Unity.TextMeshPro',
        'UnityEngine.CoreModule', 'UnityEngine.InputLegacyModule', 'UnityEngine.UI', 'UnityEngine.UIModule'
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$interop = Join-Path $WingspanDir 'BepInEx\interop'
if (-not (Test-Path (Join-Path $interop 'ScriptAssembly.dll'))) {
    throw "No interop assemblies in $interop. Install BepInEx and start the game once."
}

$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Output = [System.IO.Path]::GetFullPath($Output)
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("wingspan-refs-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work, $Output | Out-Null

Push-Location $repo
try {
    dotnet tool restore | Out-Null
    foreach ($name in $Assemblies) {
        $source = Join-Path $interop "$name.dll"
        $copy = Join-Path $work "$name.dll"
        Copy-Item $source $copy
        # Omitting non-API members keeps the files small; a few assemblies need the full member set.
        dotnet tool run refasmer -- -q --omit-non-api-members=true -O $Output $copy
        if ($LASTEXITCODE -ne 0) {
            dotnet tool run refasmer -- -q --omit-non-api-members=false -O $Output $copy
            if ($LASTEXITCODE -ne 0) { throw "Refasmer failed for $name" }
        }
        Write-Host "  $name"
    }
}
finally {
    Pop-Location
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Reference assemblies written to $Output"

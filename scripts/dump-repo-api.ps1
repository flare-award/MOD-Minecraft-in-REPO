# Dumps the R.E.P.O. API surface (Assembly-CSharp.dll) into docs/REPO-NOTES.md.
#
# This is the "decompile first" step of the host/guest redesign (PeakCraft-style
# architecture): the plugin patches the game's own input sampler, movement step,
# camera update, hazard funnel and death, and those can only be found by reading
# the real assembly. The dump lists type names, member names and arities only -
# no game code is decompiled or redistributed.
#
# Usage (PowerShell, Windows):
#   .\scripts\dump-repo-api.ps1
#   .\scripts\dump-repo-api.ps1 -RepoGameDir "E:\Program files\Steam\steamapps\common\REPO"
#   .\scripts\dump-repo-api.ps1 -Keywords "PlayerController;PlayerAvatar;Input;Camera;PhysGrab"
#
# Requires: .NET SDK 8 or newer (the tool targets net8.0; the SDK downloads the
# net8.0 reference pack from nuget.org on first build).
#
# Send the produced docs/REPO-NOTES.md to the agent - it is the input for the
# patch-point table in docs/PLAN-HOST-GUEST-RU.md.

param(
    [string]$RepoGameDir = $env:REPO_GAME_DIR,
    [string]$Out,
    [string]$Keywords
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tool = Join-Path $root "tools/ApiDump/ApiDump.csproj"
if (-not $Out) { $Out = Join-Path $root "docs/REPO-NOTES.md" }

# 1. Locate the game's Managed folder.
if (-not $RepoGameDir) {
    $candidates = @(
        "E:\Program files\Steam\steamapps\common\REPO",
        "C:\Program Files (x86)\Steam\steamapps\common\REPO",
        "D:\Steam\steamapps\common\REPO",
        "C:\Steam\steamapps\common\REPO"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "REPO_Data/Managed/Assembly-CSharp.dll")) {
            $RepoGameDir = $candidate
            break
        }
    }
}

if (-not $RepoGameDir) {
    $RepoGameDir = Read-Host "Where is R.E.P.O. installed? (folder containing REPO_Data)"
}

$managed = Join-Path $RepoGameDir "REPO_Data/Managed"
$assembly = Join-Path $managed "Assembly-CSharp.dll"
if (-not (Test-Path $assembly)) {
    throw "Assembly-CSharp.dll not found under '$managed'. Set -RepoGameDir to the folder that contains REPO_Data."
}

Write-Host "Game     : $RepoGameDir" -ForegroundColor Cyan
Write-Host "Assembly : $assembly" -ForegroundColor Cyan
Write-Host "Output   : $Out" -ForegroundColor Cyan

# 2. Build and run the dumper.
try {
    dotnet build $tool -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
} catch {
    Write-Host ""
    Write-Host "Build failed. The tool targets net8.0 and needs the .NET 8 reference pack" -ForegroundColor Yellow
    Write-Host "from nuget.org. If your network blocks nuget, install a .NET SDK that" -ForegroundColor Yellow
    Write-Host "matches your installed runtime, or run:" -ForegroundColor Yellow
    Write-Host "  dotnet add `"$tool`" package System.Reflection.Metadata" -ForegroundColor Yellow
    throw
}

if ($Keywords) {
    dotnet run --project $tool -c Release --no-build -- "$managed" "$Out" "Assembly-CSharp.dll" "$Keywords"
} else {
    dotnet run --project $tool -c Release --no-build -- "$managed" "$Out" "Assembly-CSharp.dll"
}
if ($LASTEXITCODE -ne 0) { throw "ApiDump failed ($LASTEXITCODE)" }

Write-Host ""
Write-Host "Done. Send '$Out' to the agent (or attach it in chat)." -ForegroundColor Green

# Builds the R.E.P.O. side of the mod (MinecraftInRepo.dll, BepInEx plugin).
#
# Usage (PowerShell, Windows):
#   .\scripts\build-repo-mod.ps1                                  # offline stub references
#   .\scripts\build-repo-mod.ps1 -RepoGameDir "C:\...\REPO"       # against your real install (recommended)
#   .\scripts\build-repo-mod.ps1 -RepoGameDir "C:\...\REPO" -Install   # ...and copy into BepInEx\plugins
#   .\scripts\build-repo-mod.ps1 -UseGameLibs                      # community NuGet game assemblies (needs internet)
#
# Requires: .NET SDK 8 or newer (https://dotnet.microsoft.com/download), i.e. `dotnet` in PATH.

param(
    [string]$RepoGameDir = $env:REPO_GAME_DIR,
    [switch]$UseGameLibs,
    [switch]$Install,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$repoMod = Join-Path $root "repo-mod"
$stubs = Join-Path $repoMod "Stubs\StubAssemblies.csproj"
$plugin = Join-Path $repoMod "MinecraftInRepo.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet was not found in PATH. Install the .NET SDK (8.0+): https://dotnet.microsoft.com/download"
}

if ($UseGameLibs) {
    Write-Host "== Building plugin against R.E.P.O.GameLibs.Steam (NuGet) ==" -ForegroundColor Cyan
    & dotnet build $plugin -c $Configuration -p:UseGameLibsNuGet=true --nologo
}
elseif ($RepoGameDir) {
    if (-not (Test-Path (Join-Path $RepoGameDir "REPO_Data\Managed\Assembly-CSharp.dll"))) {
        Write-Error "No REPO_Data\Managed\Assembly-CSharp.dll under '$RepoGameDir'. Point -RepoGameDir at the R.E.P.O. install folder."
    }
    Write-Host "== Building plugin against the game assemblies in '$RepoGameDir' ==" -ForegroundColor Cyan
    & dotnet build $plugin -c $Configuration -p:RepoGameDir="$RepoGameDir" --nologo
}
else {
    Write-Host "== No game dir given: building offline reference stubs first ==" -ForegroundColor Cyan
    & dotnet build $stubs -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "== Building plugin against the stubs ==" -ForegroundColor Cyan
    & dotnet build $plugin -c $Configuration --nologo
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $repoMod "bin\$Configuration\MinecraftInRepo.dll"
if (-not (Test-Path $dll)) {
    Write-Error "Build finished but '$dll' is missing."
}
Write-Host "`nBuilt: $dll" -ForegroundColor Green

if ($Install) {
    if (-not $RepoGameDir) {
        Write-Error "-Install needs -RepoGameDir (or the REPO_GAME_DIR environment variable)."
    }
    $plugins = Join-Path $RepoGameDir "BepInEx\plugins"
    if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
    Copy-Item $dll (Join-Path $plugins "MinecraftInRepo.dll") -Force
    Write-Host "Installed to: $(Join-Path $plugins 'MinecraftInRepo.dll')" -ForegroundColor Green
}

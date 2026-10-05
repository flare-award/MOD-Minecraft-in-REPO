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
    [string]$BepInExDir = $env:REPO_BEPINEX_DIR,
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

# A stub Assembly-CSharp.dll left over from an earlier `build-repo-mod.ps1` run
# without -RepoGameDir can shadow the real game assemblies (duplicate-type
# errors like CS0433 for MonoBehaviour/Vector3). Always clear it for real builds.
function Remove-StaleStubs {
    $stubDir = Join-Path $repoMod "libs"
    if (Test-Path $stubDir) {
        Write-Host "Removing stale stub references in '$stubDir' (they shadow the real game assemblies)" -ForegroundColor Yellow
        Remove-Item $stubDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($stale in @("obj", "bin")) {
        $p = Join-Path $repoMod $stale
        if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# r2modman keeps BepInEx inside the profile, not in the game folder - find it,
# otherwise the build silently falls back to a wrong reference set.
if ($RepoGameDir -and -not $BepInExDir) {
    $candidate = Join-Path $RepoGameDir "BepInEx\core\BepInEx.dll"
    if (Test-Path $candidate) {
        $BepInExDir = Join-Path $RepoGameDir "BepInEx"
    }
    else {
        $profiles = Join-Path $env:APPDATA "r2modmanPlus-local\REPO\profiles"
        if (Test-Path $profiles) {
            Get-ChildItem $profiles -Directory | ForEach-Object {
                $p = Join-Path $_.FullName "BepInEx\core\BepInEx.dll"
                if (-not $BepInExDir -and (Test-Path $p)) {
                    $BepInExDir = Join-Path $_.FullName "BepInEx"
                }
            }
        }
        if (-not $BepInExDir) {
            Write-Warning "Could not find BepInEx\core\BepInEx.dll (neither in the game folder nor in an r2modman profile). Pass -BepInExDir explicitly."
        }
    }
}
if ($BepInExDir) { Write-Host "BepInEx references: $BepInExDir" -ForegroundColor Cyan }

if ($UseGameLibs) {
    Write-Host "== Building plugin against R.E.P.O.GameLibs.Steam (NuGet) ==" -ForegroundColor Cyan
    Remove-StaleStubs
    & dotnet build $plugin -c $Configuration -p:UseGameLibsNuGet=true --nologo
}
elseif ($RepoGameDir) {
    if (-not (Test-Path (Join-Path $RepoGameDir "REPO_Data\Managed\Assembly-CSharp.dll"))) {
        Write-Error "No REPO_Data\Managed\Assembly-CSharp.dll under '$RepoGameDir'. Point -RepoGameDir at the R.E.P.O. install folder."
    }
    Write-Host "== Building plugin against the game assemblies in '$RepoGameDir' ==" -ForegroundColor Cyan
    Remove-StaleStubs
    $buildArgs = @($plugin, "-c", $Configuration, "-p:RepoGameDir=$RepoGameDir")
    if ($BepInExDir) { $buildArgs += "-p:BepInExDir=$BepInExDir" }
    $buildArgs += "--nologo"
    & dotnet build @buildArgs
}
else {
    Write-Host "== No game dir given: building offline reference stubs first (NOT for in-game use) ==" -ForegroundColor Cyan
    & dotnet build $stubs -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Warning "stub mode: the resulting DLL only compiles-checks the code - it CANNOT be loaded by the real BepInEx. Pass -RepoGameDir (or -UseGameLibs) to build a usable plugin."
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

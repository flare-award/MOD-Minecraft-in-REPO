# Launches R.E.P.O. (modded) and the Minecraft launcher side by side.
# Edit paths.json first. Minecraft itself must be started from a Fabric
# profile/installation that contains the mcrepo mod - the official launcher
# does not expose a reliable CLI for that, so the script opens the launcher
# and lets you hit Play on your Fabric profile.

$ErrorActionPreference = "Stop"
$pathsFile = Join-Path $PSScriptRoot "paths.json"
if (-not (Test-Path $pathsFile)) {
    Write-Error "paths.json not found next to this script."
}
$paths = Get-Content $pathsFile -Raw | ConvertFrom-Json

$repoExe = Join-Path $paths.repoPath "REPO.exe"
if (-not (Test-Path $repoExe)) {
    Write-Error "R.E.P.O. not found at '$repoExe'. Edit paths.json."
}
if (-not (Test-Path (Join-Path $paths.repoPath "BepInEx"))) {
    Write-Warning "No BepInEx folder found in the R.E.P.O. directory - the plugin will not load until you install it."
}
if (-not (Test-Path (Join-Path $paths.repoPath "BepInEx\plugins\MinecraftInRepo.dll"))) {
    Write-Warning "MinecraftInRepo.dll is not in BepInEx\plugins yet."
}
if (-not (Test-Path $paths.minecraftLauncher)) {
    Write-Warning "Minecraft launcher not found at '$($paths.minecraftLauncher)' - start Minecraft manually with your Fabric 1.21.1 profile."
}

Write-Host "Starting the Minecraft launcher (select your Fabric 'MinecraftInRepo' profile and hit Play)..."
Start-Process $paths.minecraftLauncher

Write-Host "Starting R.E.P.O. in 3 seconds (Ctrl+C to abort)..."
Start-Sleep -Seconds 3
Start-Process $repoExe -WorkingDirectory $paths.repoPath

Write-Host ""
Write-Host "Once both games are running:"
Write-Host "  - the overlay appears automatically (F8 cycles FullScreen / PiP / Off)"
Write-Host "  - press F7 in a level to calibrate the world alignment"
Write-Host "  - F6 toggles camera follow, F9 toggles TNT damage"

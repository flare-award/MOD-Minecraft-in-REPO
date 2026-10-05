# Downloads the prebuilt mods produced by GitHub Actions instead of building
# them locally (no .NET SDK / JDK / Minecraft decompilation needed).
#
#   .\scripts\fetch-builds.ps1                                   # latest successful build -> .\dist
#   .\scripts\fetch-builds.ps1 -OutDir C:\Mods
#   .\scripts\fetch-builds.ps1 -RepoGameDir "C:\...\REPO" -MinecraftDir "$env:APPDATA\.minecraft" -Install
#
# Requires: GitHub CLI (https://cli.github.com/) once authenticated: `gh auth login`.

param(
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "dist"),
    [string]$RepoGameDir = "",
    [string]$MinecraftDir = "",
    [switch]$Install
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "gh (GitHub CLI) was not found in PATH. Install it from https://cli.github.com/ and run 'gh auth login'."
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
Write-Host "Looking for the latest successful 'build' run..." -ForegroundColor Cyan

$runId = & gh run list --workflow build.yml --status success --limit 1 --json databaseId --jq ".[0].databaseId"
if (-not $runId) {
    Write-Error "No successful 'build' run found. Trigger one: gh workflow run build.yml"
}
Write-Host "Using run $runId"

Push-Location $OutDir
try {
    & gh run download $runId -n "MinecraftInRepo-gamelibs-build"
    & gh run download $runId -n "mcrepo-fabric-mod"
}
finally {
    Pop-Location
}

Get-ChildItem $OutDir -Recurse -Include *.dll, *.jar | ForEach-Object { Write-Host "  $($_.FullName)" }

if ($Install) {
    $dll = Get-ChildItem $OutDir -Recurse -Filter "MinecraftInRepo.dll" | Select-Object -First 1
    $jar = Get-ChildItem $OutDir -Recurse -Filter "*.jar" | Where-Object { $_.Name -notlike "*-sources.jar" } | Select-Object -First 1
    if ($RepoGameDir -and $dll) {
        $plugins = Join-Path $RepoGameDir "BepInEx\plugins"
        if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
        Copy-Item $dll.FullName (Join-Path $plugins "MinecraftInRepo.dll") -Force
        Write-Host "Installed $($dll.Name) -> $plugins" -ForegroundColor Green
    }
    if ($MinecraftDir -and $jar) {
        $mods = Join-Path $MinecraftDir "mods"
        if (-not (Test-Path $mods)) { New-Item -ItemType Directory -Path $mods | Out-Null }
        Copy-Item $jar.FullName (Join-Path $mods $jar.Name) -Force
        Write-Host "Installed $($jar.Name) -> $mods" -ForegroundColor Green
    }
}

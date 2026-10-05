# Builds the Minecraft side of the mod (mcrepo jar, Fabric 1.21.1).
#
# Usage (PowerShell, Windows):
#   .\scripts\build-mc-mod.ps1
#   .\scripts\build-mc-mod.ps1 -MinecraftDir "$env:APPDATA\.minecraft" -Install
#
# Requires: a JDK. Fabric Loom 1.18 (pinned by mc-mod/gradle.properties) needs
# JDK 25 to *run Gradle*; the mod itself is compiled to Java 21 bytecode.
# JDK 25: https://adoptium.net/temurin/releases/?version=25

param(
    [string]$MinecraftDir,
    [switch]$Install,
    [string]$JavaHome = $env:JAVA_HOME
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$mcMod = Join-Path $root "mc-mod"

if ($JavaHome) {
    $env:JAVA_HOME = $JavaHome
    $env:PATH = "$JavaHome\bin;$env:PATH"
}

$java = Get-Command java -ErrorAction SilentlyContinue
if (-not $java) {
    Write-Error "java was not found in PATH. Install a JDK (25 recommended for Fabric Loom 1.18) and/or pass -JavaHome."
}
$versionOutput = & java -version 2>&1 | Select-Object -First 1
Write-Host "Using: $versionOutput"
if ($versionOutput -notmatch '"?(2[1-9]|[3-9][0-9])') {
    Write-Warning "Fabric Loom 1.18 requires JDK 21+ (25+ recommended). Builds often fail on older JDKs."
}

Push-Location $mcMod
try {
    & .\gradlew.bat build --no-daemon
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$jar = Get-ChildItem (Join-Path $mcMod "build\libs") -Filter "*.jar" | Where-Object { $_.Name -notlike "*-sources.jar" } | Select-Object -First 1
if (-not $jar) {
    Write-Error "Build finished but no jar was produced in mc-mod\build\libs."
}
Write-Host "`nBuilt: $($jar.FullName)" -ForegroundColor Green

if ($Install) {
    if (-not $MinecraftDir) {
        Write-Error "-Install needs -MinecraftDir (path of the game directory / instance folder)."
    }
    $mods = Join-Path $MinecraftDir "mods"
    if (-not (Test-Path $mods)) { New-Item -ItemType Directory -Path $mods | Out-Null }
    Copy-Item $jar.FullName (Join-Path $mods $jar.Name) -Force
    Write-Host "Installed to: $(Join-Path $mods $jar.Name)" -ForegroundColor Green
}

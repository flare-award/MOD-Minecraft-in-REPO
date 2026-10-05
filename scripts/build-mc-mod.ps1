# Builds the Minecraft side of the mod (mcrepo jar, Fabric 1.21.1).
#
#   .\scripts\build-mc-mod.ps1
#   .\scripts\build-mc-mod.ps1 -MinecraftDir "$env:APPDATA\.minecraft" -Install
#   .\scripts\build-mc-mod.ps1 -JavaHome "C:\Program Files\Eclipse Adoptium\jdk-25.0.4.101-hotspot"
#
# IMPORTANT: Fabric Loom 1.18 (pinned in mc-mod/gradle.properties) requires
# **JDK 25 or newer to run Gradle**. Java 21 is NOT enough - the build fails
# with "Dependency requires at least JVM runtime version 25". The mod itself is
# still compiled to Java 21 bytecode.
#
# This script therefore:
#   1. uses -JavaHome / $env:JAVA_HOME if it points at a JDK 25+,
#   2. otherwise looks for a JDK 25+ in the usual install folders,
#   3. otherwise offers to install Temurin 25 with winget.

param(
    [string]$MinecraftDir,
    [string]$JavaHome = $env:JAVA_HOME,
    [switch]$Install,
    [switch]$AutoInstallJdk
)

$ErrorActionPreference = "Stop"
$RequiredJavaMajor = 25
$root = Split-Path -Parent $PSScriptRoot
$mcMod = Join-Path $root "mc-mod"

function Get-JavaMajor([string]$javaExe) {
    try {
        $output = & $javaExe -version 2>&1 | Out-String
        if ($output -match 'version\s+"(?<v>[^"]+)"') {
            $parts = $Matches['v'] -split '[.\-]'
            if ($parts[0] -eq '1') { return [int]$parts[1] }   # 1.8 style
            return [int]$parts[0]
        }
    } catch {
        return 0
    }
    return 0
}

function Find-Jdk([int]$minMajor) {
    $roots = @(
        "C:\Program Files\Eclipse Adoptium",
        "C:\Program Files\Java",
        "C:\Program Files\Microsoft",
        "C:\Program Files\Zulu",
        "C:\Program Files\Amazon Corretto",
        "C:\Program Files\BellSoft",
        "C:\Program Files\Semeru",
        "C:\Program Files\GraalVM",
        "$env:LOCALAPPDATA\Programs\Eclipse Adoptium"
    )
    foreach ($base in $roots) {
        if (-not (Test-Path $base)) { continue }
        Get-ChildItem $base -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $candidate = Join-Path $_.FullName "bin\java.exe"
            if (Test-Path $candidate) {
                $major = Get-JavaMajor $candidate
                if ($major -ge $minMajor) {
                    return $_.FullName
                }
            }
        }
    }
    return $null
}

# 1) Explicit / environment JAVA_HOME -----------------------------------------
if ($JavaHome) {
    $probe = Join-Path $JavaHome "bin\java.exe"
    if ((Test-Path $probe) -and ((Get-JavaMajor $probe) -ge $RequiredJavaMajor)) {
        Write-Host "Using JAVA_HOME: $JavaHome" -ForegroundColor Cyan
    } else {
        Write-Warning "JAVA_HOME='$JavaHome' is not a JDK $RequiredJavaMajor+ - looking for another one."
        $JavaHome = $null
    }
}

# 2) Whatever 'java' on PATH is ------------------------------------------------
if (-not $JavaHome) {
    $javaCmd = Get-Command java -ErrorAction SilentlyContinue
    if ($javaCmd -and ((Get-JavaMajor $javaCmd.Source) -ge $RequiredJavaMajor)) {
        $JavaHome = Split-Path -Parent (Split-Path -Parent $javaCmd.Source)
        Write-Host "Using java from PATH: $($javaCmd.Source)" -ForegroundColor Cyan
    }
}

# 3) Search the usual install folders -----------------------------------------
if (-not $JavaHome) {
    $found = Find-Jdk $RequiredJavaMajor
    if ($found) {
        $JavaHome = $found
        Write-Host "Found a JDK $RequiredJavaMajor+: $found" -ForegroundColor Cyan
    }
}

# 4) Offer to install Temurin 25 with winget -----------------------------------
if (-not $JavaHome) {
    Write-Warning "No JDK $RequiredJavaMajor+ found. Fabric Loom 1.18 cannot run on Java 21."
    $doInstall = $AutoInstallJdk
    if (-not $doInstall) {
        $answer = Read-Host "Install Eclipse Temurin JDK 25 with winget now? [Y/n]"
        $doInstall = ($answer -eq "" -or $answer -match '^(y|Y|д|Д)')
    }
    if ($doInstall) {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
            Write-Error "winget is not available. Install JDK $RequiredJavaMajor manually: https://adoptium.net/temurin/releases/?version=25"
        }
        Write-Host "winget install -e --id EclipseAdoptium.Temurin.25.JDK" -ForegroundColor Cyan
        & winget install -e --id EclipseAdoptium.Temurin.25.JDK --accept-package-agreements --accept-source-agreements
        $JavaHome = Find-Jdk $RequiredJavaMajor
    }
    if (-not $JavaHome) {
        Write-Error "JDK $RequiredJavaMajor+ is required. Install it (https://adoptium.net/temurin/releases/?version=25) and re-run, or pass -JavaHome."
    }
}

$env:JAVA_HOME = $JavaHome
$env:PATH = "$JavaHome\bin;$env:PATH"
Write-Host "Building with: $(& "$JavaHome\bin\java.exe" -version 2>&1 | Select-Object -First 1)" -ForegroundColor Cyan

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

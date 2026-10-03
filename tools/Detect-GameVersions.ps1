# Read-only local inventory. Run in Windows PowerShell; paste the JSON output, not game files.
# No network requests, installs, or game launches.
$ErrorActionPreference = 'SilentlyContinue'
$steamRoots = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Steam'),
    (Join-Path $env:ProgramFiles 'Steam'),
    (Join-Path $env:LOCALAPPDATA 'Steam')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
$libraries = New-Object System.Collections.Generic.List[string]
foreach ($root in $steamRoots) {
    $libraries.Add($root)
    $file = Join-Path $root 'steamapps\libraryfolders.vdf'
    if (Test-Path $file) {
        foreach ($line in Get-Content $file) {
            if ($line -match '^\s*"(?:path|[0-9]+)"\s+"([^"]+)"') {
                $path = $Matches[1] -replace '\\\\','\'
                if (Test-Path $path) { $libraries.Add($path) }
            }
        }
    }
}
$repo = @()
foreach ($lib in ($libraries | Select-Object -Unique)) {
    foreach ($manifest in (Get-ChildItem (Join-Path $lib 'steamapps') -Filter 'appmanifest_*.acf' -File)) {
        $text = Get-Content $manifest.FullName -Raw
        if ($text -notmatch '"name"\s+"R\.E\.P\.O\."') { continue }
        $appId = if ($text -match '"appid"\s+"([0-9]+)"') { $Matches[1] } else { $null }
        $buildId = if ($text -match '"buildid"\s+"([0-9]+)"') { $Matches[1] } else { $null }
        $folder = if ($text -match '"installdir"\s+"([^"]+)"') { $Matches[1] } else { $null }
        $install = Join-Path (Join-Path $lib 'steamapps\common') $folder
        $globalGameManagers = Join-Path $install 'REPO_Data\globalgamemanagers'
        $repo += [ordered]@{
            appId = $appId; steamBuildId = $buildId
            exeVersion = (Get-Item (Join-Path $install 'REPO.exe')).VersionInfo.FileVersion
            globalgamemanagersSha256 = if (Test-Path $globalGameManagers) { (Get-FileHash $globalGameManagers -Algorithm SHA256).Hash } else { $null }
            bepinexInstalled = (Test-Path (Join-Path $install 'BepInEx\core\BepInEx.dll'))
            bepinexVersion = (Get-Item (Join-Path $install 'BepInEx\core\BepInEx.dll')).VersionInfo.FileVersion
            monoManaged = (Test-Path (Join-Path $install 'REPO_Data\Managed\Assembly-CSharp.dll'))
        }
    }
}
$mcRoot = Join-Path $env:APPDATA '.minecraft'
$mc = @()
if (Test-Path (Join-Path $mcRoot 'versions')) {
    foreach ($version in (Get-ChildItem (Join-Path $mcRoot 'versions') -Directory)) {
        $jsonPath = Join-Path $version.FullName ($version.Name + '.json')
        if (!(Test-Path $jsonPath)) { continue }
        try {
            $json = Get-Content $jsonPath -Raw | ConvertFrom-Json
            $fabric = @($json.libraries | Where-Object { $_.name -like 'net.fabricmc:fabric-loader:*' } | ForEach-Object { $_.name })
            $mc += [ordered]@{
                id = $json.id; type = $json.type; inheritsFrom = $json.inheritsFrom
                mainClass = $json.mainClass; fabricLoaders = $fabric
                jarPresent = (Test-Path (Join-Path $version.FullName ($version.Name + '.jar')))
            }
        } catch { }
    }
}
$profileSummary = @()
foreach ($profileFile in @('launcher_profiles.json','launcher_profiles_microsoft_store.json')) {
    $path = Join-Path $mcRoot $profileFile
    if (!(Test-Path $path)) { continue }
    try {
        $profiles = (Get-Content $path -Raw | ConvertFrom-Json).profiles
        foreach ($p in $profiles.PSObject.Properties) {
            $profileSummary += [ordered]@{ lastVersionId = $p.Value.lastVersionId; type = $p.Value.type }
        }
    } catch { }
}
[ordered]@{ repo = $repo; minecraftVersions = $mc; minecraftProfiles = $profileSummary
    fabricApiJars = @(Get-ChildItem (Join-Path $mcRoot 'mods') -Filter '*fabric-api*.jar' -File | Select-Object -ExpandProperty Name)
} | ConvertTo-Json -Depth 8

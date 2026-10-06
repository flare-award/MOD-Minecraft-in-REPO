param(
    [string]$RepoGameDir = $env:REPO_GAME_DIR,
    [string]$Out,
    [string]$Keywords,
    [string]$Grep = "patchpoints",
    [int]$MaxTypes = 40,
    [switch]$Full
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

# Without -Full we only run the patch-point grep, which stays small enough to
# paste into a chat. -Full also dumps every type matching the keyword list.
if (-not $Full) { $Keywords = "" }

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

dotnet run --project $tool -c Release --no-build -- "$managed" "$Out" "Assembly-CSharp.dll" "$Keywords" "$Grep" "$MaxTypes"
if ($LASTEXITCODE -ne 0) { throw "ApiDump failed ($LASTEXITCODE)" }

$size = (Get-Item $Out).Length
Write-Host ""
Write-Host "Wrote $Out ($([math]::Round($size / 1KB, 1)) KB)." -ForegroundColor Green
if ($size -lt 200KB) {
    Write-Host "Small enough to paste: open it and copy the text into the chat." -ForegroundColor Green
} else {
    Write-Host "Too big to paste - send it by committing it to the branch:" -ForegroundColor Yellow
    Write-Host "  git add docs/REPO-NOTES.md" -ForegroundColor Yellow
    Write-Host "  git commit -m 'REPO API notes'" -ForegroundColor Yellow
    Write-Host "  git push origin arena/01a10290-mod-minecraft-in-repo" -ForegroundColor Yellow
    Write-Host "or attach the file in chat if the UI allows it." -ForegroundColor Yellow
}

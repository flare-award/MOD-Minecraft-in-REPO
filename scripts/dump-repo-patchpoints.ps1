# Dumps the full member lists of the classes the host/guest patches touch.
#
# Same job as dump-repo-api.ps1, but the keyword list is baked in, so there is
# nothing long to type (a long -Keywords argument is easy to lose when copying
# into a console).
#
# Usage (PowerShell, Windows):
#   .\scripts\dump-repo-patchpoints.ps1
#   .\scripts\dump-repo-patchpoints.ps1 -RepoGameDir "E:\...\REPO"
#
# Output: docs/REPO-NOTES-2.md

param(
    [string]$RepoGameDir = $env:REPO_GAME_DIR,
    [string]$Out
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $root "docs/REPO-NOTES-2.md" }

$keywords = @(
    # movement, body, health
    'PlayerController',
    'PlayerAvatar',
    'PlayerHealth',
    'PlayerTumble',
    'PlayerDeathHead',
    'PlayerCollision',
    # camera
    'PlayerLocalCamera',
    'CameraUtils',
    'GameplayManager',
    'SpectateCamera',
    # input
    'InputManager',
    'InputKey',
    'SemiFunc',
    # interaction
    'ToolController',
    'PhysGrabber',
    # damage funnel
    'HurtCollider',
    # ownership / cutscene / level state
    'GameDirector',
    'CutsceneController',
    'MenuManager',
    'RunManager'
) -join ';'

& (Join-Path $PSScriptRoot 'dump-repo-api.ps1') -RepoGameDir $RepoGameDir -Out $Out -Full -Grep none -Keywords $keywords

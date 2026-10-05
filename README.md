# Minecraft in R.E.P.O.

*[Русская версия: справочник [README-RU.md](README-RU.md) и пошаговый туториал
[TUTORIAL-RU.md](TUTORIAL-RU.md) — Russian guide and start-to-finish
walkthrough, from installation to the first TNT blast]*

**Put real Minecraft inside R.E.P.O.** Minecraft's camera follows the R.E.P.O.
camera, the live Minecraft window is rendered inside the game, and TNT you
detonate in Minecraft blows up R.E.P.O.'s enemies, items, valuables — and your
own crew.

Two games, one shared world:

```
+--------------------------+        TCP 127.0.0.1:47621        +--------------------------+
|        R.E.P.O.          |  -------------------------------  |        Minecraft         |
|  (BepInEx plugin)        |   camera pose  (60 Hz)            |  (Fabric mod, 1.21.1)    |
|                          |  ------------------------------>  |                          |
|  MinecraftInRepo.dll     |   explosions   (event lines)      |  mcrepo-1.1.0.jar        |
|  - window capture        |  <------------------------------- |  - moves the player to   |
|  - in-game overlay       |                                   |    the R.E.P.O. camera   |
|  - blast damage          |                                   |  - reports every boom    |
+--------------------------+                                   +--------------------------+
```

## Features

- **Real Minecraft inside R.E.P.O.** — the actual Minecraft window is captured
  (PrintWindow/BitBlt) and drawn as a ghost overlay or picture-in-picture.
- **Camera follow** — the R.E.P.O. camera pose is streamed to Minecraft, which
  moves its player to match (smoothed per render frame, with corrections
  suppressed). FOV is synced too.
- **TNT that matters** — explosions in Minecraft (TNT, creepers, beds, end
  crystals) are mapped back into R.E.P.O. world space and damage
  `EnemyHealth`, `PlayerHealth`, and fling/shatter items & valuables through
  the game's own physics and destruction paths.
- **One-key calibration (F7)** — stand somewhere, press F7, and the mod
  computes the rotation/translation between the two worlds from the two
  cameras' current poses.
- No Harmony patches of game code, no fabric-api dependency, no asset bundles:
  everything is additive, and the bridge is loopback-only.
- **Solo-first.** Singleplayer is the supported mode: the R.E.P.O. plugin idles
  completely as soon as it detects a multiplayer session (`General.SinglePlayerOnly`
  defaults to `true`), so co-op stays vanilla-clean instead of half-working.
  Minecraft gets three solo-friendly tweaks for free: no auto-pause when its
  window loses focus, the integrated server's player copy follows the camera
  (so TNT far from spawn still ticks and explodes), and creative flight without
  needing cheats.

## Requirements

| Side | What |
|---|---|
| R.E.P.O. | Windows install + [BepInEx 5.4.23.x](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/) |
| Minecraft | Minecraft **1.21.1** with [Fabric loader](https://fabricmc.net/use/) (any Fabric profile/installation) |
| Both | The two files from this repo's [Releases/CI artifacts](#building-from-source) |

> Play fair: use this in your own singleplayer runs or with friends who are in
> on it. The mod changes PvE co-op gameplay; it is not a tool for messing with
> strangers' sessions.

## Install

### 1. R.E.P.O. side

1. Install BepInEx for R.E.P.O. (r2modman/Gale or manually — see the
   [R.E.P.O. Modding Wiki](https://repomods.com/)).
2. Copy `MinecraftInRepo.dll` into `R.E.P.O.\BepInEx\plugins\`.

### 2. Minecraft side

1. Create a Fabric installation for **Minecraft 1.21.1** in the official
   launcher (or use any Fabric-capable launcher).
2. Drop `mcrepo-1.1.0.jar` into that installation's `mods` folder.
   No other mods are required (fabric-api is *not* needed).
3. Launch it. The log line `Bridge listening on 127.0.0.1:47621` means it's
   ready. Create/load any **singleplayer** world — a superflat world makes a
   great "voxel twin" stage for your R.E.P.O. levels. Cheats are not required.

### 3. Play

1. Start R.E.P.O. (modded). Once Minecraft is running, the status line in the
   bottom-left corner says `[Minecraft] linked` and the overlay appears.
2. In a level, stand where you want the two worlds to line up, look in a
   meaningful direction, and press **F7** once. This stores the alignment in
   `BepInEx\config\MinecraftInRepo.cfg`.
3. Light some TNT in Minecraft. Watch R.E.P.O. pay for it.

If the overlay shows the Minecraft menu instead of the world, hit Play on your
Fabric profile — the capture follows whatever window is titled "Minecraft*".

## Hotkeys

| Key | Action |
|---|---|
| F6 | Toggle camera follow (Minecraft camera follows R.E.P.O.) |
| F7 | Calibrate: align the two worlds at your current position/look direction |
| F8 | Cycle overlay: FullScreen ghost → PiP → Off |
| F9 | Toggle explosion damage in R.E.P.O. |

## Configuration

### R.E.P.O. — `BepInEx/config/MinecraftInRepo.cfg`

| Section.Key | Default | Meaning |
|---|---|---|
| General.SinglePlayerOnly | true | Idle completely in multiplayer sessions (solo is the supported mode) |
| Net.Port | 47621 | Bridge TCP port (must match the Minecraft mod) |
| Net.SendRateHz | 60 | Camera pose update rate |
| Overlay.Mode | FullScreen | Off / FullScreen / PiP (F8) |
| Overlay.Alpha | 0.35 | Ghost overlay opacity |
| Overlay.CaptureFps | 30 | Window capture rate |
| Camera.FollowEnabled | true | Camera follow (F6) |
| Camera.AlignmentYawDeg | 0 | World alignment yaw (set by F7) |
| Map.McOrigin* / Map.RepoAnchor* | 0 | Mapping anchors (set by F7) |
| Map.Scale | 1.0 | R.E.P.O. meters per Minecraft block |
| Blast.RadiusPerPower | 1.5 | Blast radius = power × this (TNT power = 4) |
| Blast.MaxEnemyDamage | 90 | Point-blank enemy damage |
| Blast.MaxPlayerDamage | 45 | Point-blank player damage |
| Blast.ItemForce | 14 | Physics impulse on items/valuables |
| Blast.FriendlyFire | true | Your TNT can hurt your own crew |
| Blast.DestroyValuables | true | Strong blasts shatter items/valuables |
| Blast.DestroyThreshold | 0.55 | Blast falloff needed to shatter |
| Blast.StunEnemies | true | Briefly stun blasted enemies |

### Minecraft — `config/mcrepo-bridge.json`

`enabled`, `port`, `bind`, `applyCamera`, `broadcastExplosions`, `syncFov`,
`hideHud`, `requestCreativeMode`, `keepRunningWhenUnfocused`,
`driveServerPlayerInSingleplayer`. Defaults are sensible; `port` must match
`Net.Port` above.

The last two are the solo-play tweaks: `keepRunningWhenUnfocused` disables
vanilla's "pause my singleplayer world when the window loses focus" while the
bridge drives the camera (R.E.P.O. holds the focus), and
`driveServerPlayerInSingleplayer` moves the integrated server's copy of the
player to the R.E.P.O. camera so chunk simulation — and therefore your TNT —
follows the shared view.

## Multiplayer notes

**Default behaviour: the mod does nothing in multiplayer.** `General.SinglePlayerOnly`
is `true`, so while R.E.P.O. reports an online session the plugin stops camera
streaming, window capture / overlay and explosion routing entirely (the status
line says `solo-only: multiplayer session detected - mod idle`). Nothing is
synced, nothing can desync somebody else's game.

If you deliberately want it in co-op, set `General.SinglePlayerOnly = false`
and **run it as the host**: R.E.P.O. syncs health from the master client, so
enemy/player/item damage is applied host-authoritatively (the same pattern the
game's own damage uses). Non-host clients get the overlay, camera follow, blast
flash and local knockback, but no shared damage. Expect rough edges — solo is
the supported mode.

## Troubleshooting

- **"not connected"** — Minecraft isn't running, isn't on 1.21.1 Fabric with
  the mod, or `port` mismatches. Check Minecraft's `latest.log` for
  `Bridge listening on 127.0.0.1:47621`.
- **Overlay is black** — some GPU/window-manager combinations refuse
  off-screen GDI capture of OpenGL windows. Keep the Minecraft window visible
  (e.g. second monitor, or snapped beside R.E.P.O. in windowed mode); the
  BitBlt fallback captures anything visible.
- **Minecraft view drifts/rubber-bands** — the optional
  `IgnorePositionCorrectionMixin` didn't apply (check `latest.log` for mixin
  warnings). Re-calibrate with F7; on a new game version the mixin targets may
  need updating (see `docs/ARCHITECTURE.md`).
- **Worlds rotated oddly after F7** — calibrate while looking along a clear
  horizontal direction (not straight up/down), then re-check.
- **No damage in multiplayer** — `General.SinglePlayerOnly` is `true` (default),
  or you're not the host (see "Multiplayer notes" above).
- **Minecraft freezes while I play R.E.P.O.** — vanilla pauses a singleplayer
  world when the Minecraft window loses focus. Keep
  `keepRunningWhenUnfocused = true`, or press `F3 + P` / set
  `pauseOnLostFocus:false` in `options.txt`.
- **TNT just sits there and never explodes** — TNT only ticks inside simulated
  chunks. Keep `driveServerPlayerInSingleplayer = true` (default) so simulation
  follows the camera, and/or raise "Simulation Distance" in Minecraft's video
  settings.

## Building from source

You need two toolchains — or none at all:

| Side | Toolchain |
|---|---|
| R.E.P.O. plugin | [.NET SDK 8+](https://dotnet.microsoft.com/download) (`dotnet` in PATH) |
| Minecraft mod | **JDK 25+** — *not* 21: Fabric Loom 1.18 refuses to run Gradle on Java 21 (`Dependency requires at least JVM runtime version 25`). The mod itself still compiles to Java 21 bytecode. Install: `winget install -e --id EclipseAdoptium.Temurin.25.JDK` or https://adoptium.net/temurin/releases/?version=25 |
| neither | Download the prebuilt files from GitHub Actions instead (see below) |

### Fastest: grab the CI artifacts

Every push builds both mods. With the [GitHub CLI](https://cli.github.com/):

```sh
./scripts/fetch-builds.sh                     # -> ./dist  (Windows: .\scripts\fetch-builds.ps1)
./scripts/fetch-builds.ps1 -RepoGameDir "C:\...\REPO" -MinecraftDir "$env:APPDATA\.minecraft" -Install
```

Or from the web: **Actions → build → latest successful run → Artifacts**
(`MinecraftInRepo-gamelibs-build` for the R.E.P.O. DLL built against the real
game assemblies, `mcrepo-fabric-mod` for the Minecraft jar).

### One-liner scripts

```powershell
# Windows
.\scripts\build-repo-mod.ps1 -RepoGameDir "C:\...\REPO" -Install
.\scripts\build-mc-mod.ps1  -MinecraftDir "$env:APPDATA\.minecraft" -Install
```

```sh
# Linux / macOS
./scripts/build-repo-mod.sh "/path/to/REPO" --install
./scripts/build-mc-mod.sh --minecraft-dir ~/.minecraft --install
```

### MinecraftInRepo.dll (R.E.P.O. plugin, C#)

```sh
# Offline / no game: build hand-written reference stubs, then the plugin
dotnet build repo-mod/Stubs/StubAssemblies.csproj -c Release
dotnet build repo-mod/MinecraftInRepo.csproj -c Release

# Against your real install (recommended for release builds):
dotnet build repo-mod/MinecraftInRepo.csproj -c Release -p:RepoGameDir="C:\...\REPO"

# Against the community game-assemblies NuGet (what CI does):
dotnet build repo-mod/MinecraftInRepo.csproj -c Release -p:UseGameLibsNuGet=true
```

The DLL lands in `repo-mod/bin/Release/`.

### mcrepo jar (Minecraft, Java)

```sh
cd mc-mod
./gradlew build      # gradle wrapper; needs JDK 25+ to RUN gradle
# jar at build/libs/mcrepo-1.1.0.jar
```

The first run downloads Minecraft 1.21.1 plus Mojang's mappings and
decompiles the game — it takes a while and needs a few GB of disk.

**If Gradle says `Dependency requires at least JVM runtime version 25. This
build uses a Java 21 JVM`** — install a JDK 25 and point Gradle at it:

```powershell
winget install -e --id EclipseAdoptium.Temurin.25.JDK      # once
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-25.0.4.101-hotspot"   # check the exact folder
$env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
java -version      # must print 25 or newer
cd mc-mod; .\gradlew build
```

`scripts/build-mc-mod.ps1` does all of that for you: it picks a JDK 25+ from
`JAVA_HOME`, PATH or the usual install folders, and offers to install Temurin 25
via winget if none is present. You can also pin it permanently with
`org.gradle.java.home=C:\\Program Files\\Eclipse Adoptium\\jdk-25...` in
`%USERPROFILE%\.gradle\gradle.properties`.

CI (GitHub Actions) builds all three configurations on every push and uploads
the artifacts — handy if you don't have the toolchains locally.

## Repository layout

```
repo-mod/     BepInEx plugin (C#): bridge client, camera sync, coordinate map,
              window capture, overlay, explosion router + API stubs for CI
mc-mod/       Fabric mod (Java, MC 1.21.1): bridge server, camera follower,
              explosion broadcaster (TNT + optional generic explosions)
launcher/     One-click launcher scripts + paths.json
scripts/      Build + artifact-fetch helpers for both mods
docs/         ARCHITECTURE.md (protocol, math, damage pipeline, API sources)
MODLOG.md     Development journal
```

## Credits & prior art

- R.E.P.O. by semiwork; Minecraft by Mojang/Microsoft — mod your own copies.
- API signatures verified against public mod sources:
  [repo-live-control](https://github.com/jkieley/repo-live-control),
  [ValuableParry](https://github.com/layfhaker/parry-mod),
  [R.E.P.O Mod Library](https://github.com/Lillious-Networks/R.E.P.O-Mod-Library),
  [REPOLib](https://github.com/ZehsTeam/REPOLib), and the
  [R.E.P.O. Modding Wiki](https://repomods.com/).
- Window-overlay technique inspired by classic "desktop capture into Unity"
  projects.

MIT licensed (see LICENSE / mc-mod/LICENSE).

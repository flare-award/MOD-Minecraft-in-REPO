# Minecraft in R.E.P.O.

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
|  MinecraftInRepo.dll     |   explosions   (event lines)      |  mcrepo-1.0.0.jar        |
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
2. Drop `mcrepo-1.0.0.jar` into that installation's `mods` folder.
   No other mods are required (fabric-api is *not* needed).
3. Launch it. The log line `Bridge listening on 127.0.0.1:47621` means it's
   ready. Create/load any world — a superflat world makes a great "voxel
   twin" stage for your R.E.P.O. levels.

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
`hideHud`, `requestCreativeMode`. Defaults are sensible; `port` must match
`Net.Port` above.

## Multiplayer notes

R.E.P.O. syncs health from the master client, so **run the mod as the host**
for full effect: enemy/player/item damage is applied host-authoritatively
(same pattern the game's own damage uses). Non-host clients get the overlay,
camera follow, blast flash and local knockback, but no shared damage.

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
- **No damage in multiplayer** — you're not the host (see above).

## Building from source

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
./gradlew build      # gradle wrapper; JDK 21 required
# jar at build/libs/mcrepo-1.0.0.jar
```

CI (GitHub Actions) builds all three configurations on every push and uploads
the artifacts — handy if you don't have the toolchains locally.

## Repository layout

```
repo-mod/     BepInEx plugin (C#): bridge client, camera sync, coordinate map,
              window capture, overlay, explosion router + API stubs for CI
mc-mod/       Fabric mod (Java, MC 1.21.1): bridge server, camera follower,
              explosion broadcaster (TNT + optional generic explosions)
launcher/     One-click launcher scripts + paths.json
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

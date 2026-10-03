# Minecraft in R.E.P.O. — development log

## Requested experience
Real Minecraft Java Edition shown on an in-world portal that follows R.E.P.O.'s camera; Minecraft TNT damages nearby R.E.P.O. enemies, items, valuables and players.

## Environment / limits
This checkout contains no game installation, Minecraft client/server, modding SDK, binaries or Windows runtime. The user reports both games are installed on their Windows machine, but they are not accessible here. No game files or extracted assets are committed.

## Vertical slice (incomplete)
`src/RepoMinecraftBridge` is a BepInEx 5 Unity plugin prototype. It attaches a green placeholder quad in front of `Camera.main`, binds a loopback-only UDP listener on port 24865 and applies a physics impulse to nearby rigidbodies on receiving the exact packet `TNT`. This is **not yet** live Minecraft rendering, Minecraft camera synchronization, TNT detection, or R.E.P.O. gameplay damage. It is not multiplayer-safe; use offline only. No game launch or in-game verification has occurred.

## Next steps to make the requested mod real
1. Confirm exact R.E.P.O. build, Unity version, BepInEx loader and Minecraft Java + Fabric versions. Build against the installed game's managed assemblies; test camera resolution across scenes.
2. Implement a Fabric companion mod to capture TNT explosion events in the Minecraft *server world* and send local authenticated/paired events including dimension and position. Map those coordinates to portal-local R.E.P.O. coordinates; do not treat every explosion as occurring at portal centre.
3. Implement unobscured Minecraft window capture on Windows (Windows Graphics Capture or equivalent) and upload frames to a Unity texture; account for resize, device loss and OpenGL rendering. Launch Minecraft through the user's legitimate launcher, not bundled binaries. Map R.E.P.O. camera yaw/pitch to Minecraft's camera via companion mod, avoiding feedback loops.
4. Integrate actual R.E.P.O. damage APIs for enemies, valuables, items and players with ownership/authority checks; rigidbody force is not damage. Test each target type offline in the real game, including explosions after scene changes and shutdown.

## Build prototype (not yet verified)
With .NET SDK and BepInEx installed, set `RepoManaged` to the R.E.P.O. install directory (the directory containing `REPO_Data` and `BepInEx`), then run `dotnet build src/RepoMinecraftBridge/RepoMinecraftBridge.csproj -p:RepoManaged="C:\\path\\to\\REPO"`. Copy the resulting DLL to `BepInEx/plugins` only after backing up your installation. A newer Unity game may require different reference assemblies; do not assume this project compiles against every release.

## Local version detection requested
Checked this sandbox on 2026-10-03: Linux, no mounted Windows drives or Steam/Minecraft installations under `/home/user`, `/mnt`, or `/media`; no .NET SDK. The user's Windows installation is not available to the sandbox. Added `tools/Detect-GameVersions.ps1`, a read-only inventory script for the user's Windows PC. It reports Steam build ID, game binary metadata/hash, loader presence, Minecraft installed version JSON and launcher profile version IDs without uploading game binaries or account information. Do not pin game APIs until the inventory is returned; `latest` is not an exact version.

The Windows inventory script now saves `tools/game-versions.json` beside itself rather than relying on console output; this file is gitignored because it describes a user's local installation. It has not been run here (PowerShell and the game installs are unavailable).

An inventory run returned all-empty lists. This does not establish versions: the script may have missed custom install locations (or run on a different PC/account). The detector now accepts `-RepoPath` and `-MinecraftPath` and emits privacy-preserving discovery counts; the user must supply actual install paths or a populated inventory before version-dependent integration.

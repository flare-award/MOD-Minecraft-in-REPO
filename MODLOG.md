# MODLOG — Minecraft in R.E.P.O.

Development journal (per `AGENTS.md`). Session of 2026-10-03.

## Request

> Put real Minecraft inside R.E.P.O. Minecraft's camera should follow
> R.E.P.O.'s, and its TNT should blow up enemies, items, valuables and players.

## Recon

- The checkout contained only the universal-modder toolkit docs; no mod code
  existed yet. R.E.P.O. (semiwork) is a Unity 2022.3 Mono game, modded with
  BepInEx 5.4.23.x (confirmed via the R.E.P.O. Modding Wiki, repomods.com,
  and Thunderstore's BepInExPack).
- The community wiki recommends `Linkoid.Repo.Plugin.Build` +
  `R.E.P.O.GameLibs.Steam` NuGet for references; many shipping mods instead
  reference `<game>/REPO_Data/Managed/*.dll` directly. Both routes are
  supported by the csproj here, plus an offline stub mode.
- Verified game API symbols by cloning public mods that compile against the
  real assemblies:
  - `jkieley/repo-live-control` → `PlayerHealth.HurtOther(int, Vector3, bool,
    int, bool)`, `PlayerAvatar.ForceImpulse/PlayerDeath/Revive`,
    `PlayerHealth.health/maxHealth` fields, Photon RPC patterns.
  - `Lillious-Networks/R.E.P.O-Mod-Library` → `PlayerHealth.Hurt(int, bool)`,
    `LevelGenerator.Instance/Generated`, `EnemyHealth.dead` field, item tag
    `Phys Grab Object`, `ValuableObject.dollarValueCurrent`.
  - `layfhaker/parry-mod` → `PhysGrabObjectImpactDetector.DestroyObject(bool)`
    + `destroyDisable` (the vanilla shatter path), `SemiFunc.PlayerGetLocal`,
    `SemiFunc.IsMasterClientOrSingleplayer`, reflection patterns for
    `EnemyRigidbody.rb`.
  - Wiki research docs (Repo-Assess/REPOCAOS analyses) → `EnemyHealth.Hurt(int,
    Vector3)` is the central host-authoritative enemy damage entry point.

## Route

Two cooperating mods + local bridge:

1. `mc-mod/` — Fabric client mod for Minecraft 1.21.1 (pinned; yarn
   `1.21.1+build.3`, loader 0.16.9). No fabric-api dependency; all hooks are
   plain Mixins. Runs a loopback TCP bridge, moves the player to follow the
   R.E.P.O. camera, broadcasts explosions.
2. `repo-mod/` — BepInEx plugin. Connects to the bridge, streams the
   R.E.P.O. camera, captures the Minecraft window (PrintWindow/BitBlt) into
   an in-game overlay, and converts explosions into R.E.P.O. damage.

Rejected alternatives:
- CEF/overlay-browser inside Unity for Minecraft Classic/Eaglercraft: not
  "real Minecraft", heavy native dependencies.
- Voxel re-implementation inside Unity: not real Minecraft either.
- Shared-memory camera sync: no advantage over TCP on loopback.

## Key decisions

- **Protocol**: newline-delimited JSON (`cam`, `getpos`, `boom`, `ping`,
  `hello`, `pos`, `pong`). Full spec in docs/ARCHITECTURE.md.
- **Camera math**: directions are converted analytically
  (`yaw = atan2(-look.x, look.z)`, `pitch = asin(-look.y)`), so the
  Unity/MC handedness difference collapses into a single configurable
  alignment yaw. F7 calibration derives translation + yaw from the two
  cameras' live poses.
- **Camera smoothness**: Minecraft applies the pose every *render frame*
  (GameRenderer.render HEAD mixin, exponential smoothing) with a 20 Hz tick
  fallback; server position corrections are cancelled while the pose stream
  is fresh. The player is kept docile (flying, invulnerable, noClip, zero
  input) so vanilla physics never fights the camera.
- **Damage authority**: R.E.P.O. syncs health from the master client, so
  damage is applied only when `IsMasterClientOrSingleplayer()`; non-hosts
  keep overlay/camera/flash/local knockback. This mirrors how every serious
  R.E.P.O. gameplay mod handles damage.
- **Item destruction** goes through the game's own
  `PhysGrabObjectImpactDetector.DestroyObject` path (host-only) instead of
  `Object.Destroy`, keeping multiplayer state coherent. Knockback uses real
  physics (`AddExplosionForce`, `ForceImpulse`).
- **Resilience**: TNT hook targets the ancient, stable `TntEntity.explode()`
  (power read reflectively); the enum-heavy `ServerWorld.createExplosion`
  hook and the position-correction suppression live in a `required:false`
  mixin config so a future rename degrades instead of breaking.
- **Buildability without the game**: `repo-mod/Stubs/` compiles
  hand-written, signature-verified declarations of every external API into a
  stub `Assembly-CSharp.dll`. Three reference modes (game / nuget / stubs)
  share one csproj.

## Sandbox constraints (honesty section)

- The sandbox had no .NET SDK, no JDK, and no access to nuget.org /
  maven.fabricmc.net / the Debian repos (only GitHub, npm and PyPI were
  reachable), so **neither half could be compiled locally**. Verification is
  delegated to the GitHub Actions workflow in `.github/workflows/build.yml`,
  which builds: the plugin against stubs, the plugin against the real
  `R.E.P.O.GameLibs.Steam` assemblies, and the Fabric mod via Loom.
- **Not verified** (needs a human with the games): runtime behavior in
  R.E.P.O. (overlay look, damage feel, multiplayer replication), PrintWindow
  capture of a GLFW/OpenGL window on the user's GPU/driver, and F7
  calibration ergonomics.
- R.E.P.O. is an online co-op game; the mod deliberately avoids touching
  Photon internals, applies damage only with host authority, and the README
  tells players to use it with consenting friends. No anti-cheat, DRM or
  ownership checks are bypassed; no game files or decompiled code are
  committed.

## Result

- `repo-mod/` — complete BepInEx plugin (~1,600 lines C#) + stub assemblies.
- `mc-mod/` — complete Fabric mod (~700 lines Java) with gradle wrapper.
- `launcher/` — PowerShell/bat launcher + `paths.json` template.
- `docs/ARCHITECTURE.md`, `README.md`, CI workflow.
- Status: **in-progress** — code complete, pending CI compile results and
  in-game verification by a human with both games installed.

## CI iteration notes (same session)

- Round 1 (stubs only were not enough): compiling against the real
  R.E.P.O.GameLibs.Steam assemblies caught `GUIStyleState.color` → the real
  property is `textColor`; also a `yield return` inside try/catch in the
  connect coroutine. Both fixed.
- Round 2: fabric-loom 1.9-SNAPSHOT no longer exists on maven.fabricmc.net.
  CI now resolves the newest loom snapshot from maven metadata at run time.
- Round 3: loom 1.18-SNAPSHOT requires JVM 25; CI runs Gradle on JDK 25
  (mod bytecode still targets Java 21 via options.release).
- Round 4: loom 1.18 split the plugin into `net.fabricmc.fabric-loom`
  (non-obfuscated MC 26+) and `net.fabricmc.fabric-loom-remap` (obfuscated
  versions like 1.21.1, paired with `mappings loom.officialMojangMappings()`
  and `modImplementation "net.fabricmc:fabric-loader:..."`). All Java sources
  use Mojang official names (`Minecraft`, `LocalPlayer`, `PrimedTnt` in
  `net.minecraft.world.entity.item`, `Level.explode`, `GameRenderer.render(DeltaTracker, boolean)`,
  `ClientPacketListener.handleMovePlayer`, `setYRot`/`setXRot`/`noPhysics`/`xOld`,
  `abilities.mayfly`, `options.hideGui`/`setCameraType`/`fov().set(...)`,
  `getSingleplayerServer()`, `getCommands().performPrefixedCommand`).
- Round 5: all three CI jobs (`repo-mod-stubs`, `repo-mod-gamelibs`, `mc-mod`)
  compiled cleanly and produced artifacts (`MinecraftInRepo.dll` and
  `mcrepo-1.0.0.jar`).

## Solo mode follow-up (user: "I only play singleplayer, co-op will be buggy")

Requirements that came out of it: the mod must be *right* for one person
playing alone, and it must not half-work (or grief) in an online session.

### R.E.P.O. plugin

- New `PlayModeGate` component polls `SemiFunc.IsMultiplayer()` once per second
  (wrapped in try/catch: in the menu / during loading the API can throw, and
  "unknown" keeps the previous state instead of flapping between modes).
- New `General.SinglePlayerOnly` config entry, default `true`. While blocked:
  `CameraSync` stops streaming poses, `MinecraftCapture` skips capture,
  `MinecraftOverlay` draws no feed, and `ExplosionRouter` drops blasts before
  touching any game object. The status line reports
  `solo-only: multiplayer session detected - mod idle`.
- Set it to `false` and host a lobby if you really want co-op; that path is
  unchanged (host-authoritative damage), just no longer the default.

### Minecraft mod (Fabric 1.21.1)

Three problems that only show up in *singleplayer*, all fixed:

1. **Vanilla pauses the world when the window loses focus** — and in solo play
   R.E.P.O. holds the focus, so TNT would freeze mid-fuse. While the bridge
   drives the camera the mod sets `pauseOnLostFocus = false` (the programmatic
   version of `F3 + P` / `pauseOnLostFocus:false` in `options.txt`) and restores
   the previous value when R.E.P.O. disconnects. Looked up reflectively on
   `Options` (falling back to `Minecraft`) and supporting both a plain
   `boolean` field and an `OptionInstance<Boolean>`, so a future rename cannot
   break the build.
2. **The integrated server decides which chunks are simulated.** If it still
   thinks the player is at spawn, TNT lit next to the R.E.P.O. camera is never
   ticked. `driveServerPlayerInSingleplayer` now moves the *server-side* copy of
   the player to the camera pose via `server.execute(...)` (server thread, not
   the client thread) with no gravity, no physics and zero velocity. Bonus:
   there is nothing left for the server to correct, so the optional
   `IgnorePositionCorrectionMixin` matters less.
3. **Flight without cheats.** `/gamemode creative` needs cheats enabled; instead
   the abilities (`mayfly`, `flying`, `invulnerable`) are set directly on the
   `ServerPlayer` and pushed with `onUpdateAbilities()`, which works on any
   singleplayer world. Original values are restored when the bridge goes idle.

`BridgeConfig` gained `configVersion = 2` plus an `upgrade()` path, because
Gson fills missing booleans with `false` — an old config file would otherwise
silently disable the two new solo features.

### Build ergonomics

- `scripts/build-repo-mod.{ps1,sh}` and `scripts/build-mc-mod.{ps1,sh}` build
  (and optionally install) each side, with checks for `dotnet` / `java` and a
  warning when the JDK is too old for Loom 1.18.
- `scripts/fetch-builds.{ps1,sh}` pull the artifacts of the latest successful
  GitHub Actions run via `gh` — the recommended path for anyone who does not
  want to install a JDK and decompile Minecraft locally.
- Version bumped to 1.1.0 on both sides; README gained a solo-play section and
  the new config rows, and a full Russian guide was added (`README-RU.md`).

CI run `37322987915`: all three jobs green, artifacts
`MinecraftInRepo-gamelibs-build` (20,049 B), `MinecraftInRepo-stubs-build`
(19,882 B), `mcrepo-fabric-mod` (22,826 B).

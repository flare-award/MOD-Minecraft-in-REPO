# Architecture

Two processes, one shared world. All traffic is loopback TCP, newline-delimited
UTF-8 JSON, single client (the R.E.P.O. running on the same machine).

## Wire protocol (version 1)

R.E.P.O. → Minecraft:

```json
{"t":"hello","proto":1}
{"t":"cam","x":0.5,"y":64.0,"z":-3.25,"yaw":180.0,"pitch":-12.5,"fov":70.0}
{"t":"getpos"}
{"t":"ping"}
```

Minecraft → R.E.P.O.:

```json
{"t":"hello","proto":1,"mc":"1.21.1","bridge":"1.0.0"}
{"t":"pos","x":0.5,"y":64.0,"z":-3.25,"yaw":180.0,"pitch":-12.5}
{"t":"boom","x":3.0,"y":65.0,"z":2.0,"power":4.0,"fire":false,"source":"tnt"}
{"t":"pong"}
```

- `cam` is sent at `Net.SendRateHz` (default 60 Hz) while F6-follow is on.
- `boom` fires from `TntEntity.explode()` (always) and from
  `ServerWorld.createExplosion(...)` (optional mixin; skips TNT to avoid
  double reports). Sources: `tnt`, `generic`, or the explosion-interaction
  name (creepers report through the generic path's entity, beds via
  `createExplosion` on use, etc.).
- Keepalives: R.E.P.O. pings every 2 s; a dead socket triggers reconnect
  attempts every `Net.ConnectInterval` seconds.

## Coordinate mapping

Both engines are Y-up; 1 Minecraft block ≈ 1 R.E.P.O. meter at `Scale = 1`.

```
repo = AnchorRepo + RotY(AlignmentYawDeg) * (mc - OriginMc) * Scale
mc   = OriginMc  + RotY(-AlignmentYawDeg) * (repo - AnchorRepo) / Scale
```

`RotY` is Unity-convention rotation around Y (positive turns +Z toward +X).

### Camera conversion

The mod sends directions, not Euler angles, so handedness quirks cancel out.
Given the R.E.P.O. camera forward `u`:

```
look   = RotY(-AlignmentYawDeg) * normalize(u)          # Minecraft axes
pitch  = asin(-look.y)
yaw    = atan2(-look.x, look.z)     # MC: look = (-sin yaw · cos pitch, -sin pitch, cos yaw · cos pitch)
```

Minecraft applies the pose to its local player every render frame
(exponential smoothing, snap beyond 48 blocks), with the eye-height
compensation `playerPos = cameraPos - eyeHeight`, zeroed velocity/input,
`noClip` + flying + invulnerable abilities, and suppression of server
position corrections while the pose stream is fresh (< 3 s).

### F7 calibration

1. R.E.P.O. sends `getpos`; Minecraft answers with its player pose.
2. `OriginMc` ← Minecraft camera position, `AnchorRepo` ← local R.E.P.O.
   avatar position.
3. `AlignmentYawDeg` ← `unityYaw(repoCamForward) - unityYaw(mcLookHorizontal)`,
   so whatever direction you're looking in R.E.P.O. is what Minecraft now
   shows — the two cameras coincide immediately.

## TNT → R.E.P.O. damage pipeline

On `boom(x, y, z, power)` (main thread):

1. `center = Map.McToRepo(xyz)`, `radius = power · RadiusPerPower`.
2. **Host check** — `SemiFunc.IsMasterClientOrSingleplayer()`. R.E.P.O.
   health sync is host-authoritative; non-hosts only get flash + local
   knockback.
3. **Enemies** — every `EnemyHealth` in radius takes
   `Hurt(round(MaxEnemyDamage · falloff), direction)` with
   `falloff = 1 - dist/radius`, plus optional knockback via its private
   `EnemyRigidbody.rb` Rigidbody (reflection) and a stun via
   `EnemyStateStunned.Set(float)` (reflection).
4. **Players** — every living `PlayerAvatar` in radius takes
   `playerHealth.HurtOther(damage, Vector3.zero, false, -1, false)` (the
   same call host tooling mods use for arbitrary avatars; falls back to
   `Hurt(damage, false)` for the local player) and
   `ForceImpulse((dir + 0.35·up) · knockback)` — TNT throws people even
   with friendly fire off.
5. **Items & valuables** — every `PhysGrabObject` in radius gets
   `Rigidbody.AddExplosionForce(ItemForce · power, center, radius, 1, Impulse)`;
   when `falloff ≥ DestroyThreshold` and `DestroyValuables` is on, the
   object is shattered through the game's own
   `PhysGrabObjectImpactDetector.DestroyObject(true)` path (host-only), so
   destruction stays network-correct.
6. Overlay flash + log line with hit counts.

## Solo mode (default) and multiplayer gating

Singleplayer is the supported mode on both sides.

**R.E.P.O. (`PlayModeGate`)** polls `SemiFunc.IsMultiplayer()` once per second.
`General.SinglePlayerOnly` (default `true`) turns a detected multiplayer session
into a full stand-down: `CameraSync.Update`, `MinecraftCapture`'s loop,
`MinecraftOverlay.OnGUI` and `ExplosionRouter.ApplyBlast` all early-return while
blocked, so no game object is read or written. "Unknown" (menu, loading, API
threw) keeps the previous decision instead of flapping. The bridge socket stays
open but idle so reconnecting to a solo run needs no restart.

**Minecraft** applies three singleplayer-only tweaks, all reverted when the
bridge stops driving the camera (`CameraSync.revertSessionTweaks`):

| Tweak | Why | How |
|---|---|---|
| `keepRunningWhenUnfocused` | R.E.P.O. owns the window focus; vanilla pauses a singleplayer world 0.5 s after Minecraft loses it, freezing TNT fuses | `pauseOnLostFocus = false`, found reflectively on `Options` (`boolean` or `OptionInstance<Boolean>`) |
| `driveServerPlayerInSingleplayer` | the integrated server decides which chunks tick; TNT far from the server's idea of the player never explodes | `server.execute(...)` moves the `ServerPlayer` copy to the camera pose with no gravity / no physics / zero velocity |
| `requestCreativeMode` | `/gamemode creative` needs cheats, and a survival player cannot fly, so the camera would be pulled back to the ground | abilities `mayfly` / `flying` / `invulnerable` set on the `ServerPlayer` + `onUpdateAbilities()` |

None of these run against a remote server (`getSingleplayerServer() == null` is
the guard), so joining a multiplayer server leaves its authority untouched.

## Threading

- **R.E.P.O.**: one reader thread per connection; it only enqueues callbacks
  into a `ConcurrentQueue<Action>` drained by `Update()`. All Unity/game
  access happens on the main thread. Writes are locked.
- **Minecraft**: accept thread + per-client reader/writer threads; the
  explosion hooks run on the integrated server thread and only enqueue JSON
  lines. Camera state is handed over via a locked pose record.

## Minecraft mixin targets (official mappings, pinned to 1.21.1)

Required config (`mcrepo.mixins.json`):

| Mixin | Target | Notes |
|---|---|---|
| MinecraftClientTickMixin | `Minecraft.tick()V` | 20 Hz camera/ability update, player snapshot |
| GameRendererCameraMixin | `GameRenderer.render(LDeltaTracker;Z)V` | per-frame camera application |
| TntEntityExplosionMixin | `PrimedTnt.explode()V` | stable no-arg signature; power read reflectively |

Optional config (`mcrepo.optional.mixins.json`, `required:false`):

| Mixin | Target | Notes |
|---|---|---|
| ServerWorldExplosionMixin | `Level.explode(Entity,DDDFFZLLevel$ExplosionInteraction;)LExplosion;` | all non-TNT explosions |
| IgnorePositionCorrectionMixin | `ClientPacketListener.handleMovePlayer` | suppress rubber-banding |

If the optional targets rename in a newer game version, that config is
skipped and the core features keep working.

## R.E.P.O. API verification

The C# side compiles against three interchangeable reference modes (see
`repo-mod/MinecraftInRepo.csproj`): the real install, the community
`R.E.P.O.GameLibs.Steam` NuGet, or hand-written stubs
(`repo-mod/Stubs/`). Every direct (non-reflection) symbol used by the mod was
verified against public mod sources that compile against the real game:

| Symbol | Verified via |
|---|---|
| `PlayerHealth.HurtOther(int, Vector3, bool, int, bool)`, `PlayerAvatar.ForceImpulse/PlayerDeath/Revive`, `PlayerHealth` fields `health`/`maxHealth` | jkieley/repo-live-control |
| `PlayerHealth.Hurt(int, bool)`, `LevelGenerator.Instance/Generated`, `RunManager.instance`, `SemiFunc.IsMultiplayer`, item tag "Phys Grab Object" | Lillious R.E.P.O Mod Library |
| `EnemyHealth.Hurt(int, Vector3)` as the central host damage entry point | R.E.P.O. modding wiki research docs (Repo-Assess / REPOCAOS analyses) |
| `ValuableObject.dollarValueCurrent`, `PhysGrabObjectImpactDetector.DestroyObject(bool)` + `destroyDisable`, `SemiFunc.PlayerGetLocal`, `SemiFunc.IsMasterClientOrSingleplayer` | layfhaker/parry-mod |
| Reflection-only: `EnemyRigidbody.rb`, `EnemyStateStunned.Set(float)` | layfhaker/parry-mod (same access pattern) |

Private members are only touched through reflection with try/catch fallbacks,
so a game update degrades gracefully instead of crashing.

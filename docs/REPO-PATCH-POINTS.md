# R.E.P.O. patch points (notes file)

Written from `docs/REPO-NOTES.md` and `docs/REPO-NOTES-2.md`, which are produced
by `tools/ApiDump` from the real `Assembly-CSharp.dll` (type names, member names
and arities only — no game code).

The rule from the porting guide: **patch the funnel, not the consumers** — one
input sampler, one movement step, one camera update, one hazard function, one
death function, and every patch does nothing while the host owns the player.

Everything here is reached by **reflection** (`repo-mod/src/Host/RepoApi.cs`),
never by compiling against a game type: the plugin has to build with no game
installed (offline stubs) and must not break when the game renames something. A
missing member logs one line and turns that feature off.

## Update order (what happens in a frame)

| Order | What | Where |
|---|---|---|
| 1 | `FixedUpdate` — the character's physics step reads the movement fields | `PlayerController.FixedUpdate` |
| 2 | `Update` — input is sampled, camera aim is applied | `PlayerController.Update`, `ToolController.Update` |
| 3 | `LateUpdate` — camera smoothing, spectate camera | `GameDirector.LateUpdate`, `SpectateCamera.LateUpdate` |
| 4 | `onBeforeRender` — **our pump runs here**, after everything above | `Plugin.Pump` → `TickAll` → `TickGuestLink` |

Writing the camera and zeroing the input from `onBeforeRender` is therefore
"after the host's own update" for free, which is exactly what the guide asks for.

## The table

| Concern | Class | Member | How the mod uses it |
|---|---|---|---|
| Local player | `PlayerAvatar` | `static instance` | who we follow / give back |
| Local controller | `PlayerController` | `static instance` | movement, physics, input fields |
| Visuals | `PlayerAvatar` | `playerAvatarVisuals` | hiding the body |
| Health | `PlayerAvatar` | `playerHealth` | health mirroring |
| Tumble | `PlayerAvatar` | `tumble` | stun → cutscene |
| Tools | `ToolController` | `static instance` | the interact key |

### Input gate (host controls off while the guest owns the body)

| Member | Action |
|---|---|
| `PlayerController.InputDirection`, `InputDirectionRaw` | zeroed every frame (kills walking) |
| `PlayerController.sprinting`, `Crouching`, `Crawling`, `Sliding`, `moving` | forced false |
| `PlayerController.JumpInputBuffer` | forced 0 (kills the jump) |
| `ToolController.InteractInput` | driven by the reserved key `G` |

Why the fields and not `SemiFunc.InputDisableMovement()` or
`InputManager.DisableMovement(t)`: those also disable the interaction ray we rely
on, and they are timers we would have to keep feeding. Zeroing the fields is one
line, needs no patch, and survives being called every frame.

For reference, the host's own input source (not patched, listed because phase 3's
input forwarding replaces it): `SemiFunc.InputDown/InputHold/InputUp(InputKey)`,
`SemiFunc.InputMovementX/Y`, `InputManager.instance.KeyHold/GetMovementX/GetMouseX`.

### Follower (the host's character while the guest owns the body)

| Member | Action |
|---|---|
| `PlayerController.Kinematic(bool)` | `true` on grab, `false` on release |
| `PlayerAvatar.transform.position` (or `playerTransform`) | moved to the guest's feet every frame |
| `PlayerController.CollisionGrounded` | forced true, so nothing starts a fall |
| `PlayerController.rb.velocity` | zeroed every frame |
| `PlayerAvatarVisuals.localVisibility` + `ApplyLocalVisibilityBody()` | hides the body |
| `GetComponentsInChildren<Renderer>().enabled` | fallback hiding, restoring exactly what we turned off |

### Camera

| Member | Action |
|---|---|
| `Camera.main.transform` (also `GameDirector.instance.MainCamera`, `CameraUtils.Instance.MainCamera`) | position = guest's eye, rotation = guest's yaw/pitch |
| `Camera.main.fieldOfView` | copied from the guest |

### Interaction

| Member | Action |
|---|---|
| `ToolController.InteractInput` | `G` held |
| `ToolController.CurrentInteraction`, `ActiveInteraction` | read to draw our own prompt (phase 5) |

The host's targeting (`ToolController.InteractionCheck`) runs with the camera we
wrote, so hold-to-interact objects work without re-implementing anything.

### Damage and death

| Member | Action |
|---|---|
| `PlayerHealth.health`, `maxHealth` (int) | read the host's damage, write the guest's health back |
| `PlayerHealth.Death()` | run the host's death when the guest dies |
| `PlayerAvatar.Revive(bool)` | revive after the guest respawns |
| `HurtCollider.playerDamage`, `playerKill`, `enemyDamage`, `deathPit` | the hazard funnel — **not patched**, we mirror health instead |

Not patching `PlayerHealth.Hurt/4` is deliberate: its signature is unknown (arity
only in the notes), and mirroring health every frame gets the same result — the
guest's armour and invulnerability decide, and the host's death flow can only
start when we start it.

### Ownership inputs (what counts as a cutscene)

| Member | Meaning |
|---|---|
| `GameDirector.instance.DisableInput` | the host itself froze the controls |
| `GameDirector.instance.currentState` | anything but `Main` (Start, End, Death, Result, Outro, Load) |
| `RunManager.instance.restarting`, `gameOver`, `allPlayersDead`, `waitToChangeScene`, `levelMainMenu`, `levelLobbyMenu`, `levelSplashScreen`, `runStarted` | loading, menus, run end |
| `PlayerAvatar.deadSet`, `spectating`, `isDisabled`, `spawned` | dead, spectating or not spawned yet |
| `PlayerTumble.isTumbling` | knocked over — the host is animating the body |
| `MenuManager.instance.currentMenuPage != null` | a host window is open (HostMenu) |

## Not used yet (later phases)

| Phase | Class | Why |
|---|---|---|
| 5 | `PhysGrabber` (`grabbed`, `grabbedObject`, `ReleaseObject`), `PhysGrabObject` | items crossing between the games |
| 5 | `ValuableObject` | valuables → Minecraft items |
| 6 | `PlayerDeathHead`, `SpectateCamera` | the host's own death flow |
| 7 | `CutsceneController`, `PlayerAvatar.OutroStart/OutroDone` | endings as cutscenes |

## Verification

`RepoApi.ReportMissing()` logs, once the first local player is seen, every member
the mod looked for and did not find:

```
[MinecraftInRepo] host API check: every member found.
```

or a list. That line is the first thing to read when something does not move.

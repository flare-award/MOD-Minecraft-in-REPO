# Protocol v2 — host ↔ guest link

Newline-delimited JSON over TCP loopback, one JSON object per line, UTF-8.
Same transport as the v1 bridge (`{"t": ...}`-style messages), a different port
and a much richer message set: v2 is the **host/guest** link from
[PLAN-HOST-GUEST-RU.md](PLAN-HOST-GUEST-RU.md), in which Minecraft (the guest)
owns the player's body and R.E.P.O. (the host) owns the world.

v1 (camera follow + TNT) keeps running untouched on its own port. The two are
independent on purpose: v2 can be developed and tested while v1 stays playable.
Host-side v2 code lives in `repo-mod/src/Host/`; the messages are defined here
because the Fabric side implements the same document.

## Conventions

* **Space.** Everything on the wire is **Minecraft space**: blocks, Y up,
  Z south, yaw 0 along +Z, pitch positive downwards. The host converts at its
  edge and nowhere else.
* **Positions** are the player's **feet** unless a field says otherwise.
* **Booleans** may be written as `true/false`, `1/0` or `"true"`. Both sides
  accept all three (`JsonLite.GetBool`).
* **Timestamps** are each side's own millisecond clock (`Environment.TickCount`
  on the host, `System.currentTimeMillis() & 0x7fffffff` on the guest). They are
  used only for liveness, never to interpolate — the two processes do not share
  a clock.
* **Message type** is field `t`. Unknown types are ignored, so a newer guest
  does not break an older host.

## Liveness

Each side stamps every message it publishes every frame.

| Side | Message | Cadence | Treats the other as dead after |
|---|---|---|---|
| host | `hs` | every frame | 3 s (guide value) |
| guest | `gs` | every tick (20/s) | 8 s (host loading screens stall its heartbeat) |

A changed `session` in the guest's `hello` counts as a **new link**: the host
resends everything that is stateful (collision, teleport, held keys).

## Host → guest

### `hs` — host state, once per frame

```json
{"t":"hs","seq":1234,"ms":91234,"owner":"GuestOwns","tseq":3,
 "loading":false,"menu":false,"vpw":1920,"vph":1080,
 "x":10.5,"y":64.0,"z":-3.25,"yaw":-90.0,"pitch":-15.0}
```

* `seq` — running frame counter. The guest paces on it: it must not run ahead of
  the host, and a stalled `seq` means the host is not ticking.
* `owner` — `HostOwns | Handoff | GuestOwns | HostMenu | Cutscene` (see the
  ownership table in [PLAN-HOST-GUEST-RU.md](PLAN-HOST-GUEST-RU.md)).
* `tseq` — teleport sequence. When it changes, the guest teleports its player to
  `(x,y,z)` and answers with `ack`.
* `loading` — the host is in a cutscene: the guest parks its player and waits for
  a teleport.
* `menu` — a host window is open: the guest drops every held key.
* `vpw/vph` — the host's viewport in pixels; the guest renders its HUD at this
  size.
* `x,y,z,yaw,pitch` — where the host's character is (Minecraft space). Used for
  the teleport target and for cutscene poses.

### `key` — a keyboard key

```json
{"t":"key","code":87,"down":true,"rep":false}
```

`code` is a **GLFW key code** (what Minecraft's `InputConstants` uses), not a
Unity `KeyCode`. `rep` marks an auto-repeat while a text field has focus.

### `mbtn` — a mouse button

```json
{"t":"mbtn","btn":0,"down":true}
```

GLFW numbering: 0 left, 1 right, 2 middle.

### `look` — mouse movement, in pixels

```json
{"t":"look","dx":6.0,"dy":-3.0}
```

The guest applies Minecraft's own sensitivity curve to it.

### `scroll` — wheel

```json
{"t":"scroll","dx":0.0,"dy":-1.0}
```

### `char` — typed text

```json
{"t":"char","s":"a"}
```

Sent only while a guest screen is open.

### `cursor` — pointer position in overlay pixels

```json
{"t":"cursor","x":960.0,"y":540.0}
```

Sent only while a guest screen is open; with no screen open, mouse movement
becomes `look` instead.

### `release` — release everything

```json
{"t":"release"}
```

Sent on every change of routing: a host menu opens, a cutscene starts, the
window loses focus, the link drops. Without it a key held at that moment stays
held in Minecraft.

### `vox` / `voxclear` — collision

```json
{"t":"vox","epoch":3,"rx":2,"ry":-1,"rz":5,"cells":"<base64 512 bytes>"}
{"t":"voxclear","epoch":4}
```

A region is **8 × 8 × 8 blocks** sampled on a **half-block** grid: 16 × 16 × 16
cells, one bit each, 512 bytes, base64 on the wire. Cell index is
`cx + 16 * (cz + 16 * cy)`, bit 0 of byte 0 is cell 0. A region's world block
origin is `(rx*8, ry*8, rz*8)`.

* `epoch` — raised whenever the host's world changes under the player (scene
  load, streamed chunk swap, a new guest). A `voxclear` or a changed epoch makes
  the guest drop everything it was told.
* An **empty** region is a legal message: it evicts what the guest had there.

The guest turns solid cells into invisible barrier blocks in its mirror world,
so Minecraft's own movement code does the collision.

### `hurt` — damage from the host

```json
{"t":"hurt","amount":6.0,"kind":"hazard"}
```

At most one every 0.5 s: Minecraft ignores repeat hits inside its invulnerability
window. Instant kills in the host become one very large `hurt`. A Creative guest
ignores these, which is the whole "hazards off" feature.

### `cmd` — commands that are not input

```json
{"t":"cmd","op":"respawn"}
{"t":"cmd","op":"closeScreen"}
{"t":"cmd","op":"pause"}
{"t":"cmd","op":"setMode","mode":"creative"}
{"t":"cmd","op":"give","id":"minecraft:emerald","n":1,"name":"Valuable"}   // phase 5
```

## Guest → host

### `hello`

```json
{"t":"hello","proto":2,"game":"minecraft","mc":"1.21.1","session":"mc-8123"}
```

### `gs` — guest state, once per tick

```json
{"t":"gs","seq":41,"ms":123456,
 "x":10.5,"y":64.0,"z":-3.25,"px":10.0,"py":64.0,"pz":-3.0,"period":50,
 "yaw":-90.0,"pitch":-15.0,"eye":1.62,"fov":70,"cam":1,"camDist":4.0,
 "hp":14,"hpMax":20,"dead":false,"screen":false,"ack":7,"mode":0}
```

* `x,y,z` — feet at the **latest** tick, `px,py,pz` — feet at the **previous**
  tick. The host interpolates between them on its own frame clock over
  `period` ms; using an already-interpolated position judders because the two
  games' frames are not in phase.
* `eye` — eye height above the feet (1.62 standing, 1.27 crouching).
* `cam` — 0 first person, 1 third person behind, 2 third person in front;
  `camDist` already includes Minecraft's zoom collision.
* `screen` — an inventory / chat / options screen covers the HUD: the cursor mode
  switches and typed text is forwarded.
* `ack` — last teleport performed; the host stays in `Handoff` until it matches
  `tseq`.
* `mode` — 0 survival, 1 creative, 2 adventure, 3 spectator.

### `ev` — events

```json
{"t":"ev","kind":"death"}
```

`kind` is `death`, `respawn` or `screen`.

### `boom` — an explosion (unchanged from v1)

```json
{"t":"boom","x":10.5,"y":64.0,"z":-3.25,"power":4.0,"fire":false,"source":"tnt"}
```

Routed by `Blast/ExplosionRouter` into R.E.P.O.'s enemies, items, valuables and
players.

## Testing without Minecraft

`tools/fake_guest.py` is a guest that speaks this document: it connects, walks a
scripted circle, answers teleports, takes damage and can be made to die or drop
the connection from the keyboard. Use it to verify the link, the ownership
machine and link-loss recovery before Minecraft is involved.

```bash
python tools/fake_guest.py --port 25671
```

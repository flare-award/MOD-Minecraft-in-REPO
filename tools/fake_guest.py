#!/usr/bin/env python3
"""A fake Minecraft guest for testing the R.E.P.O. host plugin without Minecraft.

The host/guest split (docs/PORTING-GUIDE.md) is only safe if the host behaves
when the other side appears, disappears and lies about its position. This script
is the guest for those tests: it speaks protocol v2 (docs/PROTOCOL-V2.md) over
TCP, walks a scripted circle, answers teleports, and prints everything the host
sends.

    python tools/fake_guest.py                # serve on 127.0.0.1:25671
    python tools/fake_guest.py --port 25672   # another port

Commands you can type while it runs:

    die        report the player as dead (host must fall back to a cutscene)
    revive     report it alive again
    screen     toggle "a Minecraft screen is open"
    boom       send a TNT explosion at the player's position
    stop       drop the connection (host must return control within ~3 s)
    quit       exit

Only the Python standard library is used, so it runs anywhere the host plugin's
tests are run - including on the machine that has R.E.P.O. installed.
"""

import argparse
import json
import math
import socket
import sys
import threading
import time

TICK_SECONDS = 0.05          # 20 ticks per second, like Minecraft
WALK_RADIUS = 6.0
WALK_SPEED = 4.0             # blocks per second along the circle


class FakeGuest(object):
    def __init__(self, port):
        self.port = port
        self.lock = threading.Lock()

        # The state we publish as {"t":"gs", ...}.
        self.guest = {
            "seq": 0,
            "x": 0.0, "y": 64.0, "z": 0.0,
            "px": 0.0, "py": 64.0, "pz": 0.0,
            "period": TICK_SECONDS * 1000.0,
            "yaw": 0.0, "pitch": 0.0,
            "eye": 1.62, "fov": 70.0,
            "cam": 0, "camDist": 4.0,
            "hp": 20.0, "hpMax": 20.0,
            "dead": False, "screen": False,
            "ack": 0, "mode": 0,
        }

        self.host = {}                 # last hoststate from the plugin
        self.host_seen = 0.0
        self.counts = {"hs": 0, "key": 0, "look": 0, "vox": 0, "hurt": 0, "cmd": 0, "other": 0}
        self.last_owner = None
        self.walk_time = 0.0
        self.connection = None
        self.running = True

    # ------------------------------------------------------------------ server

    def serve_forever(self):
        server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind(("127.0.0.1", self.port))
        server.listen(1)
        server.settimeout(0.5)
        print("fake guest listening on 127.0.0.1:%d (protocol v2)" % self.port)

        while self.running:
            try:
                client, _ = server.accept()
            except socket.timeout:
                continue
            except OSError:
                break

            print("host connected")
            self.connection = client
            self.send_hello(client)
            try:
                self.session(client)
            finally:
                try:
                    client.close()
                except OSError:
                    pass
                self.connection = None
                self.host = {}
                self.last_owner = None
                print("host disconnected")

        server.close()

    def send_hello(self, client):
        self.send(client, {"t": "hello", "proto": 2, "mc": "fake", "game": "minecraft",
                           "session": "fake-%d" % time.time()})

    def session(self, client):
        client.settimeout(0.05)
        buffer = b""
        next_tick = time.time()

        while self.running and self.connection is client:
            # Inbound lines from the host. A read timeout only means "nothing
            # to read yet" - only an empty read means the peer closed.
            try:
                data = client.recv(65536)
            except socket.timeout:
                data = None
            except OSError:
                break
            if data is not None:
                if not data:
                    break
                buffer += data
                while b"\n" in buffer:
                    line, buffer = buffer.split(b"\n", 1)
                    self.handle(line.decode("utf-8", "replace").strip())

            # Outbound state at Minecraft's tick rate.
            now = time.time()
            if now >= next_tick:
                next_tick = now + TICK_SECONDS
                self.step(TICK_SECONDS)
                self.send(client, self.state_message())

    def send(self, client, message):
        try:
            client.sendall((json.dumps(message) + "\n").encode("utf-8"))
        except OSError:
            self.running = False

    # ------------------------------------------------------------------- logic

    def step(self, dt):
        state = self.guest
        state["px"], state["py"], state["pz"] = state["x"], state["y"], state["z"]

        host = self.host
        if host.get("loading") or state["dead"]:
            return  # parked: the host is moving the body

        # Teleport handshake: answer every new tseq once.
        tseq = int(host.get("tseq", 0) or 0)
        if tseq != state["ack"]:
            state["x"], state["y"], state["z"] = (
                float(host.get("x", 0.0)), float(host.get("y", 64.0)), float(host.get("z", 0.0)))
            state["ack"] = tseq
            print("teleport %d -> (%.2f, %.2f, %.2f)" % (tseq, state["x"], state["y"], state["z"]))
            return

        self.walk_time += dt
        angle = self.walk_time * (WALK_SPEED / WALK_RADIUS)
        state["x"] = math.cos(angle) * WALK_RADIUS
        state["z"] = math.sin(angle) * WALK_RADIUS
        state["yaw"] = math.degrees(-angle) % 360.0
        state["seq"] += 1

    def state_message(self):
        state = dict(self.guest)
        state["t"] = "gs"
        state["ms"] = int(time.time() * 1000) % (1 << 31)
        return state

    def handle(self, line):
        if not line:
            return
        try:
            message = json.loads(line)
        except ValueError:
            print("bad json: %r" % line[:120])
            return

        kind = message.get("t")
        if kind == "hs":
            self.counts["hs"] += 1
            self.host = message
            self.host_seen = time.time()
            owner = message.get("owner")
            if owner != self.last_owner:
                print("owner: %s -> %s (loading=%s menu=%s)"
                      % (self.last_owner, owner, message.get("loading"), message.get("menu")))
                self.last_owner = owner
        elif kind in ("key", "mbtn", "look", "scroll", "char", "cursor", "release"):
            self.counts["key"] += 1
        elif kind == "vox":
            self.counts["vox"] += 1
        elif kind == "voxclear":
            self.counts["vox"] += 1
        elif kind == "hurt":
            self.counts["hurt"] += 1
            with self.lock:
                self.guest["hp"] = max(0.0, self.guest["hp"] - float(message.get("amount", 0.0)))
                if self.guest["hp"] <= 0.0:
                    self.guest["dead"] = True
        elif kind == "cmd":
            self.counts["cmd"] += 1
            print("cmd: %s" % message.get("op"))
        else:
            self.counts["other"] += 1

    def event(self, kind):
        if self.connection is not None:
            self.send(self.connection, {"t": "ev", "kind": kind})

    # ------------------------------------------------------------------ report

    def report(self):
        while self.running:
            time.sleep(2.0)
            state = self.guest
            age = "-"
            if self.host_seen:
                age = "%.1fs" % (time.time() - self.host_seen)
            print("guest: pos=(%.2f, %.2f, %.2f) hp=%.1f dead=%s screen=%s | host=%s age=%s | "
                  "hs=%d input=%d vox=%d hurt=%d cmd=%d"
                  % (state["x"], state["y"], state["z"], state["hp"], state["dead"],
                     state["screen"], self.last_owner, age, self.counts["hs"], self.counts["key"],
                     self.counts["vox"], self.counts["hurt"], self.counts["cmd"]))


def main():
    parser = argparse.ArgumentParser(description="Fake Minecraft guest (protocol v2)")
    parser.add_argument("--port", type=int, default=25671)
    args = parser.parse_args()

    guest = FakeGuest(args.port)
    threading.Thread(target=guest.serve_forever, daemon=True).start()
    threading.Thread(target=guest.report, daemon=True).start()

    print("commands: die | revive | screen | boom | stop | quit")
    try:
        while guest.running:
            try:
                command = sys.stdin.readline()
            except KeyboardInterrupt:
                break
            if not command:
                time.sleep(0.2)
                continue
            command = command.strip().lower()
            if command in ("quit", "exit", "q"):
                break
            elif command == "die":
                guest.guest["dead"] = True
                guest.event("death")
                print("player is dead")
            elif command == "revive":
                guest.guest["dead"] = False
                guest.guest["hp"] = guest.guest["hpMax"]
                guest.event("respawn")
                print("player respawned")
            elif command == "screen":
                guest.guest["screen"] = not guest.guest["screen"]
                print("screen open = %s" % guest.guest["screen"])
            elif command == "boom":
                guest.send(guest.connection, {
                    "t": "boom", "x": guest.guest["x"], "y": guest.guest["y"],
                    "z": guest.guest["z"], "power": 4.0, "fire": False, "source": "tnt"})
                print("explosion sent")
            elif command == "stop":
                if guest.connection is not None:
                    try:
                        guest.connection.close()
                    except OSError:
                        pass
                print("connection dropped - the host must take its controls back")
            elif command == "":
                continue
            else:
                print("unknown command: %r" % command)
    finally:
        guest.running = False
    print("bye")


if __name__ == "__main__":
    main()

// The host's (R.E.P.O.'s) published state, sent once per frame.
//
//   {"t":"hs","seq":n,"t":ms,"owner":"GuestOwns","tseq":n,"loading":true,"menu":false,
//    "vpw":1920,"vph":1080,"x":..,"y":..,"z":..,"yaw":..,"pitch":..}
//
// The guest paces its frames on "seq" and takes the teleport handshake from
// "tseq" plus the position fields: when "tseq" changes it teleports its player
// to (x,y,z) and answers with "ack" in its own state.

using MinecraftInRepo.Net;

namespace MinecraftInRepo.Host
{
    public struct HostState
    {
        /// <summary>Running frame counter. Incremented by the link when it publishes.</summary>
        public long Seq;

        /// <summary>Host wall clock in milliseconds (Environment.TickCount).</summary>
        public long TimestampMs;

        public Owner Owner;

        /// <summary>Raised by one whenever the host wants the guest to teleport.</summary>
        public long TeleportSeq;

        /// <summary>Host cutscene: the guest parks its player and waits for a teleport.</summary>
        public bool Loading;

        /// <summary>A host menu is open: the guest drops every held key.</summary>
        public bool MenuOpen;

        public int ViewportWidth, ViewportHeight;

        /// <summary>Where the host wants the guest: its character's feet, Minecraft space.</summary>
        public double X, Y, Z;

        public float Yaw, Pitch;

        public string ToJson()
        {
            return JsonLite.WriteObject(
                "t", "hs",
                "seq", Seq,
                "t", TimestampMs,
                "owner", Owner.ToString(),
                "tseq", TeleportSeq,
                "loading", Loading,
                "menu", MenuOpen,
                "vpw", ViewportWidth,
                "vph", ViewportHeight,
                "x", X,
                "y", Y,
                "z", Z,
                "yaw", Yaw,
                "pitch", Pitch);
        }
    }
}

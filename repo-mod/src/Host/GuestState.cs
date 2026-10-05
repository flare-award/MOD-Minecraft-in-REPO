// The guest's (Minecraft's) reported state, one message per tick.
//
// Wire message: {"t":"gs","seq":n,"x":..,"y":..,"z":..,"px":..,"py":..,"pz":..,
//                "tick":ms,"yaw":..,"pitch":..,"eye":..,"fov":..,"cam":0..2,
//                "camDist":..,"hp":..,"hpMax":..,"dead":0/1,"screen":0/1,
//                "ack":n,"mode":0..3,"period":ms,"ms":wallclock}
//
// Positions are the player's FEET in Minecraft block space (Y up). The last two
// tick positions and the tick period are sent so the host can interpolate on its
// own frame clock; using the guest's already-interpolated position judders,
// because the two games' frames are not in phase.

using System.Collections.Generic;
using MinecraftInRepo.Net;

namespace MinecraftInRepo.Host
{
    public struct GuestState
    {
        /// <summary>Running tick counter; a change means a new tick arrived.</summary>
        public long Seq;

        public double X, Y, Z;          // latest tick, feet
        public double PX, PY, PZ;       // previous tick, feet

        /// <summary>Milliseconds the guest spends per tick (50 at 20 tps).</summary>
        public float TickPeriodMs;

        public float Yaw, Pitch;        // degrees, Minecraft conventions
        public float EyeHeight;         // eye above the feet
        public float Fov;
        public int CameraMode;          // 0 first person, 1 third person back, 2 third person front
        public float CameraDistance;    // third person distance, already collision adjusted

        public float Health, MaxHealth;
        public bool Dead;
        public bool ScreenOpen;         // an inventory/chat/options screen covers the HUD

        /// <summary>Last teleport sequence number the guest has performed.</summary>
        public long TeleportAck;

        /// <summary>Minecraft game mode: 0 survival, 1 creative, 2 adventure, 3 spectator.</summary>
        public int GameMode;

        /// <summary>Guest wall clock in milliseconds, for the heartbeat.</summary>
        public long TimestampMs;

        public static bool TryParse(Dictionary<string, object> msg, out GuestState state)
        {
            state = default(GuestState);
            if (msg == null)
            {
                return false;
            }

            state.Seq = (long)JsonLite.GetNumber(msg, "seq");
            state.X = JsonLite.GetNumber(msg, "x");
            state.Y = JsonLite.GetNumber(msg, "y");
            state.Z = JsonLite.GetNumber(msg, "z");
            state.PX = JsonLite.GetNumber(msg, "px", state.X);
            state.PY = JsonLite.GetNumber(msg, "py", state.Y);
            state.PZ = JsonLite.GetNumber(msg, "pz", state.Z);
            state.TickPeriodMs = (float)JsonLite.GetNumber(msg, "period", 50.0);
            if (state.TickPeriodMs <= 0f)
            {
                state.TickPeriodMs = 50f;
            }

            state.Yaw = (float)JsonLite.GetNumber(msg, "yaw");
            state.Pitch = (float)JsonLite.GetNumber(msg, "pitch");
            state.EyeHeight = (float)JsonLite.GetNumber(msg, "eye", 1.62);
            state.Fov = (float)JsonLite.GetNumber(msg, "fov", 70.0);
            state.CameraMode = (int)JsonLite.GetNumber(msg, "cam");
            state.CameraDistance = (float)JsonLite.GetNumber(msg, "camDist", 4.0);

            state.Health = (float)JsonLite.GetNumber(msg, "hp", 20.0);
            state.MaxHealth = (float)JsonLite.GetNumber(msg, "hpMax", 20.0);
            state.Dead = JsonLite.GetBool(msg, "dead");
            state.ScreenOpen = JsonLite.GetBool(msg, "screen");
            state.TeleportAck = (long)JsonLite.GetNumber(msg, "ack");
            state.GameMode = (int)JsonLite.GetNumber(msg, "mode");
            state.TimestampMs = (long)JsonLite.GetNumber(msg, "ms");
            return true;
        }

        /// <summary>
        /// Feet position between the previous and the latest tick. The host calls
        /// this with an alpha computed from its own frame clock, not from the
        /// guest's clock (the two processes do not share one).
        /// </summary>
        public void LerpPosition(double alpha, out double x, out double y, out double z)
        {
            if (alpha <= 0.0)
            {
                x = PX; y = PY; z = PZ;
                return;
            }
            if (alpha >= 1.0)
            {
                x = X; y = Y; z = Z;
                return;
            }
            x = PX + (X - PX) * alpha;
            y = PY + (Y - PY) * alpha;
            z = PZ + (Z - PZ) * alpha;
        }

        public bool IsCreative => GameMode == 1;
    }
}

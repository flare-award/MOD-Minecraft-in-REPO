// Pure helpers for reading the guest's pose and health.
//
// No UnityEngine, no BepInEx: unit tested in tests/HostTests.

using System;

namespace MinecraftInRepo.Host
{
    /// <summary>
    /// Tracks when the latest guest tick arrived on the host's own clock and
    /// turns that into an interpolation alpha. The two processes do not share a
    /// clock, so the guest's own timestamps cannot be used for this.
    /// </summary>
    public struct TickTracker
    {
        public long LastSeq;
        public int ArrivalMs;
        public bool Primed;

        /// <summary>Call whenever a guest state is observed.</summary>
        public void Observe(GuestState state, int nowMs)
        {
            if (!Primed)
            {
                LastSeq = state.Seq;
                ArrivalMs = nowMs;
                Primed = true;
                return;
            }

            if (state.Seq != LastSeq)
            {
                LastSeq = state.Seq;
                ArrivalMs = nowMs;
            }
        }

        /// <summary>0 = at the previous tick, 1 = at the latest tick.</summary>
        public double Alpha(GuestState state, int nowMs)
        {
            if (!Primed)
            {
                return 1.0;
            }
            if (state.Seq != LastSeq)
            {
                // A new tick we have not stamped yet: show the previous pose.
                return 0.0;
            }

            double period = state.TickPeriodMs <= 0f ? 50.0 : state.TickPeriodMs;
            if (period <= 0.0)
            {
                return 1.0;
            }

            double alpha = (nowMs - ArrivalMs) / period;
            if (alpha < 0.0)
            {
                return 0.0;
            }
            return alpha > 1.0 ? 1.0 : alpha;
        }
    }

    /// <summary>Health conversion between the two games.</summary>
    public static class HealthScale
    {
        /// <summary>Minecraft's full health in hearts-points.</summary>
        public const float GuestMaxHealth = 20f;

        /// <summary>R.E.P.O. damage into Minecraft damage.</summary>
        public static float ToGuest(int repoDamage, int repoMaxHealth)
        {
            if (repoDamage <= 0)
            {
                return 0f;
            }
            if (repoMaxHealth <= 0)
            {
                return repoDamage;
            }
            return (float)repoDamage * GuestMaxHealth / repoMaxHealth;
        }

        /// <summary>Minecraft health into R.E.P.O. health.</summary>
        public static int ToHost(float guestHealth, int repoMaxHealth, bool dead)
        {
            if (dead)
            {
                return 0;
            }
            int value = repoMaxHealth <= 0
                ? (int)Math.Round(guestHealth)
                : (int)Math.Round(guestHealth * repoMaxHealth / GuestMaxHealth);
            if (value < 1)
            {
                value = 1;   // never let the host's own death flow start on its own
            }
            if (repoMaxHealth > 0 && value > repoMaxHealth)
            {
                value = repoMaxHealth;
            }
            return value;
        }
    }
}

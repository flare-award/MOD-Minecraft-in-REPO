// The guest's health is the only health.
//
// R.E.P.O.'s own damage still lands on its own character (we do not patch the
// hazard funnel), but every frame we read what it took, forward it to the guest
// as one hurt event, and then write the guest's health back. That way Minecraft's
// armour, absorption and invulnerability decide what actually happens, the
// host's HUD never disagrees, and the host's death flow can never start on its
// own - only the guest's death starts it.

using System;

namespace MinecraftInRepo.Host
{
    public sealed class HealthBridge
    {
        private readonly RepoApi api;
        private readonly GuestLink link;
        private readonly Action<string> log;

        private int mirrorHealth = -1;
        private int lastHurtMs;
        private bool deathHandled;

        public HealthBridge(RepoApi repoApi, GuestLink guestLink, Action<string> log)
        {
            api = repoApi;
            link = guestLink;
            this.log = log ?? delegate { };
        }

        /// <summary>Milliseconds between two hurt events (Minecraft's invulnerability window).</summary>
        public int HurtCooldownMs { get; set; } = 500;

        public void Reset()
        {
            mirrorHealth = -1;
            deathHandled = false;
        }

        public void Update(object playerHealth, object avatar, GuestState guest, int nowMs)
        {
            if (playerHealth == null)
            {
                return;
            }

            int health = api.InstanceValue<int>(playerHealth, "health", -1);
            int maxHealth = api.InstanceValue<int>(playerHealth, "maxHealth", -1);
            if (health < 0 || maxHealth <= 0)
            {
                return;
            }

            if (mirrorHealth < 0)
            {
                mirrorHealth = health;
            }

            // 1. Host hazards -> one hurt event for the guest.
            if (health < mirrorHealth && !guest.IsCreative && !guest.Dead)
            {
                int taken = mirrorHealth - health;
                if (nowMs - lastHurtMs >= HurtCooldownMs)
                {
                    lastHurtMs = nowMs;
                    float amount = HealthScale.ToGuest(taken, maxHealth);
                    if (link != null && amount > 0f)
                    {
                        link.SendHurt(amount, "hazard");
                        log("[MinecraftInRepo] hazard: " + taken + " host health -> " +
                            amount.ToString("0.0") + " to Minecraft.");
                    }
                }
            }

            // 2. The guest's health is written back, so the host can never kill us.
            int target = guest.IsCreative
                ? maxHealth
                : HealthScale.ToHost(guest.Health, maxHealth, guest.Dead);
            if (target != health)
            {
                api.SetInstanceValue(playerHealth, "health", target);
            }
            mirrorHealth = target;

            // 3. A guest death runs the host's own death, once.
            if (guest.Dead && !deathHandled)
            {
                deathHandled = true;
                log("[MinecraftInRepo] the Minecraft player died - running R.E.P.O.'s death.");
                api.Invoke(playerHealth, "Death");
            }
            else if (!guest.Dead && deathHandled)
            {
                deathHandled = false;
                log("[MinecraftInRepo] the Minecraft player respawned - reviving in R.E.P.O.");
                api.Invoke(avatar, "Revive", true);
            }
        }
    }
}

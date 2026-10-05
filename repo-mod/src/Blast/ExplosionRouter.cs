// Turns Minecraft explosions into R.E.P.O. carnage.
//
// When the bridge reports a blast at Minecraft position P with power W:
//   1. P is mapped into R.E.P.O. world space and the blast radius becomes
//      W * RadiusPerPower meters.
//   2. Enemies in radius take EnemyHealth.Hurt damage with falloff and are
//      knocked back / stunned where the game exposes the hooks for it.
//   3. Players in radius take PlayerHealth damage plus a ForceImpulse
//      (friendly fire configurable).
//   4. Items and valuables in radius get a real physics explosion force;
//      strong blasts shatter them through the game's own destruction path.
//
// Damage is host-authoritative (R.E.P.O. syncs health from the master
// client), so on non-host clients only the flash and local knockback remain.

using System;
using System.Collections.Concurrent;
using System.Reflection;
using BepInEx.Logging;
using MinecraftInRepo.Net;
using MinecraftInRepo.Sync;
using UnityEngine;

namespace MinecraftInRepo.Blast
{
    public sealed class ExplosionRouter : MonoBehaviour
    {
        private ModConfig config;
        private CoordinateMap map;
        private PlayModeGate gate;
        private ManualLogSource log;

        private readonly ConcurrentQueue<McExplosion> queue = new ConcurrentQueue<McExplosion>();

        private float lastBoomTime = -100f;
        private bool warnedNotHost;

        public bool Enabled { get; set; }
        public float LastBoomTime => lastBoomTime;

        public void Init(ModConfig modConfig, CoordinateMap coordinateMap, PlayModeGate playModeGate, ManualLogSource logger)
        {
            config = modConfig;
            map = coordinateMap;
            gate = playModeGate;
            log = logger;
            Enabled = config.BlastEnabled.Value;
        }

        /// <summary>Called from the bridge event (main thread) - enqueues for Update.</summary>
        public void Enqueue(McExplosion explosion)
        {
            queue.Enqueue(explosion);
        }

        /// <summary>Called from the plugin's own Update (see MinecraftInRepoPlugin).</summary>
        public void Tick()
        {
            McExplosion explosion;
            int processed = 0;
            while (queue.TryDequeue(out explosion) && processed < 8)
            {
                processed++;
                try
                {
                    ApplyBlast(explosion);
                }
                catch (Exception e)
                {
                    log.LogError("[MinecraftInRepo] Failed applying blast: " + e);
                }
            }
        }

        private void ApplyBlast(McExplosion explosion)
        {
            if (!Enabled || !gate.Allowed)
            {
                // Solo-only guard: in a multiplayer session nothing is touched.
                return;
            }

            lastBoomTime = Time.time;

            Vector3 center = map.McToRepo(new Vector3((float)explosion.X, (float)explosion.Y, (float)explosion.Z));
            float power = Mathf.Max(0.1f, explosion.Power);
            float radius = Mathf.Max(0.5f, power * config.RadiusPerPower.Value);

            bool authority = SemiFunc.IsMasterClientOrSingleplayer();
            if (!authority)
            {
                if (!warnedNotHost)
                {
                    warnedNotHost = true;
                    log.LogWarning("[MinecraftInRepo] Not the host: explosion damage needs the host to run this mod. Flash/knockback only.");
                }
                KnockLocalPlayer(center, radius, power);
                return;
            }

            if (!LevelReady())
            {
                return; // menu / loading: nothing to blow up yet
            }

            int enemies = DamageEnemies(center, radius, power);
            int players = DamagePlayers(center, radius, power);
            int items = FlingItems(center, radius, power);

            log.LogInfo(string.Format(
                "[MinecraftInRepo] BOOM ({0}, power {1:F1}) at R.E.P.O. {2}: {3} enemies, {4} players, {5} items hit.",
                explosion.Source, power, center, enemies, players, items));
        }

        private static bool LevelReady()
        {
            LevelGenerator generator = LevelGenerator.Instance;
            return generator != null && generator.Generated;
        }

        // ------------------------------------------------------------------
        // Enemies
        // ------------------------------------------------------------------

        private int DamageEnemies(Vector3 center, float radius, float power)
        {
            int hit = 0;
            EnemyHealth[] all = UnityEngine.Object.FindObjectsOfType<EnemyHealth>();
            foreach (EnemyHealth health in all)
            {
                if (health == null)
                {
                    continue;
                }
                Vector3 position = health.transform.position;
                float distance = Vector3.Distance(position, center);
                if (distance >= radius)
                {
                    continue;
                }
                float falloff = Mathf.Clamp01(1f - distance / radius);
                Vector3 direction = (position - center).normalized;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector3.up;
                }

                int damage = Mathf.RoundToInt(config.MaxEnemyDamage.Value * falloff);
                if (damage > 0)
                {
                    health.Hurt(damage, direction);
                    hit++;
                }

                TryEnemyKnockback(health, direction * (config.EnemyKnockback.Value * falloff * (0.5f + power * 0.25f)));

                if (config.StunEnemies.Value && falloff > 0.25f)
                {
                    TryEnemyStun(health, 0.6f + falloff);
                }
            }
            return hit;
        }

        /// <summary>Enemies with an EnemyRigidbody expose a private 'rb' Rigidbody - push it.</summary>
        private static void TryEnemyKnockback(EnemyHealth health, Vector3 impulse)
        {
            try
            {
                EnemyRigidbody enemyBody = health.GetComponentInChildren<EnemyRigidbody>();
                if (enemyBody == null)
                {
                    return;
                }
                FieldInfo rbField = typeof(EnemyRigidbody).GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Rigidbody rb = rbField != null ? rbField.GetValue(enemyBody) as Rigidbody : null;
                if (rb == null)
                {
                    rb = enemyBody.GetComponent<Rigidbody>();
                }
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForce(impulse, ForceMode.Impulse);
                }
            }
            catch
            {
                // knockback is optional flavour; never let it break the damage path
            }
        }

        private static void TryEnemyStun(EnemyHealth health, float duration)
        {
            try
            {
                EnemyStateStunned stunned = health.GetComponentInChildren<EnemyStateStunned>();
                if (stunned == null)
                {
                    return;
                }
                MethodInfo set = typeof(EnemyStateStunned).GetMethod("Set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
                if (set != null)
                {
                    set.Invoke(stunned, new object[] { duration });
                }
            }
            catch
            {
                // stunning is optional
            }
        }

        // ------------------------------------------------------------------
        // Players
        // ------------------------------------------------------------------

        private int DamagePlayers(Vector3 center, float radius, float power)
        {
            int hit = 0;
            PlayerAvatar local = SemiFunc.PlayerGetLocal();
            PlayerAvatar[] all = UnityEngine.Object.FindObjectsOfType<PlayerAvatar>();
            foreach (PlayerAvatar avatar in all)
            {
                if (avatar == null || avatar.playerHealth == null)
                {
                    continue;
                }
                if (!IsAlive(avatar.playerHealth))
                {
                    continue;
                }

                Vector3 position = avatar.transform.position;
                float distance = Vector3.Distance(position, center);
                if (distance >= radius)
                {
                    continue;
                }
                float falloff = Mathf.Clamp01(1f - distance / radius);
                Vector3 direction = (position - center).normalized;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector3.up;
                }

                bool self = avatar == local;
                int damage = Mathf.RoundToInt(config.MaxPlayerDamage.Value * falloff);
                if (damage > 0 && (config.FriendlyFire.Value || !self))
                {
                    try
                    {
                        // Same call the game's own host tooling uses to damage any avatar.
                        avatar.playerHealth.HurtOther(damage, Vector3.zero, false, -1, false);
                    }
                    catch
                    {
                        if (self)
                        {
                            try { avatar.playerHealth.Hurt(damage, false); } catch { /* give up */ }
                        }
                    }
                    hit++;
                }

                try
                {
                    // Always fling, even when friendly fire is off - TNT throws people.
                    avatar.ForceImpulse((direction + Vector3.up * 0.35f) * (config.PlayerKnockback.Value * falloff * (0.5f + power * 0.25f)));
                }
                catch
                {
                    // impulse is optional
                }
            }
            return hit;
        }

        private static readonly FieldInfo PlayerHealthField =
            typeof(PlayerHealth).GetField("health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static bool IsAlive(PlayerHealth health)
        {
            try
            {
                if (PlayerHealthField != null)
                {
                    return (int)PlayerHealthField.GetValue(health) > 0;
                }
            }
            catch
            {
                // fall through: assume alive
            }
            return true;
        }

        /// <summary>Non-host fallback: at least throw ourselves around for the feel of it.</summary>
        private void KnockLocalPlayer(Vector3 center, float radius, float power)
        {
            try
            {
                PlayerAvatar local = SemiFunc.PlayerGetLocal();
                if (local == null)
                {
                    return;
                }
                float distance = Vector3.Distance(local.transform.position, center);
                if (distance >= radius)
                {
                    return;
                }
                float falloff = Mathf.Clamp01(1f - distance / radius);
                Vector3 direction = (local.transform.position - center).normalized;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector3.up;
                }
                local.ForceImpulse((direction + Vector3.up * 0.35f) * (config.PlayerKnockback.Value * falloff));
            }
            catch
            {
                // cosmetic only
            }
        }

        // ------------------------------------------------------------------
        // Items & valuables
        // ------------------------------------------------------------------

        private int FlingItems(Vector3 center, float radius, float power)
        {
            int hit = 0;
            PhysGrabObject[] all = UnityEngine.Object.FindObjectsOfType<PhysGrabObject>();
            foreach (PhysGrabObject item in all)
            {
                if (item == null)
                {
                    continue;
                }
                GameObject go = item.gameObject;
                if (go == null || !go.activeSelf)
                {
                    continue;
                }
                float distance = Vector3.Distance(go.transform.position, center);
                if (distance >= radius)
                {
                    continue;
                }
                float falloff = Mathf.Clamp01(1f - distance / radius);

                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(config.ItemForce.Value * power, center, radius, 1f, ForceMode.Impulse);
                }
                hit++;

                if (config.DestroyValuables.Value && falloff >= Mathf.Clamp01(config.DestroyThreshold.Value))
                {
                    TryShatter(go);
                }
            }
            return hit;
        }

        /// <summary>Uses the game's own impact-destruction path so destruction stays
        /// network-correct (this is how valuables normally break on hard impacts).</summary>
        private void TryShatter(GameObject go)
        {
            try
            {
                PhysGrabObjectImpactDetector detector = go.GetComponent<PhysGrabObjectImpactDetector>();
                if (detector == null)
                {
                    return;
                }
                detector.destroyDisable = false;
                detector.DestroyObject(true);
            }
            catch
            {
                // shattering is best-effort; the item survives the blast
            }
        }
    }
}

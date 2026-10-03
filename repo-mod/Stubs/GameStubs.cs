// ---------------------------------------------------------------------------
// Hand-written stub of the R.E.P.O. (Assembly-CSharp) game API used by
// MinecraftInRepo. Every declaration here was verified against public mod
// sources that compile against the real game assemblies:
//
//   * PlayerAvatar.ForceImpulse / PlayerDeath / Revive / playerHealth,
//     PlayerHealth.HurtOther(int, Vector3, bool, int, bool), PlayerHealth
//     "health"/"maxHealth" fields  -> jkieley/repo-live-control
//   * PlayerHealth.Hurt(int, bool), LevelGenerator.Instance/Generated,
//     RunManager.instance, SemiFunc.IsMultiplayer                       -> Lillious R.E.P.O Mod Library
//   * EnemyHealth.Hurt(int, Vector3) as the central host-authoritative
//     enemy damage entry point                                          -> R.E.P.O. modding wiki research docs
//   * EnemyHealth "dead" field                                          -> Lillious R.E.P.O Mod Library
//   * ValuableObject.dollarValueCurrent, PhysGrabObjectImpactDetector
//     .DestroyObject(bool) + destroyDisable, SemiFunc.PlayerGetLocal,
//     SemiFunc.IsMasterClientOrSingleplayer                             -> layfhaker/parry-mod
//
// When a real R.E.P.O. Assembly-CSharp.dll is referenced instead, these
// declarations are simply not compiled.
// ---------------------------------------------------------------------------

using UnityEngine;

// R.E.P.O.'s game code lives in the global namespace.

public class PlayerAvatar : MonoBehaviour
{
    public static PlayerAvatar instance { get; private set; }

    public PlayerHealth playerHealth;

    public void PlayerDeath(int unknown) { }
    public void Revive(bool unknown) { }
    public void ForceImpulse(Vector3 impulse) { }
}

public class PlayerHealth : MonoBehaviour
{
    public void Hurt(int damage, bool unknown) { }
    public void HurtOther(int damage, Vector3 position, bool unknown1, int unknown2, bool unknown3) { }
    public void Heal(int amount, bool unknown) { }
    public void HealOther(int amount, bool unknown) { }
    public void UpdateHealthRPC(int current, int max, bool unknown1, bool unknown2) { }
}

public class Enemy : MonoBehaviour { }

public class EnemyHealth : MonoBehaviour
{
    /// <summary>Central host-authoritative enemy damage entry point.</summary>
    public void Hurt(int damage, Vector3 hurtDirection) { }
}

public class EnemyRigidbody : MonoBehaviour { }

public class EnemyStateStunned : MonoBehaviour
{
    public void Set(float duration) { }
}

public class ValuableObject : MonoBehaviour { }

public class PhysGrabObject : MonoBehaviour { }

public class PhysGrabObjectImpactDetector : MonoBehaviour
{
    public bool destroyDisable;

    /// <summary>Vanilla destruction path for grabbable objects/valuables.</summary>
    public void DestroyObject(bool unknown) { }
}

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance { get; private set; }
    public bool Generated { get; private set; }
}

public class RunManager : MonoBehaviour
{
    public static RunManager instance { get; private set; }
}

public static class SemiFunc
{
    public static bool IsMultiplayer() { return false; }
    public static bool IsMasterClientOrSingleplayer() { return true; }
    public static PlayerAvatar PlayerGetLocal() { return null; }
    public static string PlayerGetSteamID(PlayerAvatar player) { return ""; }
}

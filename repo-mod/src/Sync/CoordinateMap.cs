// Maps positions and directions between the R.E.P.O. world (Unity, meters,
// Y-up, left-handed) and the Minecraft world (blocks, Y-up).
//
// Both games use Y-up world coordinates, so the mapping is a yaw rotation
// around Y plus a translation and a uniform scale:
//
//     repo  = AnchorRepo + RotY(AlignmentYaw) * (mc    - OriginMc) * Scale
//     mc    = OriginMc   + RotY(-AlignmentYaw) * (repo - AnchorRepo) / Scale
//
// F7 calibration fills OriginMc / AnchorRepo / AlignmentYaw from the two
// cameras' current poses.

using UnityEngine;

namespace MinecraftInRepo.Sync
{
    public sealed class CoordinateMap
    {
        private readonly ModConfig config;

        public CoordinateMap(ModConfig modConfig)
        {
            config = modConfig;
        }

        public Vector3 McToRepo(Vector3 mc)
        {
            Vector3 d = mc - config.OriginMc;
            return config.AnchorRepo + RotateY(config.AlignmentYawDeg.Value, d) * config.Scale.Value;
        }

        public Vector3 RepoToMc(Vector3 repo)
        {
            Vector3 d = repo - config.AnchorRepo;
            return config.OriginMc + RotateY(-config.AlignmentYawDeg.Value, d) / config.Scale.Value;
        }

        /// <summary>Maps a direction (no translation, no scale) from R.E.P.O. space to Minecraft space.</summary>
        public Vector3 RepoDirectionToMc(Vector3 dir)
        {
            return RotateY(-config.AlignmentYawDeg.Value, dir);
        }

        /// <summary>Unity yaw (degrees, 0 = +Z, positive turns +Z towards +X) of a direction vector.</summary>
        public static float UnityYaw(Vector3 dir)
        {
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>Rotation around the Y axis by the given angle in Unity's convention.</summary>
        public static Vector3 RotateY(float degrees, Vector3 v)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector3(
                v.x * cos + v.z * sin,
                v.y,
                -v.x * sin + v.z * cos);
        }
    }
}

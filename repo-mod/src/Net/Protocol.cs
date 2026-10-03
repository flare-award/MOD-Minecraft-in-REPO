// Shared protocol types for the R.E.P.O. <-> Minecraft bridge.
// Wire format: newline-delimited UTF-8 JSON over TCP loopback. See docs/ARCHITECTURE.md.

namespace MinecraftInRepo.Net
{
    /// <summary>A camera pose (Minecraft world space, degrees).</summary>
    public struct McPose
    {
        public double X;
        public double Y;
        public double Z;
        public float Yaw;
        public float Pitch;
        public float Fov;
    }

    /// <summary>An explosion reported by Minecraft.</summary>
    public struct McExplosion
    {
        public double X;
        public double Y;
        public double Z;
        public float Power;
        public bool Fire;
        public string Source;
    }
}

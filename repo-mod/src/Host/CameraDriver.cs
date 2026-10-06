// Writes the guest's eye into the host's camera.
//
// The plugin's pump runs on Application.onBeforeRender, which is after every
// Update and LateUpdate, so this always overwrites the host's own camera result
// - which is what makes the host's interaction ray agree with what the player
// sees.

using System;
using MinecraftInRepo.Sync;
using UnityEngine;

namespace MinecraftInRepo.Host
{
    public sealed class CameraDriver
    {
        private readonly ModConfig config;
        private readonly CoordinateMap map;
        private readonly Action<string> log;

        public CameraDriver(ModConfig modConfig, CoordinateMap coordinateMap, Action<string> log)
        {
            config = modConfig;
            map = coordinateMap;
            this.log = log ?? delegate { };
        }

        /// <summary>Logged once: how far the eye we computed is from the guest's own report.</summary>
        public float LastEyeError { get; private set; }

        public void Update(GuestState guest, double alpha)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            double feetX, feetY, feetZ;
            guest.LerpPosition(alpha, out feetX, out feetY, out feetZ);

            Vector3 eyeMc = new Vector3((float)feetX, (float)feetY + guest.EyeHeight, (float)feetZ);
            Vector3 eyeRepo = map.McToRepo(eyeMc);

            Vector3 forwardMc = ForwardFromYawPitch(guest.Yaw, guest.Pitch);
            Vector3 forwardRepo = CoordinateMap.RotateY(config.AlignmentYawDeg.Value, forwardMc);
            float unityYaw = CoordinateMap.UnityYaw(forwardRepo);

            // Unity's euler X is positive downwards, the same sign as Minecraft's pitch.
            camera.transform.SetPositionAndRotation(eyeRepo, Quaternion.Euler(guest.Pitch, unityYaw, 0f));

            if (guest.Fov > 1f && camera.fieldOfView > 1f)
            {
                camera.fieldOfView = guest.Fov;
            }

            LastEyeError = (eyeRepo - eyeMcToRepo(guest)).magnitude;
        }

        private Vector3 eyeMcToRepo(GuestState guest)
        {
            return map.McToRepo(new Vector3(guest.X, guest.Y + guest.EyeHeight, guest.Z));
        }

        /// <summary>Minecraft's conventions: yaw 0 looks along +Z, pitch positive is downwards.</summary>
        public static Vector3 ForwardFromYawPitch(float yawDegrees, float pitchDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            double pitch = pitchDegrees * Math.PI / 180.0;
            double horizontal = Math.Cos(pitch);
            // yaw 0 -> +Z, yaw 90 -> -X (Minecraft's handedness)
            float x = (float)(-Math.Sin(yaw) * horizontal);
            float y = (float)-Math.Sin(pitch);
            float z = (float)(Math.Cos(yaw) * horizontal);
            return new Vector3(x, y, z);
        }
    }
}

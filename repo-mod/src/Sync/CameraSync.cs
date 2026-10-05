// Sends the R.E.P.O. main-camera pose to Minecraft so the Minecraft camera
// follows it, and performs the F7 one-press calibration that aligns the two
// worlds.

using BepInEx.Logging;
using MinecraftInRepo.Net;
using UnityEngine;

namespace MinecraftInRepo.Sync
{
    public sealed class CameraSync : MonoBehaviour
    {
        private ModConfig config;
        private CoordinateMap map;
        private BridgeClient bridge;
        private PlayModeGate gate;
        private ManualLogSource log;

        private float lastSent = -1f;
        private bool calibrationRequested;

        public bool FollowEnabled { get; set; }

        public void Init(ModConfig modConfig, CoordinateMap coordinateMap, BridgeClient bridgeClient,
            PlayModeGate playModeGate, ManualLogSource logger)
        {
            config = modConfig;
            map = coordinateMap;
            bridge = bridgeClient;
            gate = playModeGate;
            log = logger;
            FollowEnabled = config.FollowEnabled.Value;
        }

        /// <summary>Called from the plugin's own Update (see MinecraftInRepoPlugin).</summary>
        public void Tick()
        {
            if (!FollowEnabled || !bridge.Connected || !gate.Allowed)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            float minInterval = 1f / Mathf.Max(1, config.SendRateHz.Value);
            if (Time.time - lastSent < minInterval)
            {
                return;
            }
            lastSent = Time.time;

            // Convert the Unity forward vector into the Minecraft look direction,
            // then into Minecraft yaw/pitch:
            //   look.x = -sin(yaw)cos(pitch), look.y = -sin(pitch), look.z = cos(yaw)cos(pitch)
            Vector3 look = map.RepoDirectionToMc(cam.transform.forward).normalized;
            float pitch = Mathf.Asin(Mathf.Clamp(-look.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(-look.x, look.z) * Mathf.Rad2Deg;

            Vector3 mcPos = map.RepoToMc(cam.transform.position);
            bridge.SendCam(mcPos.x, mcPos.y, mcPos.z, yaw, pitch, cam.fieldOfView);
        }

        /// <summary>F7: ask Minecraft where it is, then align the two worlds there.</summary>
        public void RequestCalibration()
        {
            if (!bridge.Connected)
            {
                log.LogWarning("[MinecraftInRepo] Cannot calibrate: Minecraft bridge is not connected.");
                return;
            }
            if (Camera.main == null)
            {
                log.LogWarning("[MinecraftInRepo] Cannot calibrate: no main camera.");
                return;
            }
            calibrationRequested = true;
            bridge.SendGetPos();
        }

        public void OnMcPose(McPose pose)
        {
            if (!calibrationRequested)
            {
                return;
            }
            calibrationRequested = false;
            Calibrate(pose);
        }

        private void Calibrate(McPose mc)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            // The R.E.P.O. point that should sit exactly on the Minecraft camera.
            // Prefer the local avatar (eye-level-ish) over the raw camera.
            PlayerAvatar avatar = SemiFunc.PlayerGetLocal();
            Vector3 repoPoint = avatar != null ? avatar.transform.position : cam.transform.position;

            // Horizontal look direction of the Minecraft camera, from its yaw.
            float rad = mc.Yaw * Mathf.Deg2Rad;
            Vector3 mcLook = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            Vector3 repoLook = cam.transform.forward;
            repoLook.y = 0f;
            if (repoLook.sqrMagnitude < 0.01f)
            {
                repoLook = Vector3.forward;
            }

            // repo = Anchor + RotY(phi) * (mc - Origin)  =>  pick phi so the
            // current Minecraft look direction maps onto the R.E.P.O. camera forward.
            float phi = CoordinateMap.UnityYaw(repoLook) - CoordinateMap.UnityYaw(mcLook);
            phi = phi - Mathf.Round(phi / 360f) * 360f;

            config.AlignmentYawDeg.Value = phi;
            config.McOriginX.Value = (float)mc.X;
            config.McOriginY.Value = (float)mc.Y;
            config.McOriginZ.Value = (float)mc.Z;
            config.RepoAnchorX.Value = repoPoint.x;
            config.RepoAnchorY.Value = repoPoint.y;
            config.RepoAnchorZ.Value = repoPoint.z;

            log.LogInfo(string.Format(
                "[MinecraftInRepo] Calibrated: MC {0:F1},{1:F1},{2:F1} (yaw {3:F1}) <-> R.E.P.O. {4} (yaw {5:F1}), alignment {6:F1} deg",
                mc.X, mc.Y, mc.Z, mc.Yaw, repoPoint, CoordinateMap.UnityYaw(cam.transform.forward), phi));
        }
    }
}

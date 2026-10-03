using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace RepoMinecraftBridge
{
    // Local, opt-in proof of concept. No game assets or ownership checks are modified.
    [BepInPlugin("dev.repo.minecraft.bridge", "Minecraft Portal Bridge", "0.1.0")]
    public sealed class BridgePlugin : BaseUnityPlugin
    {
        private ConfigEntry<int> port;
        private ConfigEntry<float> distance;
        private ConfigEntry<float> radius;
        private ConfigEntry<float> impulse;
        private UdpClient listener;
        private GameObject portal;
        private Camera trackedCamera;

        private void Awake()
        {
            port = Config.Bind("Bridge", "Port", 24865, "Loopback TNT event port (single-player only).");
            distance = Config.Bind("Portal", "Distance", 3f, "Distance from REPO camera in metres.");
            radius = Config.Bind("TNT", "Radius", 5f, "REPO physics blast radius in metres.");
            impulse = Config.Bind("TNT", "Impulse", 12f, "Physics impulse on nearby rigidbodies.");
            try
            {
                listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, port.Value));
                listener.Client.Blocking = false;
            }
            catch (SocketException ex) { Logger.LogError("Cannot bind local TNT events: " + ex.Message); }
        }

        private void Update()
        {
            if (trackedCamera != Camera.main)
            {
                trackedCamera = Camera.main;
                if (portal != null) Destroy(portal);
                portal = null;
            }
            if (trackedCamera != null)
            {
                if (portal == null) CreatePortal();
                portal.transform.position = trackedCamera.transform.position + trackedCamera.transform.forward * distance.Value;
                portal.transform.rotation = trackedCamera.transform.rotation;
            }
            if (listener == null) return;
            // Read on Unity's main thread: never mutate physics from a UDP callback.
            for (int n = 0; n < 32 && listener.Available > 0; n++)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                string packet = Encoding.UTF8.GetString(listener.Receive(ref sender));
                if (!IPAddress.IsLoopback(sender.Address)) continue;
                // TNT event is intentionally a fixed literal; never execute untrusted network input.
                if (packet == "TNT") Blast();
            }
        }

        private void CreatePortal()
        {
            portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.name = "Minecraft portal placeholder (capture not connected)";
            portal.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
            var collider = portal.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = portal.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = new Color(0.12f, 0.4f, 0.15f);
        }

        private void Blast()
        {
            if (portal == null) return;
            Vector3 origin = portal.transform.position;
            foreach (var body in Physics.OverlapSphere(origin, Mathf.Max(0, radius.Value)))
            {
                if (body == null || body.gameObject == portal) continue;
                var rb = body.attachedRigidbody;
                if (rb != null) rb.AddExplosionForce(Mathf.Max(0, impulse.Value), origin, Mathf.Max(0.1f, radius.Value), 0.25f, ForceMode.Impulse);
            }
            Logger.LogInfo("Received local TNT event; applied physics impulse (not game damage).");
        }

        private void OnDestroy()
        {
            if (listener != null) listener.Close();
            if (portal != null) Destroy(portal);
        }
    }
}

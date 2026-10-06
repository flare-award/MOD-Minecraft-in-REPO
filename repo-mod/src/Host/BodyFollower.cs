// The host's character while the guest owns the player: hidden, kinematic and
// moved to the guest's position every frame, so the host's triggers, hazards,
// checkpoints and scripts keep working without a single change.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MinecraftInRepo.Host
{
    public sealed class BodyFollower
    {
        private readonly RepoApi api;
        private readonly Action<string> log;
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        public bool Following { get; private set; }

        public BodyFollower(RepoApi repoApi, Action<string> log)
        {
            api = repoApi;
            this.log = log ?? delegate { };
        }

        /// <summary>Take the body: freeze its physics, hide it, and stop its fall logic.</summary>
        public void Grab(object controller, object avatarVisuals, object avatar)
        {
            if (Following)
            {
                return;
            }
            Following = true;
            hiddenRenderers.Clear();

            if (controller != null)
            {
                api.Invoke(controller, "Kinematic", true);
                api.SetInstanceValue(controller, "CollisionGrounded", true);
            }

            bool hid = false;
            if (avatarVisuals != null)
            {
                hid = api.SetInstanceValue(avatarVisuals, "localVisibility", false)
                      && api.Invoke(avatarVisuals, "ApplyLocalVisibilityBody") != null;
            }
            if (!hid && avatar is Component)
            {
                // Fallback: hide every renderer under the avatar, remembering which
                // ones we touched so releasing restores exactly those.
                Renderer[] renderers = ((Component)avatar).GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }
                    renderer.enabled = false;
                    hiddenRenderers.Add(renderer);
                }
                hid = hiddenRenderers.Count > 0;
            }

            log("[MinecraftInRepo] follower: body taken (kinematic, renderers hidden=" + hid + ").");
        }

        /// <summary>Give the body back with its physics, renderers and timers restored.</summary>
        public void Release(object controller, object avatarVisuals)
        {
            if (!Following)
            {
                return;
            }
            Following = false;

            if (controller != null)
            {
                api.Invoke(controller, "Kinematic", false);
                object rb = api.InstanceValue(controller, "rb");
                if (rb != null)
                {
                    api.SetInstanceValue(rb, "velocity", new Vector3(0f, 0f, 0f));
                }
            }

            if (avatarVisuals != null)
            {
                api.SetInstanceValue(avatarVisuals, "localVisibility", true);
                api.Invoke(avatarVisuals, "ApplyLocalVisibilityBody");
            }

            foreach (Renderer renderer in hiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            hiddenRenderers.Clear();

            log("[MinecraftInRepo] follower: body returned to R.E.P.O.");
        }

        /// <summary>Move the hidden body to the guest's feet (R.E.P.O. space).</summary>
        public void Update(object avatar, object controller, Vector3 feet)
        {
            if (!Following || !(avatar is Component))
            {
                return;
            }

            Transform transform = ((Component)avatar).transform;
            transform.position = feet;

            if (controller != null)
            {
                // Write the state the host's skipped movement code would have written,
                // so nothing downstream starts a fall or a pass-out.
                api.SetInstanceValue(controller, "CollisionGrounded", true);
                object rb = api.InstanceValue(controller, "rb");
                if (rb != null)
                {
                    api.SetInstanceValue(rb, "velocity", new Vector3(0f, 0f, 0f));
                }
            }
        }
    }
}

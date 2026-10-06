// Turns the host's own gameplay controls off and drives its one reserved key.
//
// The host samples its input in Update and applies it in FixedUpdate / Update.
// Zeroing the movement fields every frame (from the render pump, which runs
// after all of that) is the smallest patch that stops walk, sprint, jump, crouch
// and item use without touching any of their consumers. The host's own targeting
// still runs, so the reserved interact key works for free.

using System;
using UnityEngine;

namespace MinecraftInRepo.Host
{
    public sealed class InputGate
    {
        private readonly RepoApi api;
        private readonly Action<string> log;

        public InputGate(RepoApi repoApi, Action<string> log)
        {
            api = repoApi;
            this.log = log ?? delegate { };
        }

        /// <summary>Set from the reserved host interact key (G).</summary>
        public bool InteractHeld { get; set; }

        private bool loggedZeroing;

        public void Update(object controller, object toolController)
        {
            if (controller != null)
            {
                api.SetInstanceValue(controller, "InputDirection", Vector3.zero);
                api.SetInstanceValue(controller, "InputDirectionRaw", Vector3.zero);
                api.SetInstanceValue(controller, "sprinting", false);
                api.SetInstanceValue(controller, "Crouching", false);
                api.SetInstanceValue(controller, "Crawling", false);
                api.SetInstanceValue(controller, "Sliding", false);
                api.SetInstanceValue(controller, "moving", false);
                api.SetInstanceValue(controller, "JumpInputBuffer", 0f);

                if (!loggedZeroing)
                {
                    loggedZeroing = true;
                    bool ok = api.InstanceValue(controller, "InputDirection") != null;
                    log("[MinecraftInRepo] input gate: host movement fields zeroed (found=" + ok + ").");
                }
            }

            if (toolController != null)
            {
                // Written after the host's own Update read it, so it lands next frame.
                api.SetInstanceValue(toolController, "InteractInput", InteractHeld);
            }
        }

        /// <summary>Called when the guest takes the body back: let go of everything.</summary>
        public void Clear(object toolController)
        {
            InteractHeld = false;
            if (toolController != null)
            {
                api.SetInstanceValue(toolController, "InteractInput", false);
            }
        }
    }
}

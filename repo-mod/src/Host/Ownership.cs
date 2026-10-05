// Who owns the player this frame.
//
// Ported from the PeakCraft/SkyCraft design (docs/PORTING-GUIDE.md, "Who owns
// the player"): one decision per frame, in one function, and every other module
// asks a question of the result instead of looking at the host's own flags.
//
//   HostOwns  - no link, no character, or the guest is gone. The host runs
//               exactly as if the plugin were not installed.
//   Handoff   - the guest has been asked to teleport to the host's character
//               and has not confirmed yet. The host keeps its camera and its
//               visible character, with movement frozen.
//   GuestOwns - normal play: Minecraft owns the body, the host's character is a
//               hidden follower.
//   HostMenu  - a host window is open over a guest-owned body.
//   Cutscene  - the host itself is moving or showing the character: loading,
//               scripted falls, death, endings, any camera override.
//
// Deliberately free of UnityEngine and BepInEx types so it can be unit tested
// without the game (tests/HostTests).

namespace MinecraftInRepo.Host
{
    public enum Owner
    {
        HostOwns = 0,
        Handoff = 1,
        GuestOwns = 2,
        HostMenu = 3,
        Cutscene = 4
    }

    /// <summary>Everything the decision needs, gathered by the plugin once per frame.</summary>
    public struct OwnershipInputs
    {
        /// <summary>The link is up and the guest's heartbeat is fresh.</summary>
        public bool LinkUp;

        /// <summary>The host has a local character to own or to follow with.</summary>
        public bool HasCharacter;

        /// <summary>The host is moving or showing the character itself (see the Cutscene list).</summary>
        public bool Cutscene;

        /// <summary>A host window (truck console, shop, pause menu) is open.</summary>
        public bool HostMenuOpen;

        /// <summary>A teleport has been published and the guest has not acked it.</summary>
        public bool TeleportPending;

        /// <summary>The guest player is dead and waiting behind its death screen.</summary>
        public bool GuestDead;
    }

    public sealed class Ownership
    {
        private bool decided;

        public Owner Current { get; private set; }
        public Owner Previous { get; private set; }

        /// <summary>True on the frame the state changed - the moment to release or take things back.</summary>
        public bool Changed { get; private set; }

        /// <summary>Why the current state was chosen; goes into the one-line-per-second log.</summary>
        public string Reason { get; private set; }

        public Ownership()
        {
            Current = Owner.HostOwns;
            Previous = Owner.HostOwns;
            Reason = "startup";
        }

        /// <summary>Call exactly once per frame, before anything asks a question.</summary>
        public Owner Decide(OwnershipInputs input)
        {
            if (decided && SameAs(input))
            {
                // Idempotent within a frame: several callers may ask.
                return Current;
            }

            Owner next;
            string reason;
            if (!input.LinkUp) { next = Owner.HostOwns; reason = "no link"; }
            else if (!input.HasCharacter) { next = Owner.HostOwns; reason = "no character"; }
            else if (input.Cutscene) { next = Owner.Cutscene; reason = "host cutscene"; }
            else if (input.GuestDead) { next = Owner.Cutscene; reason = "guest dead"; }
            else if (input.HostMenuOpen) { next = Owner.HostMenu; reason = "host menu"; }
            else if (input.TeleportPending) { next = Owner.Handoff; reason = "teleport pending"; }
            else { next = Owner.GuestOwns; reason = "playing"; }

            Previous = Current;
            Changed = next != Current;
            Current = next;
            Reason = reason;
            decided = true;
            lastInputs = input;
            return Current;
        }

        /// <summary>Call at the start of the next frame before Decide().</summary>
        public void BeginFrame()
        {
            decided = false;
        }

        private OwnershipInputs lastInputs;

        private bool SameAs(OwnershipInputs input)
        {
            return input.LinkUp == lastInputs.LinkUp
                && input.HasCharacter == lastInputs.HasCharacter
                && input.Cutscene == lastInputs.Cutscene
                && input.HostMenuOpen == lastInputs.HostMenuOpen
                && input.TeleportPending == lastInputs.TeleportPending
                && input.GuestDead == lastInputs.GuestDead;
        }

        // --- The questions modules are allowed to ask -------------------------

        /// <summary>The host's character may run its own movement code.</summary>
        public bool HostMovementEnabled => Current == Owner.HostOwns;

        /// <summary>The host reads its own gameplay keys (interact key excepted).</summary>
        public bool HostInputEnabled => Current == Owner.HostOwns || Current == Owner.HostMenu;

        /// <summary>The host places its camera itself instead of taking the guest's eye.</summary>
        public bool HostCameraActive => Current == Owner.HostOwns || Current == Owner.Handoff || Current == Owner.Cutscene;

        /// <summary>The host's own HUD is drawn.</summary>
        public bool HostHudVisible => Current != Owner.GuestOwns;

        /// <summary>The host's character is rendered (and must be, in handoff and cutscenes).</summary>
        public bool HostCharacterVisible => Current == Owner.HostOwns || Current == Owner.Handoff || Current == Owner.Cutscene;

        /// <summary>The host's character follows the guest player as a hidden follower.</summary>
        public bool BodyFollows => Current == Owner.GuestOwns || Current == Owner.HostMenu;

        /// <summary>The guest's HUD and screens are drawn over the host's picture.</summary>
        public bool GuestOverlayVisible => Current == Owner.GuestOwns || Current == Owner.HostMenu || Current == Owner.Cutscene;

        /// <summary>The guest is told the host is in a cutscene: it parks its player and waits for a teleport.</summary>
        public bool SendLoadingFlag => Current == Owner.Cutscene;

        /// <summary>The guest is told a host menu is open: it drops every held key.</summary>
        public bool SendMenuOpenFlag => Current == Owner.HostMenu;

        /// <summary>Everything held in the guest must be released (a menu, a cutscene, focus loss, link drop).</summary>
        public bool ReleaseGuestKeys => Current != Owner.GuestOwns;

        /// <summary>Collision export should centre on the host's character, not on the guest.</summary>
        public bool ExportAtHostCharacter => Current != Owner.GuestOwns && Current != Owner.HostMenu;
    }
}

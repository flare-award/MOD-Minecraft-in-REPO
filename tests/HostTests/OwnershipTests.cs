using MinecraftInRepo.Host;
using Xunit;

namespace MinecraftInRepo.Tests
{
    public class OwnershipTests
    {
        private static OwnershipInputs Linked()
        {
            return new OwnershipInputs { LinkUp = true, HasCharacter = true };
        }

        private static Owner Decide(Ownership ownership, OwnershipInputs input)
        {
            ownership.BeginFrame();
            return ownership.Decide(input);
        }

        [Fact]
        public void NoLinkMeansTheHostOwnsEverything()
        {
            Ownership ownership = new Ownership();
            Assert.Equal(Owner.HostOwns, Decide(ownership, new OwnershipInputs { HasCharacter = true }));
            Assert.True(ownership.HostMovementEnabled);
            Assert.True(ownership.HostInputEnabled);
            Assert.True(ownership.HostCameraActive);
            Assert.True(ownership.HostHudVisible);
            Assert.True(ownership.HostCharacterVisible);
            Assert.False(ownership.BodyFollows);
            Assert.False(ownership.GuestOverlayVisible);
        }

        [Fact]
        public void NoCharacterMeansTheHostOwnsEverything()
        {
            Ownership ownership = new Ownership();
            Assert.Equal(Owner.HostOwns, Decide(ownership, new OwnershipInputs { LinkUp = true }));
        }

        [Fact]
        public void LinkedAndPlayableMeansTheGuestOwns()
        {
            Ownership ownership = new Ownership();
            Assert.Equal(Owner.GuestOwns, Decide(ownership, Linked()));
            Assert.True(ownership.BodyFollows);
            Assert.True(ownership.GuestOverlayVisible);
            Assert.False(ownership.HostMovementEnabled);
            Assert.False(ownership.HostInputEnabled);
            Assert.False(ownership.HostCameraActive);
            Assert.False(ownership.HostHudVisible);
            Assert.False(ownership.HostCharacterVisible);
            Assert.False(ownership.ReleaseGuestKeys);
        }

        [Fact]
        public void TeleportPendingIsHandoffAndKeepsTheCharacter()
        {
            Ownership ownership = new Ownership();
            OwnershipInputs input = Linked();
            input.TeleportPending = true;
            Assert.Equal(Owner.Handoff, Decide(ownership, input));
            Assert.True(ownership.HostCameraActive);
            Assert.True(ownership.HostCharacterVisible);
            Assert.False(ownership.HostMovementEnabled);
            Assert.False(ownership.BodyFollows);
        }

        [Fact]
        public void HostMenuKeepsTheBodyButReturnsInput()
        {
            Ownership ownership = new Ownership();
            OwnershipInputs input = Linked();
            input.HostMenuOpen = true;
            Assert.Equal(Owner.HostMenu, Decide(ownership, input));
            Assert.True(ownership.BodyFollows);
            Assert.True(ownership.HostInputEnabled);
            Assert.True(ownership.SendMenuOpenFlag);
            Assert.True(ownership.ReleaseGuestKeys);
        }

        [Fact]
        public void CutsceneAndDeathGiveTheCharacterBack()
        {
            Ownership ownership = new Ownership();
            OwnershipInputs cutscene = Linked();
            cutscene.Cutscene = true;
            Assert.Equal(Owner.Cutscene, Decide(ownership, cutscene));
            Assert.True(ownership.HostMovementEnabled == false);
            Assert.True(ownership.HostCameraActive);
            Assert.True(ownership.SendLoadingFlag);

            Ownership dead = new Ownership();
            OwnershipInputs inputs = Linked();
            inputs.GuestDead = true;
            Assert.Equal(Owner.Cutscene, Decide(dead, inputs));
        }

        [Fact]
        public void LosingTheLinkReturnsEverythingInOneTransition()
        {
            Ownership ownership = new Ownership();
            Assert.Equal(Owner.GuestOwns, Decide(ownership, Linked()));

            OwnershipInputs lost = new OwnershipInputs { HasCharacter = true };
            Assert.Equal(Owner.HostOwns, Decide(ownership, lost));
            Assert.True(ownership.Changed);
            Assert.True(ownership.ReleaseGuestKeys);
            Assert.True(ownership.HostMovementEnabled);
            Assert.True(ownership.HostCameraActive);
            Assert.True(ownership.HostHudVisible);
        }

        [Fact]
        public void DecideIsIdempotentInsideOneFrame()
        {
            Ownership ownership = new Ownership();
            ownership.BeginFrame();
            Assert.Equal(Owner.GuestOwns, ownership.Decide(Linked()));
            // No BeginFrame() in between: a second caller gets the same answer and
            // Changed stays true once, not twice.
            Assert.Equal(Owner.GuestOwns, ownership.Decide(Linked()));
            Assert.True(ownership.Changed);

            ownership.BeginFrame();
            Assert.Equal(Owner.GuestOwns, ownership.Decide(Linked()));
            Assert.False(ownership.Changed);
        }

        [Fact]
        public void PriorityIsLinkThenCharacterThenCutsceneThenMenuThenTeleport()
        {
            Ownership ownership = new Ownership();
            OwnershipInputs input = Linked();
            input.Cutscene = true;
            input.HostMenuOpen = true;
            input.TeleportPending = true;
            ownership.BeginFrame();
            Assert.Equal(Owner.Cutscene, ownership.Decide(input));

            input.Cutscene = false;
            ownership.BeginFrame();
            Assert.Equal(Owner.HostMenu, ownership.Decide(input));

            input.HostMenuOpen = false;
            ownership.BeginFrame();
            Assert.Equal(Owner.Handoff, ownership.Decide(input));

            input.TeleportPending = false;
            ownership.BeginFrame();
            Assert.Equal(Owner.GuestOwns, ownership.Decide(input));
        }
    }
}

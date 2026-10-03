package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.BridgeServer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.item.PrimedTnt;
import net.minecraft.world.level.Explosion;
import net.minecraft.world.level.Level;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/**
 * Bonus coverage on top of the dedicated TNT hook: reports every other kind of
 * explosion (creepers, beds, respawn anchors, end crystals, ...) as well, so
 * anything that goes boom in Minecraft goes boom in R.E.P.O.
 *
 * This hook lives in the optional mixin config (mcrepo.optional.mixins.json,
 * required=false): its signature names a game enum, so if a future Minecraft
 * version renames it, the whole config is skipped gracefully and TNT keeps
 * working through TntEntityExplosionMixin.
 *
 * Target (official mappings, Minecraft 1.21.1):
 *   Level#explode(Entity, double, double, double, float, boolean, Level.ExplosionInteraction)
 */
@Mixin(Level.class)
public abstract class ServerWorldExplosionMixin {
    @Inject(
            method = "explode(Lnet/minecraft/world/entity/Entity;DDDFFZLnet/minecraft/world/level/Level$ExplosionInteraction;)Lnet/minecraft/world/level/Explosion;",
            at = @At("HEAD"))
    private void mcrepo$broadcastExplosion(
            Entity entity,
            double x,
            double y,
            double z,
            float power,
            boolean createFire,
            Level.ExplosionInteraction interactionType,
            CallbackInfoReturnable<Explosion> cir) {
        Level self = (Level) (Object) this;
        if (self.isClientSide || entity instanceof PrimedTnt) {
            return; // client echo or already reported by TntEntityExplosionMixin
        }
        String source = interactionType == null
                ? "generic"
                : interactionType.name().toLowerCase(java.util.Locale.ROOT);
        BridgeServer.broadcastExplosion(x, y, z, power, createFire, source);
    }
}

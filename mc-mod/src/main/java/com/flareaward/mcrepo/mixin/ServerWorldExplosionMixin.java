package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.BridgeServer;
import net.minecraft.entity.Entity;
import net.minecraft.entity.TntEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.world.World;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

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
 * Target (yarn, Minecraft 1.21.1):
 *   ServerWorld#createExplosion(Entity, double, double, double, float, boolean, World.ExplosionInteractionType)
 */
@Mixin(ServerWorld.class)
public abstract class ServerWorldExplosionMixin {
    @Inject(
            method = "createExplosion(Lnet/minecraft/entity/Entity;DDDFFZLnet/minecraft/world/World$ExplosionInteractionType;)Lnet/minecraft/world/explosion/Explosion;",
            at = @At("HEAD"))
    private void mcrepo$broadcastExplosion(
            Entity entity,
            double x,
            double y,
            double z,
            float power,
            boolean createFire,
            World.ExplosionInteractionType interactionType,
            CallbackInfo ci) {
        if (entity instanceof TntEntity) {
            return; // already reported by TntEntityExplosionMixin
        }
        String source = interactionType == null
                ? "generic"
                : interactionType.name().toLowerCase(java.util.Locale.ROOT);
        BridgeServer.broadcastExplosion(x, y, z, power, createFire, source);
    }
}

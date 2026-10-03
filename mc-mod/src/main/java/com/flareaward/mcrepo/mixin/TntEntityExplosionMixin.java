package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.BridgeServer;
import net.minecraft.world.entity.item.PrimedTnt;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

import java.lang.reflect.Field;

/**
 * The core of the TNT bridge: whenever a primed TNT entity detonates on the
 * logical server, tell R.E.P.O. where the blast happened. R.E.P.O. then
 * damages its enemies, items, valuables and players at the mapped position.
 *
 * PrimedTnt#explode() has had a stable no-arg signature forever, which keeps
 * this hook robust across Minecraft versions.
 */
@Mixin(PrimedTnt.class)
public abstract class TntEntityExplosionMixin {
    @Inject(method = "explode", at = @At("HEAD"))
    private void mcrepo$broadcastTntExplosion(CallbackInfo ci) {
        PrimedTnt self = (PrimedTnt) (Object) this;
        if (self.level().isClientSide) {
            return; // visual echo on the client; the server side already reported it
        }
        BridgeServer.broadcastExplosion(
                self.getX(),
                self.getY(),
                self.getZ(),
                readPower(self),
                false,
                "tnt");
    }

    /**
     * Reads the TNT power (default 4.0) without a @Shadow so a field rename in
     * a future version only costs us a fallback to the vanilla default.
     */
    private static float readPower(PrimedTnt entity) {
        try {
            for (Field field : PrimedTnt.class.getDeclaredFields()) {
                if (field.getType() == float.class) {
                    field.setAccessible(true);
                    float value = field.getFloat(entity);
                    if (value > 0f) {
                        return value;
                    }
                }
            }
        } catch (Throwable ignored) {
            // fall back to vanilla TNT power
        }
        return 4.0f;
    }
}

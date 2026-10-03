package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.CameraSync;
import net.minecraft.client.network.ClientPlayNetworkHandler;
import net.minecraft.network.packet.s2c.play.PlayerPositionLookS2CPacket;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * While the bridge drives the camera, drop server position/look corrections so
 * the integrated server never rubber-bands the player back to its own (lagging)
 * idea of where the player is.
 *
 * Optional config: if the packet handler method is renamed in a future game
 * version, this hook is skipped (minor rubber-banding) instead of breaking
 * camera sync entirely.
 */
@Mixin(ClientPlayNetworkHandler.class)
public abstract class IgnorePositionCorrectionMixin {
    @Inject(method = "onPlayerPositionLook", at = @At("HEAD"), cancellable = true)
    private void mcrepo$ignoreCorrections(PlayerPositionLookS2CPacket packet, CallbackInfo ci) {
        CameraSync cameraSync = CameraSync.get();
        if (cameraSync != null && cameraSync.isActive()) {
            ci.cancel();
        }
    }
}

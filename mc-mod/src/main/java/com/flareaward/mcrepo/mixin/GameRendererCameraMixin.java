package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.CameraSync;
import net.minecraft.client.DeltaTracker;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * Applies the latest R.E.P.O. camera pose once per rendered frame, right before
 * Minecraft renders. This is what makes the Minecraft view track R.E.P.O.
 * smoothly instead of stuttering at 20 Hz.
 *
 * Note: GameRenderer#render(DeltaTracker, boolean) is the 1.21.1 signature.
 */
@Mixin(GameRenderer.class)
public abstract class GameRendererCameraMixin {
    @Inject(method = "render", at = @At("HEAD"))
    private void mcrepo$applyBridgeCamera(DeltaTracker deltaTracker, boolean renderLevel, CallbackInfo ci) {
        CameraSync cameraSync = CameraSync.get();
        if (cameraSync != null) {
            cameraSync.onRenderFrame();
        }
    }
}

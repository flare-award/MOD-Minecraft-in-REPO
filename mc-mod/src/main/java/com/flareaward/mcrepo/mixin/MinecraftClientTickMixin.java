package com.flareaward.mcrepo.mixin;

import com.flareaward.mcrepo.BridgeServer;
import com.flareaward.mcrepo.CameraSync;
import net.minecraft.client.MinecraftClient;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Drives the 20 Hz camera-follow update and keeps the player snapshot fresh for "getpos". */
@Mixin(MinecraftClient.class)
public abstract class MinecraftClientTickMixin {
    @Inject(method = "tick", at = @At("HEAD"))
    private void mcrepo$bridgeTick(CallbackInfo ci) {
        MinecraftClient client = (MinecraftClient) (Object) this;
        CameraSync cameraSync = CameraSync.get();
        if (cameraSync != null) {
            cameraSync.onClientTick(client);
        }
        if (client.player != null) {
            BridgeServer.PLAYER_SNAPSHOT.set(new double[] {
                    client.player.getX(),
                    client.player.getY(),
                    client.player.getZ(),
                    client.player.getYaw(),
                    client.player.getPitch()
            });
        }
    }
}

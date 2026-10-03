package com.flareaward.mcrepo;

import net.fabricmc.api.ClientModInitializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Client-side entrypoint for the Minecraft half of "Minecraft in R.E.P.O.".
 *
 * Responsibilities of this side of the bridge:
 *  - run a local TCP server (loopback only) that the R.E.P.O. plugin connects to;
 *  - follow the R.E.P.O. camera: every pose received over the bridge is applied
 *    to the local player/camera, so Minecraft renders the world from exactly
 *    where the R.E.P.O. player is looking;
 *  - broadcast every TNT explosion (and, via the optional mixin, other
 *    explosions) back to R.E.P.O. so they can damage enemies, items,
 *    valuables and players over there.
 */
public final class McRepoBridge implements ClientModInitializer {
    public static final String MOD_ID = "mcrepo";
    public static final String BRIDGE_VERSION = "1.0.0";
    public static final Logger LOGGER = LoggerFactory.getLogger("MinecraftInRepo");

    @Override
    public void onInitializeClient() {
        BridgeConfig config = BridgeConfig.load();
        CameraSync.init(config);
        if (config.enabled) {
            BridgeServer.start(config);
            LOGGER.info("Bridge listening on {}:{}", config.bind, config.port);
        } else {
            LOGGER.info("Bridge disabled via config (enabled=false). Camera sync/explosions inactive.");
        }
    }
}

package com.flareaward.mcrepo;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import net.fabricmc.loader.api.FabricLoader;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * Persistent configuration, stored in config/mcrepo-bridge.json.
 * Written with defaults on first launch.
 */
public final class BridgeConfig {
    /** Master switch for the bridge server. */
    public boolean enabled = true;
    /** Interface to bind. Keep loopback: the bridge is not meant to be exposed. */
    public String bind = "127.0.0.1";
    /** TCP port the R.E.P.O. plugin connects to (must match its Bridge.Port). */
    public int port = 47621;
    /** Follow the R.E.P.O. camera with the Minecraft player/camera. */
    public boolean applyCamera = true;
    /** Report explosions (TNT etc.) to R.E.P.O. */
    public boolean broadcastExplosions = true;
    /** Try to match Minecraft's FOV to the FOV sent by R.E.P.O. */
    public boolean syncFov = true;
    /** Hide the Minecraft HUD while the bridge drives the camera. */
    public boolean hideHud = true;
    /** Ask the integrated server for creative mode when a world loads (avoids suffocation/fall interference). */
    public boolean requestCreativeMode = true;

    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

    public static BridgeConfig load() {
        Path path = FabricLoader.getInstance().getConfigDir().resolve("mcrepo-bridge.json");
        try {
            if (Files.exists(path)) {
                String text = new String(Files.readAllBytes(path), StandardCharsets.UTF_8);
                BridgeConfig loaded = GSON.fromJson(text, BridgeConfig.class);
                if (loaded != null) {
                    return loaded;
                }
            }
        } catch (Exception e) {
            McRepoBridge.LOGGER.warn("Could not read {}: {}", path, e.toString());
        }
        BridgeConfig defaults = new BridgeConfig();
        try {
            Path parent = path.getParent();
            if (parent != null) {
                Files.createDirectories(parent);
            }
            Files.write(path, GSON.toJson(defaults).getBytes(StandardCharsets.UTF_8));
        } catch (IOException e) {
            McRepoBridge.LOGGER.warn("Could not write default config {}: {}", path, e.toString());
        }
        return defaults;
    }
}

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
    /**
     * Bumped whenever new options are added, so an older config file on disk can
     * be upgraded to the new defaults instead of silently falling back to false.
     */
    public int configVersion = 2;
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
    /**
     * Solo play: R.E.P.O. has the focus, so Minecraft runs in the background.
     * Vanilla pauses a singleplayer world when the window loses focus - this
     * turns that off (same as F3 + P / pauseOnLostFocus:false) while the bridge
     * drives the camera, and restores the old value afterwards.
     */
    public boolean keepRunningWhenUnfocused = true;
    /**
     * Solo play: also move the integrated-server copy of the player to the
     * R.E.P.O. camera. Without this the server still thinks the player stands at
     * the spawn point, so TNT far away from it is never ticked and does not
     * explode. Driving the server player makes chunk simulation follow the
     * camera, which is exactly what we want when both games share one view.
     */
    public boolean driveServerPlayerInSingleplayer = true;

    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

    public static BridgeConfig load() {
        Path path = FabricLoader.getInstance().getConfigDir().resolve("mcrepo-bridge.json");
        try {
            if (Files.exists(path)) {
                String text = new String(Files.readAllBytes(path), StandardCharsets.UTF_8);
                BridgeConfig loaded = GSON.fromJson(text, BridgeConfig.class);
                if (loaded != null) {
                    return upgrade(loaded, path);
                }
            }
        } catch (Exception e) {
            McRepoBridge.LOGGER.warn("Could not read {}: {}", path, e.toString());
        }
        BridgeConfig defaults = new BridgeConfig();
        save(defaults, path);
        return defaults;
    }

    /** Options added after the first release default to false when Gson reads an
     *  old file; push them back to their intended singleplayer-friendly value. */
    private static BridgeConfig upgrade(BridgeConfig loaded, Path path) {
        if (loaded.configVersion >= 2) {
            return loaded;
        }
        loaded.keepRunningWhenUnfocused = true;
        loaded.driveServerPlayerInSingleplayer = true;
        loaded.configVersion = 2;
        save(loaded, path);
        return loaded;
    }

    private static void save(BridgeConfig config, Path path) {
        try {
            Path parent = path.getParent();
            if (parent != null) {
                Files.createDirectories(parent);
            }
            Files.write(path, GSON.toJson(config).getBytes(StandardCharsets.UTF_8));
        } catch (IOException e) {
            McRepoBridge.LOGGER.warn("Could not write config {}: {}", path, e.toString());
        }
    }
}

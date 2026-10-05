package com.flareaward.mcrepo;

import net.minecraft.client.CameraType;
import net.minecraft.client.Minecraft;
import net.minecraft.client.Options;
import net.minecraft.client.player.Input;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.player.Abilities;
import net.minecraft.world.phys.Vec3;

import java.lang.reflect.Field;
import java.lang.reflect.Method;

/**
 * Makes the Minecraft camera follow the R.E.P.O. camera.
 *
 * The R.E.P.O. plugin sends eye-position + yaw/pitch/fov at up to ~60 Hz.
 * Every render frame we move the local player to the newest pose (exponentially
 * smoothed), and every client tick we keep the player in a docile state
 * (flying, invulnerable, no gravity, no clip, zeroed input) so vanilla physics
 * never fights the camera.
 *
 * Written against Mojang's official mappings.
 */
public final class CameraSync {
    /** How long a pose is considered fresh before we stop driving the camera. */
    private static final long POSE_TIMEOUT_MS = 3000L;
    /** Smoothing rate; higher = snappier camera. */
    private static final double SMOOTH_RATE = 30.0;
    /** Teleport instead of gliding when further away than this many blocks. */
    private static final double SNAP_DISTANCE = 48.0;

    private static CameraSync instance;

    public static void init(BridgeConfig config) {
        instance = new CameraSync(config);
    }

    public static CameraSync get() {
        return instance;
    }

    private final BridgeConfig config;

    // Latest pose received from R.E.P.O. (written by the socket thread).
    private final Object poseLock = new Object();
    private double targetX, targetY, targetZ;
    private float targetYaw, targetPitch, targetFov;
    private long poseReceivedAt;
    private boolean hasPose;

    // Render-thread smoothing state.
    private double smoothX, smoothY, smoothZ;
    private float smoothYaw, smoothPitch;
    private boolean seeded;
    private long lastFrameNanos;
    private long lastFovSyncMillis;

    private boolean worldSetupDone;

    // Solo-play session tweaks, applied once and reverted when the bridge stops
    // driving the camera:
    //   * pauseOnLostFocus = false  (R.E.P.O. owns the focus while you play)
    //   * server-side abilities     (fly + invulnerable, works without cheats)
    private Boolean savedPauseOnLostFocus;
    private boolean pauseOverridden;
    private boolean serverTweaksApplied;
    private Boolean savedMayfly;
    private Boolean savedFlying;
    private Boolean savedInvulnerable;
    private boolean wasActive;

    private CameraSync(BridgeConfig config) {
        this.config = config;
    }

    public void receivePose(double x, double y, double z, float yaw, float pitch, float fov) {
        synchronized (poseLock) {
            targetX = x;
            targetY = y;
            targetZ = z;
            targetYaw = normalizeYaw(yaw);
            targetPitch = clamp(pitch, -89.9f, 89.9f);
            targetFov = fov;
            poseReceivedAt = System.currentTimeMillis();
            hasPose = true;
        }
    }

    /** True while we should be overriding the player/camera. Used to suppress server corrections. */
    public boolean isActive() {
        if (!config.applyCamera || !hasPose) {
            return false;
        }
        return System.currentTimeMillis() - poseReceivedAt < POSE_TIMEOUT_MS;
    }

    /** Called from the GameRenderer.render mixin, i.e. once per rendered frame. */
    public void onRenderFrame() {
        if (!isActive()) {
            return;
        }
        Minecraft client = Minecraft.getInstance();
        LocalPlayer player = client.player;
        if (player == null || client.level == null) {
            return;
        }

        double tx, ty, tz;
        float tyaw, tpitch;
        synchronized (poseLock) {
            tx = targetX;
            ty = targetY;
            tz = targetZ;
            tyaw = targetYaw;
            tpitch = targetPitch;
        }

        long now = System.nanoTime();
        double dt = lastFrameNanos == 0 ? 0.016 : Math.min(0.1, (now - lastFrameNanos) / 1e9);
        lastFrameNanos = now;

        // The camera sits at the player position plus eye height; aim the camera, not the feet.
        double px = tx;
        double py = ty - player.getEyeHeight();
        double pz = tz;

        if (!seeded || distance(smoothX, smoothY, smoothZ, px, py, pz) > SNAP_DISTANCE) {
            smoothX = px;
            smoothY = py;
            smoothZ = pz;
            smoothYaw = tyaw;
            smoothPitch = tpitch;
            seeded = true;
        } else {
            double k = 1.0 - Math.exp(-dt * SMOOTH_RATE);
            smoothX += (px - smoothX) * k;
            smoothY += (py - smoothY) * k;
            smoothZ += (pz - smoothZ) * k;
            smoothYaw += shortestAngleDelta(smoothYaw, tyaw) * k;
            smoothPitch += (tpitch - smoothPitch) * k;
        }

        applyToPlayer(player, smoothX, smoothY, smoothZ, normalizeYaw(smoothYaw), clamp(smoothPitch, -89.9f, 89.9f));
    }

    /** Called from the Minecraft.tick mixin at 20 Hz. */
    public void onClientTick(Minecraft client) {
        LocalPlayer player = client.player;
        if (player == null || client.level == null) {
            worldSetupDone = false;
            seeded = false;
            revertSessionTweaks(client);
            wasActive = false;
            return;
        }
        boolean active = isActive();
        if (wasActive && !active) {
            // R.E.P.O. went away: give Minecraft its own settings back.
            revertSessionTweaks(client);
        }
        wasActive = active;
        if (!active) {
            return;
        }

        setupWorldOnce(client);
        syncFovPeriodically(client);

        // Authoritative 20 Hz application (also covers very low FPS situations).
        double tx, ty, tz;
        float tyaw, tpitch;
        synchronized (poseLock) {
            tx = targetX;
            ty = targetY;
            tz = targetZ;
            tyaw = targetYaw;
            tpitch = targetPitch;
        }
        smoothX = tx;
        smoothY = ty - player.getEyeHeight();
        smoothZ = tz;
        smoothYaw = tyaw;
        smoothPitch = tpitch;
        seeded = true;
        applyToPlayer(player, smoothX, smoothY, smoothZ, tyaw, tpitch);
        driveServerPlayer(client, smoothX, smoothY, smoothZ, tyaw, tpitch);
    }

    /**
     * Solo play lives on the integrated server, and the integrated server is
     * what decides which chunks get simulated. If it still thinks the player is
     * standing at the spawn point, TNT lit 200 blocks away (i.e. right next to
     * the R.E.P.O. camera) is never ticked and never explodes. Moving the
     * server-side copy of the player to the camera pose makes chunk simulation,
     * mobs and TNT follow the shared view - and it also means there is nothing
     * left for the server to "correct" on the client.
     */
    private void driveServerPlayer(Minecraft client, double x, double y, double z, float yaw, float pitch) {
        if (!config.driveServerPlayerInSingleplayer) {
            return;
        }
        MinecraftServer server = client.getSingleplayerServer();
        if (server == null || client.player == null) {
            return; // multiplayer server: leave its authority alone
        }
        java.util.UUID uuid = client.player.getUUID();
        server.execute(() -> {
            try {
                ServerPlayer serverPlayer = server.getPlayerList().getPlayer(uuid);
                if (serverPlayer == null) {
                    return;
                }
                serverPlayer.setPos(x, y, z);
                serverPlayer.xOld = x;
                serverPlayer.yOld = y;
                serverPlayer.zOld = z;
                serverPlayer.setYRot(yaw);
                serverPlayer.setXRot(pitch);
                serverPlayer.setDeltaMovement(Vec3.ZERO);
                serverPlayer.setNoGravity(true);
                serverPlayer.noPhysics = true;
                serverPlayer.fallDistance = 0.0f;
            } catch (Throwable t) {
                McRepoBridge.LOGGER.debug("Could not move the server player: {}", t.toString());
            }
        });
    }

    private void applyToPlayer(LocalPlayer player, double x, double y, double z, float yaw, float pitch) {
        player.setPos(x, y, z);
        // Kill render interpolation between previous and current tick positions:
        // we drive the camera every frame ourselves.
        player.xOld = x;
        player.yOld = y;
        player.zOld = z;
        player.setYRot(yaw);
        player.setXRot(pitch);
        player.yRotO = yaw;
        player.xRotO = pitch;
        player.setDeltaMovement(Vec3.ZERO);
        player.fallDistance = 0.0f;
        player.noPhysics = true;
        player.setNoGravity(true);
        player.getAbilities().mayfly = true;
        player.getAbilities().flying = true;
        player.getAbilities().invulnerable = true;
        // Neutralize keyboard/WASD so the player cannot fight the camera.
        player.input = new Input();
        player.horizontalCollision = false;
        player.verticalCollision = false;
    }

    private void setupWorldOnce(Minecraft client) {
        if (worldSetupDone) {
            return;
        }
        worldSetupDone = true;

        if (config.hideHud) {
            try {
                client.options.hideGui = true;
            } catch (Throwable t) {
                McRepoBridge.LOGGER.debug("Could not hide HUD: {}", t.toString());
            }
        }
        try {
            client.options.setCameraType(CameraType.FIRST_PERSON);
        } catch (Throwable t) {
            McRepoBridge.LOGGER.debug("Could not force first person view: {}", t.toString());
        }

        if (config.keepRunningWhenUnfocused) {
            setPauseOnLostFocus(client, false);
        }

        if (config.requestCreativeMode) {
            MinecraftServer server = client.getSingleplayerServer();
            if (server != null) {
                try {
                    server.getCommands().performPrefixedCommand(server.createCommandSourceStack(), "gamemode creative");
                } catch (Throwable t) {
                    McRepoBridge.LOGGER.debug("Could not switch to creative mode: {}", t.toString());
                }
            }
            applyServerAbilities(client);
        }
    }

    /**
     * The /gamemode command needs cheats, and without it a survival player
     * cannot fly - so the camera would be yanked back to the ground. Setting the
     * abilities straight on the integrated server's player copy works on any
     * singleplayer world and is synced to the client by vanilla.
     */
    private void applyServerAbilities(Minecraft client) {
        if (serverTweaksApplied) {
            return;
        }
        MinecraftServer server = client.getSingleplayerServer();
        if (server == null || client.player == null) {
            return;
        }
        java.util.UUID uuid = client.player.getUUID();
        serverTweaksApplied = true;
        server.execute(() -> {
            try {
                ServerPlayer serverPlayer = server.getPlayerList().getPlayer(uuid);
                if (serverPlayer == null) {
                    serverTweaksApplied = false;
                    return;
                }
                Abilities abilities = serverPlayer.getAbilities();
                savedMayfly = abilities.mayfly;
                savedFlying = abilities.flying;
                savedInvulnerable = abilities.invulnerable;
                abilities.mayfly = true;
                abilities.flying = true;
                abilities.invulnerable = true;
                serverPlayer.onUpdateAbilities();
            } catch (Throwable t) {
                McRepoBridge.LOGGER.debug("Could not apply creative abilities: {}", t.toString());
            }
        });
    }

    private void revertSessionTweaks(Minecraft client) {
        if (!pauseOverridden && !serverTweaksApplied) {
            return; // nothing was changed, nothing to give back
        }
        setPauseOnLostFocus(client, savedPauseOnLostFocus == null ? true : savedPauseOnLostFocus);
        pauseOverridden = false;
        savedPauseOnLostFocus = null;

        if (!serverTweaksApplied) {
            return;
        }
        MinecraftServer server = client.getSingleplayerServer();
        if (server == null) {
            serverTweaksApplied = false;
            return;
        }
        java.util.UUID uuid = client.player == null ? null : client.player.getUUID();
        server.execute(() -> {
            try {
                ServerPlayer serverPlayer = uuid == null ? null : server.getPlayerList().getPlayer(uuid);
                if (serverPlayer != null) {
                    Abilities abilities = serverPlayer.getAbilities();
                    if (savedMayfly != null) {
                        abilities.mayfly = savedMayfly;
                    }
                    if (savedFlying != null) {
                        abilities.flying = savedFlying;
                    }
                    if (savedInvulnerable != null) {
                        abilities.invulnerable = savedInvulnerable;
                    }
                    serverPlayer.setNoGravity(false);
                    serverPlayer.noPhysics = false;
                    serverPlayer.onUpdateAbilities();
                }
            } catch (Throwable t) {
                McRepoBridge.LOGGER.debug("Could not restore player abilities: {}", t.toString());
            }
        });
        serverTweaksApplied = false;
        savedMayfly = null;
        savedFlying = null;
        savedInvulnerable = null;
        worldSetupDone = false;
    }

    /**
     * Solo play means R.E.P.O. holds the window focus, and vanilla pauses a
     * singleplayer world 0.5 s after the Minecraft window loses it - TNT would
     * freeze mid-fuse. This is the programmatic version of the F3 + P toggle
     * (options.txt: pauseOnLostFocus), looked up reflectively so the mod still
     * compiles if a future version moves or renames the field.
     */
    private void setPauseOnLostFocus(Minecraft client, boolean value) {
        try {
            Object target = client.options;
            Field field = findField(Options.class, "pauseOnLostFocus");
            if (field == null) {
                target = client;
                field = findField(Minecraft.class, "pauseOnLostFocus");
            }
            if (field == null) {
                return;
            }
            field.setAccessible(true);
            Object current = field.get(target);
            if (current instanceof Boolean) {
                if (!pauseOverridden) {
                    savedPauseOnLostFocus = (Boolean) current;
                    pauseOverridden = true;
                }
                if ((Boolean) current != value) {
                    field.setBoolean(target, value);
                }
                return;
            }
            if (current != null) {
                // Newer versions keep it as an OptionInstance<Boolean>.
                Method setter = findMethod(current.getClass(), "set", Object.class);
                if (setter != null) {
                    pauseOverridden = true;
                    setter.setAccessible(true);
                    setter.invoke(current, Boolean.valueOf(value));
                }
            }
        } catch (Throwable t) {
            McRepoBridge.LOGGER.debug("Could not change pauseOnLostFocus: {}", t.toString());
        }
    }

    private static Field findField(Class<?> type, String name) {
        Class<?> current = type;
        while (current != null) {
            try {
                return current.getDeclaredField(name);
            } catch (NoSuchFieldException ignored) {
                current = current.getSuperclass();
            } catch (Throwable ignored) {
                return null;
            }
        }
        return null;
    }

    private static Method findMethod(Class<?> type, String name, Class<?> parameter) {
        Class<?> current = type;
        while (current != null) {
            try {
                return current.getDeclaredMethod(name, parameter);
            } catch (NoSuchMethodException ignored) {
                current = current.getSuperclass();
            } catch (Throwable ignored) {
                return null;
            }
        }
        return null;
    }

    private void syncFovPeriodically(Minecraft client) {
        if (!config.syncFov) {
            return;
        }
        long now = System.currentTimeMillis();
        if (now - lastFovSyncMillis < 2000) {
            return;
        }
        lastFovSyncMillis = now;
        float fov;
        synchronized (poseLock) {
            fov = targetFov;
        }
        if (fov < 30f || fov > 160f) {
            return;
        }
        try {
            // Minecraft's FOV slider maps roughly 1:1 to the real vertical FOV at 70.
            client.options.fov().set((int) Math.round(fov));
        } catch (Throwable t) {
            McRepoBridge.LOGGER.debug("Could not sync FOV: {}", t.toString());
        }
    }

    private static float normalizeYaw(float yaw) {
        float result = yaw % 360f;
        if (result >= 180f) {
            result -= 360f;
        } else if (result < -180f) {
            result += 360f;
        }
        return result;
    }

    private static float shortestAngleDelta(float from, float to) {
        float delta = (to - from) % 360f;
        if (delta > 180f) {
            delta -= 360f;
        } else if (delta < -180f) {
            delta += 360f;
        }
        return delta;
    }

    private static double distance(double x1, double y1, double z1, double x2, double y2, double z2) {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double dz = z2 - z1;
        return Math.sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float clamp(float value, float min, float max) {
        return value < min ? min : Math.min(value, max);
    }
}

package com.flareaward.mcrepo;

import net.minecraft.client.CameraType;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.Input;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.phys.Vec3;

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
            return;
        }
        if (!isActive()) {
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

        if (config.requestCreativeMode) {
            MinecraftServer server = client.getSingleplayerServer();
            if (server != null) {
                try {
                    server.getCommands().performPrefixedCommand(server.createCommandSourceStack(), "gamemode creative");
                } catch (Throwable t) {
                    McRepoBridge.LOGGER.debug("Could not switch to creative mode: {}", t.toString());
                }
            }
        }
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

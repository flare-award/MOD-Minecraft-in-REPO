package com.flareaward.mcrepo;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import net.fabricmc.loader.api.FabricLoader;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Loopback TCP bridge. Speaks newline-delimited JSON with the R.E.P.O. plugin:
 *
 *   R.E.P.O. -> Minecraft
 *     {"t":"hello","proto":1}
 *     {"t":"cam","x":..,"y":..,"z":..,"yaw":..,"pitch":..,"fov":..}
 *     {"t":"getpos"}          (asks for the current Minecraft player pose)
 *     {"t":"ping"}
 *
 *   Minecraft -> R.E.P.O.
 *     {"t":"hello","proto":1,"mc":"<version>","bridge":"<version>"}
 *     {"t":"pos","x":..,"y":..,"z":..,"yaw":..,"pitch":..}
 *     {"t":"boom","x":..,"y":..,"z":..,"power":..,"fire":bool,"source":"tnt|..."}
 *     {"t":"pong"}
 *
 * Only one client is served at a time (the R.E.P.O. running on this machine).
 */
public final class BridgeServer {
    public static final int PROTOCOL = 1;

    private static BridgeServer instance;

    /** x, y, z, yaw, pitch of the local player; refreshed every client tick. */
    public static final AtomicReference<double[]> PLAYER_SNAPSHOT =
            new AtomicReference<>(new double[] { 0.0, 64.0, 0.0, 0.0, 0.0 });

    private final BridgeConfig config;
    private final ConcurrentLinkedQueue<String> outQueue = new ConcurrentLinkedQueue<>();
    private volatile Socket clientSocket;
    private volatile boolean running = true;

    public static void start(BridgeConfig config) {
        instance = new BridgeServer(config);
        Thread acceptThread = new Thread(instance::acceptLoop, "MinecraftInRepo-Bridge");
        acceptThread.setDaemon(true);
        acceptThread.start();
    }

    public static BridgeServer get() {
        return instance;
    }

    public static boolean isConnected() {
        BridgeServer server = instance;
        if (server == null) {
            return false;
        }
        Socket socket = server.clientSocket;
        return socket != null && socket.isConnected() && !socket.isClosed();
    }

    /** Called by the explosion mixins from the (integrated) server thread. */
    public static void broadcastExplosion(double x, double y, double z, float power, boolean fire, String source) {
        BridgeServer server = instance;
        if (server == null || !server.config.broadcastExplosions || !isConnected()) {
            return;
        }
        JsonObject message = new JsonObject();
        message.addProperty("t", "boom");
        message.addProperty("x", round(x));
        message.addProperty("y", round(y));
        message.addProperty("z", round(z));
        message.addProperty("power", power);
        message.addProperty("fire", fire);
        message.addProperty("source", source);
        server.enqueue(message.toString());
    }

    private BridgeServer(BridgeConfig config) {
        this.config = config;
    }

    private void enqueue(String line) {
        outQueue.add(line);
    }

    private void acceptLoop() {
        ServerSocket serverSocket;
        try {
            serverSocket = new ServerSocket();
            serverSocket.setReuseAddress(true);
            serverSocket.bind(new InetSocketAddress(config.bind, config.port));
        } catch (IOException e) {
            McRepoBridge.LOGGER.error("Could not bind bridge to {}:{} - {}", config.bind, config.port, e.toString());
            return;
        }
        while (running) {
            try {
                Socket socket = serverSocket.accept();
                handleClient(socket);
            } catch (IOException e) {
                if (!running) {
                    return;
                }
                McRepoBridge.LOGGER.warn("Bridge accept failed: {}", e.toString());
                sleepQuietly(500);
            }
        }
    }

    private void handleClient(Socket socket) {
        Socket previous = clientSocket;
        clientSocket = socket;
        closeQuietly(previous);
        McRepoBridge.LOGGER.info("R.E.P.O. connected ({})", socket.getRemoteSocketAddress());

        Thread writerThread = new Thread(() -> writerLoop(socket), "MinecraftInRepo-BridgeWriter");
        writerThread.setDaemon(true);
        writerThread.start();

        try {
            socket.setTcpNoDelay(true);
            BufferedReader reader = new BufferedReader(
                    new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
            String line;
            while (!socket.isClosed() && (line = reader.readLine()) != null) {
                handleMessage(line);
            }
        } catch (IOException e) {
            // Peer went away; fall through to cleanup.
        } finally {
            if (clientSocket == socket) {
                clientSocket = null;
            }
            closeQuietly(socket);
            McRepoBridge.LOGGER.info("R.E.P.O. disconnected");
        }
    }

    private void writerLoop(Socket socket) {
        try {
            BufferedWriter writer = new BufferedWriter(
                    new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8));
            StringBuilder batch = new StringBuilder();
            while (!socket.isClosed()) {
                String line = outQueue.poll();
                if (line == null) {
                    sleepQuietly(5);
                    continue;
                }
                batch.append(line).append('\n');
                while ((line = outQueue.poll()) != null) {
                    batch.append(line).append('\n');
                }
                writer.write(batch.toString());
                batch.setLength(0);
                writer.flush();
            }
        } catch (IOException e) {
            closeQuietly(socket);
        }
    }

    private void handleMessage(String line) {
        try {
            JsonObject message = JsonParser.parseString(line).getAsJsonObject();
            if (!message.has("t")) {
                return;
            }
            String type = message.get("t").getAsString();
            switch (type) {
                case "hello":
                    sendHello();
                    break;
                case "ping": {
                    JsonObject pong = new JsonObject();
                    pong.addProperty("t", "pong");
                    enqueue(pong.toString());
                    break;
                }
                case "getpos": {
                    double[] snapshot = PLAYER_SNAPSHOT.get();
                    JsonObject pos = new JsonObject();
                    pos.addProperty("t", "pos");
                    pos.addProperty("x", round(snapshot[0]));
                    pos.addProperty("y", round(snapshot[1]));
                    pos.addProperty("z", round(snapshot[2]));
                    pos.addProperty("yaw", round(snapshot[3]));
                    pos.addProperty("pitch", round(snapshot[4]));
                    enqueue(pos.toString());
                    break;
                }
                case "cam": {
                    CameraSync cameraSync = CameraSync.get();
                    if (cameraSync != null) {
                        cameraSync.receivePose(
                                number(message, "x"),
                                number(message, "y"),
                                number(message, "z"),
                                (float) number(message, "yaw"),
                                (float) number(message, "pitch"),
                                (float) number(message, "fov"));
                    }
                    break;
                }
                default:
                    break;
            }
        } catch (Exception e) {
            McRepoBridge.LOGGER.debug("Ignoring malformed bridge message: {}", line);
        }
    }

    private void sendHello() {
        String minecraftVersion = FabricLoader.getInstance()
                .getModContainer("minecraft")
                .map(container -> container.getMetadata().getVersion().getFriendlyString())
                .orElse("unknown");
        JsonObject hello = new JsonObject();
        hello.addProperty("t", "hello");
        hello.addProperty("proto", PROTOCOL);
        hello.addProperty("mc", minecraftVersion);
        hello.addProperty("bridge", McRepoBridge.BRIDGE_VERSION);
        enqueue(hello.toString());
    }

    private static double number(JsonObject message, String key) {
        return message.has(key) ? message.get(key).getAsDouble() : 0.0;
    }

    private static double round(double value) {
        return Math.round(value * 1000.0) / 1000.0;
    }

    private static void closeQuietly(Socket socket) {
        if (socket == null) {
            return;
        }
        try {
            socket.close();
        } catch (IOException ignored) {
            // nothing to do
        }
    }

    private static void sleepQuietly(long millis) {
        try {
            Thread.sleep(millis);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}

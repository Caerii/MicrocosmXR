package io.github.microcosmxr.streamer;

import net.minecraft.server.MinecraftServer;

import java.net.InetSocketAddress;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

/**
 * WebSocket server that streams chunk/block data to connected clients (e.g. Unity).
 * Runs on the game thread when sending; accepts connections on a background thread.
 */
public class StreamerServer {

	private final MinecraftServer server;
	private final int port;
	private final List<StreamerWebSocketHandler> clients = new CopyOnWriteArrayList<>();
	private StreamerWebSocketServer wsServer;
	private Thread wsThread;
	private volatile int originX;
	private volatile int originY;
	private volatile int originZ;
	private static final double DEFAULT_SCALE = 0.02;

	public StreamerServer(MinecraftServer server, int port) {
		this.server = server;
		this.port = port;
		this.originX = 0;
		this.originY = 64;
		this.originZ = 0;
	}

	public void setOrigin(int x, int y, int z) {
		this.originX = x;
		this.originY = y;
		this.originZ = z;
	}

	public int getOriginX() { return originX; }
	public int getOriginY() { return originY; }
	public int getOriginZ() { return originZ; }

	public void start() {
		try {
			wsServer = new StreamerWebSocketServer(new InetSocketAddress(port), server, this);
			wsServer.setReuseAddr(true);
			wsThread = new Thread(wsServer::run, "MicrocosmStreamer-WS");
			wsThread.setDaemon(true);
			wsThread.start();
		} catch (Exception e) {
			MicrocosmStreamerMod.LOGGER.error("Failed to start WebSocket server", e);
		}
	}

	public void stop() {
		if (wsServer != null) {
			try {
				wsServer.stop();
			} catch (Exception e) {
				MicrocosmStreamerMod.LOGGER.warn("Error stopping WebSocket server", e);
			}
			wsServer = null;
		}
		clients.clear();
	}

	void onOpen(StreamerWebSocketHandler client) {
		clients.add(client);
		MicrocosmStreamerMod.LOGGER.info("Streamer client connected (total: {})", clients.size());
		// Send HELLO + SET_ORIGIN immediately
		client.sendHello();
		client.sendSetOrigin(originX, originY, originZ, DEFAULT_SCALE);
		// Stream chunk region on next server tick (must run on game thread)
		server.execute(() -> StreamRegionTask.streamRegionToClient(server, this, client));
	}

	void onClose(StreamerWebSocketHandler client) {
		clients.remove(client);
		MicrocosmStreamerMod.LOGGER.info("Streamer client disconnected (remaining: {})", clients.size());
	}

	public void broadcastChunkSectionSnapshot(int cx, int cz, int sy, List<String> palette, short[] indices) {
		for (StreamerWebSocketHandler client : clients) {
			client.sendChunkSectionSnapshot(cx, cz, sy, palette, indices);
		}
	}

	public void broadcastBlockDelta(int x, int y, int z, String blockStateId) {
		for (StreamerWebSocketHandler client : clients) {
			client.sendBlockDelta(x, y, z, blockStateId);
		}
	}
}

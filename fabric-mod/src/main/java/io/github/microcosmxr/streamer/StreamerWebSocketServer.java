package io.github.microcosmxr.streamer;

import org.java_websocket.WebSocket;
import org.java_websocket.server.WebSocketServer;

import java.net.InetSocketAddress;
import java.util.concurrent.ConcurrentHashMap;

/**
 * WebSocket server that accepts connections and delegates to StreamerWebSocketHandler.
 */
public class StreamerWebSocketServer extends WebSocketServer {

	private final StreamerServer streamerServer;
	private final ConcurrentHashMap<WebSocket, StreamerWebSocketHandler> handlers = new ConcurrentHashMap<>();

	public StreamerWebSocketServer(InetSocketAddress address, net.minecraft.server.MinecraftServer server, StreamerServer streamerServer) {
		super(address);
		this.streamerServer = streamerServer;
		// server is available for future use (e.g. per-world streaming)
	}

	@Override
	public void onOpen(WebSocket conn, org.java_websocket.handshake.ClientHandshake handshake) {
		StreamerWebSocketHandler handler = new StreamerWebSocketHandler(conn, streamerServer);
		handlers.put(conn, handler);
		streamerServer.onOpen(handler);
	}

	@Override
	public void onClose(WebSocket conn, int code, String reason, boolean remote) {
		StreamerWebSocketHandler handler = handlers.remove(conn);
		if (handler != null) {
			streamerServer.onClose(handler);
		}
	}

	@Override
	public void onMessage(WebSocket conn, String message) {
		// Optional: handle text commands from client (e.g. request chunk at x,z)
	}

	@Override
	public void onError(WebSocket conn, Exception ex) {
		MicrocosmStreamerMod.LOGGER.warn("WebSocket error", ex);
	}

	@Override
	public void onStart() {
		MicrocosmStreamerMod.LOGGER.info("Microcosm Streamer WebSocket server started on port {}", getPort());
	}
}

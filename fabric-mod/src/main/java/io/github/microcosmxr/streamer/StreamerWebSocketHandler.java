package io.github.microcosmxr.streamer;

import org.java_websocket.WebSocket;

import java.io.ByteArrayOutputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.List;

/**
 * Encodes and sends protocol messages to a single WebSocket client.
 * Protocol: text for HELLO/SET_ORIGIN (easy to debug), binary for CHUNK_SECTION_SNAPSHOT and BLOCK_DELTA.
 */
public class StreamerWebSocketHandler {

	private static final String PROTOCOL_VERSION = "1";
	private static final byte MSG_HELLO = 0;
	private static final byte MSG_SET_ORIGIN = 1;
	private static final byte MSG_CHUNK_SECTION_SNAPSHOT = 2;
	private static final byte MSG_BLOCK_DELTA = 3;

	private final WebSocket socket;
	private final StreamerServer streamerServer;

	public StreamerWebSocketHandler(WebSocket socket, StreamerServer streamerServer) {
		this.socket = socket;
		this.streamerServer = streamerServer;
	}

	public void sendHello() {
		if (socket.isOpen()) {
			socket.send("HELLO " + PROTOCOL_VERSION);
		}
	}

	public void sendSetOrigin(int x0, int y0, int z0, double scale) {
		if (socket.isOpen()) {
			socket.send("SET_ORIGIN " + x0 + " " + y0 + " " + z0 + " " + scale);
		}
	}

	public void sendChunkSectionSnapshot(int cx, int cz, int sy, List<String> palette, short[] indices) {
		if (!socket.isOpen()) return;
		try {
			ByteArrayOutputStream baos = new ByteArrayOutputStream();
			DataOutputStream out = new DataOutputStream(baos);
			out.writeByte(MSG_CHUNK_SECTION_SNAPSHOT);
			out.writeInt(cx);
			out.writeInt(cz);
			out.writeInt(sy);
			out.writeInt(palette.size());
			for (String s : palette) {
				byte[] b = s.getBytes(StandardCharsets.UTF_8);
				out.writeShort(b.length);
				out.write(b);
			}
			for (int i = 0; i < 4096; i++) {
				out.writeShort(indices[i] & 0xFFFF);
			}
			out.flush();
			socket.send(baos.toByteArray());
		} catch (IOException e) {
			MicrocosmStreamerMod.LOGGER.warn("Failed to send chunk section snapshot", e);
		}
	}

	public void sendBlockDelta(int x, int y, int z, String blockStateId) {
		if (!socket.isOpen()) return;
		try {
			ByteArrayOutputStream baos = new ByteArrayOutputStream();
			DataOutputStream out = new DataOutputStream(baos);
			out.writeByte(MSG_BLOCK_DELTA);
			out.writeInt(x);
			out.writeInt(y);
			out.writeInt(z);
			byte[] b = blockStateId.getBytes(StandardCharsets.UTF_8);
			out.writeShort(b.length & 0xFFFF);
			out.write(b);
			out.flush();
			socket.send(baos.toByteArray());
		} catch (IOException e) {
			MicrocosmStreamerMod.LOGGER.warn("Failed to send block delta", e);
		}
	}
}

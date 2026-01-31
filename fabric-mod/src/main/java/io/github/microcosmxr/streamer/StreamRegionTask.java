package io.github.microcosmxr.streamer;

import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.chunk.ChunkAccess;
import net.minecraft.world.level.chunk.LevelChunkSection;

/**
 * Sends chunk section snapshots for a region around the stream origin to a newly connected client.
 * Run on the server (game) thread via server.execute().
 */
public final class StreamRegionTask {

	private static final int CHUNK_RADIUS_XZ = 2;  // stream 2 chunks each direction (5×5 = 25 chunks)
	private static final int SECTION_RANGE = 8;    // stream 8 sections up/down from origin section

	private StreamRegionTask() {}

	public static void streamRegionToClient(MinecraftServer server, StreamerServer streamerServer, StreamerWebSocketHandler client) {
		ServerLevel level = server.overworld();
		if (level == null) return;

		int ox = streamerServer.getOriginX();
		int oy = streamerServer.getOriginY();
		int oz = streamerServer.getOriginZ();
		int originChunkX = ox >> 4;
		int originChunkZ = oz >> 4;
		int minY = level.dimensionType().minY();
		int originSectionIndex = (oy - minY) >> 4;

		int sent = 0;
		for (int dx = -CHUNK_RADIUS_XZ; dx <= CHUNK_RADIUS_XZ; dx++) {
			for (int dz = -CHUNK_RADIUS_XZ; dz <= CHUNK_RADIUS_XZ; dz++) {
				int cx = originChunkX + dx;
				int cz = originChunkZ + dz;
				ChunkAccess chunk = level.getChunk(cx, cz);
				if (chunk == null) continue;

				int sectionCount = chunk.getSections().length;
				int syStart = Math.max(0, originSectionIndex - SECTION_RANGE);
				int syEnd = Math.min(sectionCount - 1, originSectionIndex + SECTION_RANGE);

				for (int sy = syStart; sy <= syEnd; sy++) {
					LevelChunkSection section = chunk.getSection(sy);
					if (section == null || section.hasOnlyAir()) continue;

					ChunkSerializer.SectionSnapshot snap = ChunkSerializer.serializeSection(chunk, sy);
					client.sendChunkSectionSnapshot(cx, cz, sy, snap.palette, snap.indices);
					sent++;
				}
			}
		}
		MicrocosmStreamerMod.LOGGER.info("Streamed {} chunk sections to new client (origin {} {} {})", sent, ox, oy, oz);
	}
}

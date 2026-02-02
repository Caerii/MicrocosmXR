package io.github.microcosmxr.streamer;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.core.registries.Registries;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.level.chunk.ChunkAccess;
import net.minecraft.world.level.chunk.LevelChunk;
import net.minecraft.world.level.chunk.LevelChunkSection;
import net.minecraft.world.level.entity.EntityTypeTest;
import net.minecraft.world.phys.AABB;

/**
 * Sends chunk section snapshots, block entities, and entities for a region around the stream origin to a newly connected client.
 * Run on the server (game) thread via server.execute().
 */
public final class StreamRegionTask {

	/** Chunk radius in X/Z (e.g. 4 → 9×9 = 81 chunks). Kept smaller for headset performance. */
	private static final int CHUNK_RADIUS_XZ = 4;
	/** Section range up/down from origin section. Kept smaller for headset performance. */
	private static final int SECTION_RANGE = 8;
	private static final int ENTITY_RADIUS = 64;   // stream entities within this block radius of origin

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

		int sentSections = 0;
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

					ChunkSerializer.SectionSnapshot snap = ChunkSerializer.serializeSection(level, chunk, sy);
					client.sendChunkSectionSnapshot(cx, cz, sy, snap);
					sentSections++;
				}

				// Block entities in this chunk (only LevelChunk has block entity map)
				if (chunk instanceof LevelChunk levelChunk) {
					int sectionWorldYMin = minY + syStart * 16;
					int sectionWorldYMax = minY + syEnd * 16 + 15;
					levelChunk.getBlockEntities().forEach((pos, blockEntity) -> {
						if (pos.getY() >= sectionWorldYMin && pos.getY() <= sectionWorldYMax) {
							String typeId = level.registryAccess().lookupOrThrow(Registries.BLOCK_ENTITY_TYPE).getKey(blockEntity.getType()).toString();
							client.sendBlockEntity(pos.getX(), pos.getY(), pos.getZ(), typeId, null);
						}
					});
				}
			}
		}

		// Entities in range
		AABB aabb = new AABB(
			ox - ENTITY_RADIUS, oy - ENTITY_RADIUS, oz - ENTITY_RADIUS,
			ox + ENTITY_RADIUS, oy + ENTITY_RADIUS, oz + ENTITY_RADIUS
		);
		List<Entity> entities = new ArrayList<>();
		level.getEntities(EntityTypeTest.forClass(Entity.class), aabb, e -> true, entities);
		int sentEntities = 0;
		for (Entity entity : entities) {
			String typeId = level.registryAccess().lookupOrThrow(Registries.ENTITY_TYPE).getKey(entity.getType()).toString();
			client.sendEntitySpawn(
				entity.getId(),
				typeId,
				entity.getX(), entity.getY(), entity.getZ(),
				entity.getYRot(), entity.getXRot()
			);
			sentEntities++;
		}

		MicrocosmStreamerMod.LOGGER.info("Streamed {} chunk sections, block entities, {} entities to new client (origin {} {} {})", sentSections, sentEntities, ox, oy, oz);
	}
}

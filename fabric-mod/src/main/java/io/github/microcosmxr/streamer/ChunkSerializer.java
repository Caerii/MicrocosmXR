package io.github.microcosmxr.streamer;

import net.minecraft.core.BlockPos;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.chunk.ChunkAccess;
import net.minecraft.world.level.chunk.LevelChunkSection;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Serializes chunk section data into palette + indices for the streaming protocol.
 * Uses Mojang mappings (1.21): ChunkAccess.getSection(int), LevelChunkSection.getBlockState(x,y,z).
 */
public final class ChunkSerializer {

	private static final int SECTION_SIZE = 16 * 16 * 16; // 4096

	private ChunkSerializer() {}

	/**
	 * Build palette (unique block state strings) and indices[4096] for one 16×16×16 section.
	 * Block state string format: same as BlockState.toString() (e.g. "minecraft:stone", "minecraft:oak_planks[axis=z]").
	 */
	public static SectionSnapshot serializeSection(ChunkAccess chunk, int sectionIndex) {
		LevelChunkSection section = chunk.getSection(sectionIndex);
		if (section == null || section.hasOnlyAir()) {
			return new SectionSnapshot(new ArrayList<>(List.of("minecraft:air")), new short[SECTION_SIZE]);
		}

		Map<String, Short> paletteMap = new LinkedHashMap<>();
		short[] indices = new short[SECTION_SIZE];
		List<String> palette = new ArrayList<>();
		short nextId = 0;

		section.acquire();
		try {
			for (int y = 0; y < 16; y++) {
				for (int z = 0; z < 16; z++) {
					for (int x = 0; x < 16; x++) {
						BlockState state = section.getBlockState(x, y, z);
						String key = blockStateToString(state);
						Short id = paletteMap.get(key);
						if (id == null) {
							id = nextId++;
							paletteMap.put(key, id);
							palette.add(key);
						}
						indices[(y * 16 + z) * 16 + x] = id;
					}
				}
			}
		} finally {
			section.release();
		}

		return new SectionSnapshot(palette, indices);
	}

	/** Block state to protocol string (block id + optional state properties). */
	public static String blockStateToString(BlockState state) {
		if (state == null) return "minecraft:air";
		// Use Minecraft's canonical string form for block state (e.g. minecraft:stone, minecraft:oak_planks[axis=z])
		return state.toString();
	}

	public static final class SectionSnapshot {
		public final List<String> palette;
		public final short[] indices;

		public SectionSnapshot(List<String> palette, short[] indices) {
			this.palette = palette;
			this.indices = indices;
		}
	}
}

package io.github.microcosmxr.streamer;

import net.minecraft.core.BlockPos;
import net.minecraft.core.RegistryAccess;
import net.minecraft.core.registries.Registries;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.LightLayer;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.chunk.ChunkAccess;
import net.minecraft.world.level.chunk.LevelChunkSection;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Serializes chunk section data into palette + indices + light + biomes for the streaming protocol.
 * Uses Mojang mappings (1.21): ChunkAccess.getSection(int), LevelChunkSection.getBlockState(x,y,z).
 */
public final class ChunkSerializer {

	private static final int SECTION_SIZE = 16 * 16 * 16; // 4096
	private static final int BIOME_SIZE = 4 * 4 * 4; // 64 biomes per section (4×4×4 quart grid)

	private ChunkSerializer() {}

	/**
	 * Build full section snapshot: block palette + indices, block/sky light, biome palette + indices.
	 * Pass level for light (and for biome registry); level can be null to skip light/biomes.
	 */
	public static SectionSnapshot serializeSection(ServerLevel level, ChunkAccess chunk, int sectionIndex) {
		LevelChunkSection section = chunk.getSection(sectionIndex);
		if (section == null || section.hasOnlyAir()) {
			byte[] emptyLight = new byte[SECTION_SIZE];
			List<String> emptyBiomePalette = new ArrayList<>(List.of("minecraft:plains"));
			short[] emptyBiomeIndices = new short[BIOME_SIZE];
			return new SectionSnapshot(
				new ArrayList<>(List.of("minecraft:air")),
				new short[SECTION_SIZE],
				emptyLight,
				emptyLight,
				emptyBiomePalette,
				emptyBiomeIndices
			);
		}

		Map<String, Short> paletteMap = new LinkedHashMap<>();
		short[] indices = new short[SECTION_SIZE];
		List<String> palette = new ArrayList<>();
		short nextId = 0;

		int chunkX = chunk.getPos().x;
		int chunkZ = chunk.getPos().z;
		int minY = level != null ? level.dimensionType().minY() : -64;
		int sectionWorldY = minY + sectionIndex * 16;
		int sectionMinQuartY = sectionWorldY >> 2;

		byte[] blockLight = new byte[SECTION_SIZE];
		byte[] skyLight = new byte[SECTION_SIZE];

		section.acquire();
		try {
			for (int ly = 0; ly < 16; ly++) {
				for (int lz = 0; lz < 16; lz++) {
					for (int lx = 0; lx < 16; lx++) {
						BlockState state = section.getBlockState(lx, ly, lz);
						String key = blockStateToString(level != null ? level.registryAccess() : null, state);
						Short id = paletteMap.get(key);
						if (id == null) {
							id = nextId++;
							paletteMap.put(key, id);
							palette.add(key);
						}
						indices[(ly * 16 + lz) * 16 + lx] = id;

						if (level != null) {
							BlockPos pos = new BlockPos(chunkX * 16 + lx, sectionWorldY + ly, chunkZ * 16 + lz);
							blockLight[(ly * 16 + lz) * 16 + lx] = (byte) Math.min(15, level.getBrightness(LightLayer.BLOCK, pos));
							skyLight[(ly * 16 + lz) * 16 + lx] = (byte) Math.min(15, level.getBrightness(LightLayer.SKY, pos));
						}
					}
				}
			}
		} finally {
			section.release();
		}

		// Biomes: 4×4×4 quart grid per section
		RegistryAccess regAccess = level != null ? level.registryAccess() : null;
		List<String> biomePalette = new ArrayList<>();
		short[] biomeIndices = new short[BIOME_SIZE];
		Map<String, Short> biomePaletteMap = new LinkedHashMap<>();
		short nextBiomeId = 0;
		for (int qy = 0; qy < 4; qy++) {
			for (int qz = 0; qz < 4; qz++) {
				for (int qx = 0; qx < 4; qx++) {
					int quartX = chunkX * 4 + qx;
					int quartY = sectionMinQuartY + qy;
					int quartZ = chunkZ * 4 + qz;
					String biomeId = getBiomeId(chunk, regAccess, quartX, quartY, quartZ);
					Short bid = biomePaletteMap.get(biomeId);
					if (bid == null) {
						bid = nextBiomeId++;
						biomePaletteMap.put(biomeId, bid);
						biomePalette.add(biomeId);
					}
					biomeIndices[(qy * 4 + qz) * 4 + qx] = bid;
				}
			}
		}

		return new SectionSnapshot(palette, indices, blockLight, skyLight, biomePalette, biomeIndices);
	}

	/** Legacy: no light/biomes (for callers that don't pass level). */
	public static SectionSnapshot serializeSection(ChunkAccess chunk, int sectionIndex) {
		return serializeSection(null, chunk, sectionIndex);
	}

	private static String getBiomeId(ChunkAccess chunk, RegistryAccess registryAccess, int quartX, int quartY, int quartZ) {
		try {
			var holder = chunk.getNoiseBiome(quartX, quartY, quartZ);
			if (registryAccess == null) return "minecraft:plains";
			// Get Registry<Biome> from RegistryAccess (1.21: obtainRegistryOrThrow / lookupOrThrow)
			var registry = registryAccess.lookupOrThrow(Registries.BIOME);
			return holder.unwrap().map(
				key -> key.toString(),
				value -> registry.getKey(value).toString()
			);
		} catch (Exception e) {
			return "minecraft:plains";
		}
	}

	/** Block state to protocol string: always "minecraft:block_id" so the client atlas lookup works. */
	public static String blockStateToString(RegistryAccess registryAccess, BlockState state) {
		if (state == null) return "minecraft:air";
		if (registryAccess != null) {
			try {
				return registryAccess.lookupOrThrow(Registries.BLOCK).getKey(state.getBlock()).toString();
			} catch (Exception ignored) {}
		}
		return state.toString();
	}

	public static final class SectionSnapshot {
		public final List<String> palette;
		public final short[] indices;
		public final byte[] blockLight;
		public final byte[] skyLight;
		public final List<String> biomePalette;
		public final short[] biomeIndices;

		public SectionSnapshot(List<String> palette, short[] indices, byte[] blockLight, byte[] skyLight,
		                       List<String> biomePalette, short[] biomeIndices) {
			this.palette = palette;
			this.indices = indices;
			this.blockLight = blockLight != null ? blockLight : new byte[SECTION_SIZE];
			this.skyLight = skyLight != null ? skyLight : new byte[SECTION_SIZE];
			this.biomePalette = biomePalette != null ? biomePalette : List.of("minecraft:plains");
			this.biomeIndices = biomeIndices != null ? biomeIndices : new short[BIOME_SIZE];
		}

		/** Legacy constructor (no light/biomes). */
		public SectionSnapshot(List<String> palette, short[] indices) {
			this(palette, indices, new byte[SECTION_SIZE], new byte[SECTION_SIZE], List.of("minecraft:plains"), new short[BIOME_SIZE]);
		}
	}
}

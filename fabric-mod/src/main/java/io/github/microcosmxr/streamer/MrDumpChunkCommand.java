package io.github.microcosmxr.streamer;

import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.arguments.IntegerArgumentType;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.core.BlockPos;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.chunk.ChunkAccess;
import net.minecraft.world.level.chunk.LevelChunkSection;

/**
 * Command to verify chunk/block data without a client: /mr_dump_chunk cx cz [sy]
 * Dumps one chunk section's palette and a sample of block states to the server log.
 */
public final class MrDumpChunkCommand {

	private MrDumpChunkCommand() {}

	public static void register(CommandDispatcher<CommandSourceStack> dispatcher) {
		dispatcher.register(
			Commands.literal("mr_dump_chunk")
				.requires(source -> true)  // TODO 1.21.11: permission API differs; restrict via server OP (op <player>) or a permission mod
				.then(Commands.argument("chunkX", IntegerArgumentType.integer())
					.then(Commands.argument("chunkZ", IntegerArgumentType.integer())
						.executes(ctx -> dumpChunk(ctx.getSource(), IntegerArgumentType.getInteger(ctx, "chunkX"), IntegerArgumentType.getInteger(ctx, "chunkZ"), null))
						.then(Commands.argument("sectionY", IntegerArgumentType.integer(0, 15))
							.executes(ctx -> dumpChunk(ctx.getSource(),
								IntegerArgumentType.getInteger(ctx, "chunkX"),
								IntegerArgumentType.getInteger(ctx, "chunkZ"),
								IntegerArgumentType.getInteger(ctx, "sectionY"))))))
		);
	}

	private static int dumpChunk(CommandSourceStack source, int chunkX, int chunkZ, Integer sectionY) {
		ServerLevel level = source.getLevel();
		ChunkAccess chunk = level.getChunk(chunkX, chunkZ);
		if (chunk == null) {
			source.sendFailure(Component.literal("Chunk not loaded."));
			return 0;
		}

		int sectionCount = chunk.getSections().length;
		int startIdx = sectionY != null ? sectionY : 0;
		int endIdx = sectionY != null ? sectionY : Math.max(0, sectionCount - 1);

		for (int idx = startIdx; idx <= endIdx && idx < sectionCount; idx++) {
			LevelChunkSection section = chunk.getSection(idx);
			if (section == null || section.hasOnlyAir()) {
				MicrocosmStreamerMod.LOGGER.info("[mr_dump_chunk] cx={} cz={} sectionIndex={} -> empty/air", chunkX, chunkZ, idx);
				continue;
			}

			ChunkSerializer.SectionSnapshot snap = ChunkSerializer.serializeSection(chunk, idx);
			MicrocosmStreamerMod.LOGGER.info("[mr_dump_chunk] cx={} cz={} sectionIndex={} palette size={} palette={}",
				chunkX, chunkZ, idx, snap.palette.size(), snap.palette);
			// Log first few block indices as sample
			StringBuilder sample = new StringBuilder();
			for (int i = 0; i < Math.min(32, snap.indices.length); i++) {
				if (i > 0) sample.append(",");
				sample.append(snap.indices[i]);
			}
			MicrocosmStreamerMod.LOGGER.info("[mr_dump_chunk] sectionIndex={} indices sample (first 32): {}", idx, sample);
		}

		source.sendSuccess(() -> Component.literal("Dumped chunk " + chunkX + "," + chunkZ + " to server log. Check server console."), true);
		return 1;
	}
}

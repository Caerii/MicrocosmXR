package io.github.microcosmxr.streamer;

import net.fabricmc.fabric.api.event.player.PlayerBlockBreakEvents;
import net.minecraft.core.BlockPos;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.block.state.BlockState;

/**
 * Listens for block break/place and broadcasts BLOCK_DELTA to streamer clients when the block is in range of the origin.
 */
public final class BlockDeltaCallback {

	private static final int BLOCK_RADIUS = 64;  // only send deltas within this manhattan-ish range of origin

	private BlockDeltaCallback() {}

	public static void register() {
		PlayerBlockBreakEvents.AFTER.register((world, player, pos, state, blockEntity) -> {
			if (!(world instanceof ServerLevel)) return;
			StreamerServer server = MicrocosmStreamerMod.getStreamerServer();
			if (server == null) return;
			if (!inRange(server, pos.getX(), pos.getY(), pos.getZ())) return;
			// Block broken -> new state is air
			server.broadcastBlockDelta(pos.getX(), pos.getY(), pos.getZ(), "minecraft:air");
		});
		// Block place: can be added later via Fabric BlockPlaceCallback or mixin on setBlockState
	}

	private static boolean inRange(StreamerServer server, int x, int y, int z) {
		int dx = Math.abs(x - server.getOriginX());
		int dy = Math.abs(y - server.getOriginY());
		int dz = Math.abs(z - server.getOriginZ());
		return dx <= BLOCK_RADIUS && dy <= BLOCK_RADIUS && dz <= BLOCK_RADIUS;
	}

	/** Call this when a block is set (e.g. from a mixin or world change listener) to broadcast the new state. */
	public static void onBlockSet(int x, int y, int z, BlockState newState) {
		StreamerServer server = MicrocosmStreamerMod.getStreamerServer();
		if (server == null) return;
		if (!inRange(server, x, y, z)) return;
		server.broadcastBlockDelta(x, y, z, ChunkSerializer.blockStateToString(newState));
	}
}

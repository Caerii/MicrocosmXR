package io.github.microcosmxr.streamer.mixin;

import net.minecraft.core.BlockPos;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.state.BlockState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

import io.github.microcosmxr.streamer.BlockDeltaCallback;

/**
 * When a block is set on the server, broadcast BLOCK_DELTA to streamer clients if in range.
 */
@Mixin(Level.class)
public abstract class LevelSetBlockMixin {

	@Inject(method = "setBlock(Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/level/block/state/BlockState;II)Z", at = @At("RETURN"))
	private void onSetBlock(BlockPos pos, BlockState newState, int flags, int recursionLeft, CallbackInfoReturnable<Boolean> cir) {
		if (!((Object) this instanceof ServerLevel)) return;
		if (!cir.getReturnValueZ()) return; // block wasn't actually set
		BlockDeltaCallback.onBlockSet((ServerLevel) (Object) this, pos.getX(), pos.getY(), pos.getZ(), newState);
	}
}

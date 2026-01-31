package io.github.microcosmxr.streamer;

import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.arguments.IntegerArgumentType;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.network.chat.Component;

public final class MrStartCommand {

	private MrStartCommand() {}

	public static void register(CommandDispatcher<CommandSourceStack> dispatcher) {
		dispatcher.register(
			Commands.literal("mr_start")
				.requires(source -> true)  // TODO 1.21.11: permission API differs; restrict via server OP (op <player>) or a permission mod
				.then(Commands.argument("x", IntegerArgumentType.integer())
					.then(Commands.argument("y", IntegerArgumentType.integer())
						.then(Commands.argument("z", IntegerArgumentType.integer())
							.executes(ctx -> {
								int x = IntegerArgumentType.getInteger(ctx, "x");
								int y = IntegerArgumentType.getInteger(ctx, "y");
								int z = IntegerArgumentType.getInteger(ctx, "z");
								StreamerServer server = MicrocosmStreamerMod.getStreamerServer();
								if (server == null) {
									ctx.getSource().sendFailure(Component.literal("Microcosm Streamer server not running."));
									return 0;
								}
								server.setOrigin(x, y, z);
								ctx.getSource().sendSuccess(() -> Component.literal("Stream origin set to " + x + ", " + y + ", " + z), true);
								return 1;
							}))))
		);
	}
}

package io.github.microcosmxr.streamer;

import net.fabricmc.api.DedicatedServerModInitializer;
import net.fabricmc.fabric.api.command.v2.CommandRegistrationCallback;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class MicrocosmStreamerMod implements DedicatedServerModInitializer {

	public static final String MOD_ID = "microcosm-streamer";
	public static final Logger LOGGER = LoggerFactory.getLogger(MOD_ID);

	private static StreamerServer streamerServer;

	@Override
	public void onInitializeServer() {
		LOGGER.info("Microcosm Streamer initializing (server-side)");

		ServerLifecycleEvents.SERVER_STARTED.register(server -> {
			streamerServer = new StreamerServer(server, 25566);
			streamerServer.start();
			LOGGER.info("Microcosm Streamer WebSocket server listening on port 25566");
		});

		ServerLifecycleEvents.SERVER_STOPPING.register(server -> {
			if (streamerServer != null) {
				streamerServer.stop();
				streamerServer = null;
			}
		});

		CommandRegistrationCallback.EVENT.register((dispatcher, registryAccess, environment) -> {
			MrStartCommand.register(dispatcher);
			MrDumpChunkCommand.register(dispatcher);
		});

		BlockDeltaCallback.register();
	}

	public static StreamerServer getStreamerServer() {
		return streamerServer;
	}
}

# Streaming verification — what you can get

Summary of what the Fabric mod sends and how to verify it (Node test client + Unity).

## Data the mod sends over WebSocket

| Data | Message | What you get |
|------|---------|--------------|
| Handshake | Text `HELLO 1`, `SET_ORIGIN x y z scale` | Protocol version and stream origin + scale. |
| Chunk sections | Binary `CHUNK_SECTION_SNAPSHOT` (type 2) | Block palette + 4096 indices; optional 4096 block light + 4096 sky light; optional biome palette + 64 indices. (Legacy servers send blocks only; parser accepts both.) |
| Block changes | Binary `BLOCK_DELTA` (type 3) | World (x,y,z) and new block state ID on break/place. |
| Block entities | Binary `BLOCK_ENTITY` (type 4) | (x,y,z), type ID, optional NBT bytes. |
| Entity spawns | Binary `ENTITY_SPAWN` (type 5) | Entity id, type ID, position (x,y,z), yaw, pitch. |

## How to verify

1. **Build mod** (Java 21): `cd fabric-mod; .\gradlew.bat build`
2. **Deploy:** Copy `fabric-mod/build/libs/microcosm-streamer-0.1.0.jar` and Fabric API into the server `mods/` folder; start the server and load a world.
3. **Set origin:** `/mr_start <x> <y> <z>` (e.g. your position).
4. **Node test client:** `cd fabric-mod; pnpm install; node test-client.js localhost 25566`
   - You should see HELLO/SET_ORIGIN (as text or binary), then many `CHUNK_SECTION_SNAPSHOT` lines.
   - Break/place a block to see `BLOCK_DELTA`.
   - Block entities and entities in range appear as type 4/5 when the mod sends them.
5. **Unity:** Use `MinecraftStreamBehaviour` + `ChunkStreamClient`. The parser handles extended snapshots (light, biomes) and BLOCK_ENTITY/ENTITY_SPAWN. Subscribe to `OnSectionReceived`, `OnBlockDeltaReceived`, `OnBlockEntityReceived`, `OnEntitySpawnReceived` for meshing and entity placement.

## Note on legacy format

If the server runs an older mod JAR, chunk messages may omit light and biomes (blocks only). The Node test client and Unity parser accept both; after deploying the latest mod, you get full light and biomes in snapshots.

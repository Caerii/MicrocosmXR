# Minecraft chunk streaming client (Unity)

Skeleton client that connects to the Fabric mod WebSocket server and receives chunk/block data for rendering in MR.

## Setup

1. **Add to scene:** Create an empty GameObject and add `MinecraftStreamBehaviour`. Set **Host** (e.g. `localhost` or your PC’s LAN IP when running on Quest) and **Port** (default `25566`).
2. **Connect:** Either check **Connect On Start** or call `MinecraftStreamBehaviour.Connect()` from a button/script.
3. **Meshing (stub):** Add `ChunkMesherStub` to the same (or another) GameObject and assign **Stream** to the `MinecraftStreamBehaviour`. The stub subscribes to section/block updates; replace `BuildSectionMesh` with real visible-faces + greedy meshing and atlas UVs.

## Scripts

| Script | Purpose |
|--------|--------|
| `Protocol.cs` | Message type constants, `ChunkSectionCoord`, default port. |
| `SectionData.cs` | One 16×16×16 section: block palette + indices, block/sky light (4096 bytes each), biome palette + 64 indices. |
| `ChunkStreamParser.cs` | Parses text (HELLO, SET_ORIGIN) and binary (CHUNK_SECTION_SNAPSHOT, BLOCK_DELTA, BLOCK_ENTITY, ENTITY_SPAWN). |
| `ChunkStreamClient.cs` | Holds origin, scale, sections, dirty set; events for section, block delta, block entity, entity spawn. Feed with parser; read sections and dirty list for meshing. |
| `MinecraftStreamConnection.cs` | WebSocket client using `System.Net.WebSockets.ClientWebSocket`; feeds parser → client. |
| `MinecraftStreamBehaviour.cs` | MonoBehaviour: host/port, Connect/Disconnect, exposes `Client`. |
| `ChunkMesherStub.cs` | Subscribes to client; stub for building meshes from section data (TODO: full mesher). |

## Data flow

1. `MinecraftStreamBehaviour` connects to `ws://host:25566`.
2. Server sends HELLO, SET_ORIGIN, then CHUNK_SECTION_SNAPSHOT (blocks + light + biomes), BLOCK_DELTA, BLOCK_ENTITY, and ENTITY_SPAWN (binary).
3. `MinecraftStreamConnection` receives messages and calls `ChunkStreamParser.ParseText` / `ParseBinary` → `ChunkStreamClient`.
4. Client stores origin/scale and section data; marks sections dirty when they change.
5. Mesher (you implement) consumes dirty sections, builds meshes, positions at `TableAnchor + scale * (sectionWorldMc - origin_mc)`.

## Quest / Android

`ClientWebSocket` may not be available on all Unity platforms (e.g. some Android/IL2CPP builds). If connection fails on Quest, use a WebSocket package that supports Unity (e.g. [NativeWebSocket](https://github.com/endel/NativeWebSocket)) and feed the same `ChunkStreamClient` via `ChunkStreamParser.ParseText` / `ParseBinary` from your receive handler.

## Protocol reference

See repo root `docs/chunk_streaming_setup.md` and `fabric-mod/README.md`.

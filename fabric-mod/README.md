# Microcosm Streamer (Fabric mod)

Server-side Fabric mod for **Minecraft 1.21.11** that streams chunk and block data to Unity (MicrocosmXR) over WebSocket so you can render the world in mixed reality on Quest 3.

## What it does

- **WebSocket server** on port **25566** (configurable in code). Unity or any client can connect to receive:
  - `HELLO` + `SET_ORIGIN` (text) on connect
  - `CHUNK_SECTION_SNAPSHOT` (binary): palette + 4096 indices per 16×16×16 section
  - `BLOCK_DELTA` (binary): single block change (x, y, z, blockStateId)
- **Commands**
  - `/mr_start <x> <y> <z>` — set stream origin (region center). Requires OP 2.
  - `/mr_dump_chunk <chunkX> <chunkZ> [sectionIndex]` — dump one chunk’s section data to server log to **verify blocks** without a client.
- **Block break** — when a block is broken in range of the origin, a `BLOCK_DELTA` is sent to all connected clients.

## Verify you’re getting blocks (no Unity yet)

1. **Build the mod** (requires Java 21 in `JAVA_HOME` or first in `PATH`)
   ```powershell
   cd fabric-mod; .\gradlew.bat build
   ```
   Output: `build/libs/microcosm-streamer-0.1.0.jar`. If you see "Gradle requires JVM 17 or later" / "JVM 8", set `JAVA_HOME` to your JDK 21 install.

2. **Run a Fabric server** (1.21.11) with this mod + Fabric API in `mods/`. Start the server and load a world.

3. **Dump a chunk to log**
   - Go to a place with blocks (e.g. spawn).
   - Run: `/mr_dump_chunk 0 0`
   - Check server console: you should see `[mr_dump_chunk] cx=0 cz=0 ... palette size=... palette=[minecraft:stone, ...]` and an indices sample. That confirms chunk/section and block state serialization.

4. **Connect a WebSocket client**
   - One-liner (pnpm, semicolons): `cd fabric-mod; pnpm install; pnpm run test`
   - Or with custom host/port: `pnpm run test:host -- <host> <port>` (e.g. `pnpm run test:host -- localhost 25566`).
   - You should receive text: `HELLO 1` and `SET_ORIGIN 0 64 0 0.02`.
   - Then the server will send binary `CHUNK_SECTION_SNAPSHOT` messages for chunks around the origin. Set origin first with `/mr_start <x> <y> <z>` (e.g. your current position), then connect so the streamed region contains loaded chunks.

## Protocol (for your teammate / Unity)

- **Text**
  - `HELLO <protocolVersion>`
  - `SET_ORIGIN <x0> <y0> <z0> <scale>`
- **Binary**
  - **CHUNK_SECTION_SNAPSHOT** (msg type 2): `byte type=2`, `int cx, cz, sy`, `int paletteLen`, then for each palette entry: `short len`, `utf8 bytes`, then 4096 × `short` indices.
  - **BLOCK_DELTA** (msg type 3): `byte type=3`, `int x, y, z`, `short len`, `utf8 blockStateId`.

Block state IDs are strings like `minecraft:stone`, `minecraft:oak_planks[axis=z]` (same as Minecraft `BlockState.toString()`).

## Build

**Requires Java 21.** Set `JAVA_HOME` to your Java 21 install (or put Java 21 first in `PATH`), then:

**Windows (PowerShell or CMD):**
```powershell
.\gradlew.bat build
```

**macOS/Linux:**
```bash
./gradlew build
```

Output JAR: `build/libs/microcosm-streamer-0.1.0.jar`. Put it and Fabric API into your server’s `mods/` folder.

## Requirements

- **Java 21** (required for build and for Minecraft 1.21.11). [Install JDK 21](https://adoptium.net/) and set `JAVA_HOME` or ensure `java -version` shows 21.
- **Minecraft** 1.21.11  
- **Fabric Loader** 0.18.x  
- **Fabric API** (1.21.11)

## Project layout

- `src/main/java/.../streamer/` — mod entry, WebSocket server, chunk serialization, commands, block-delta callback.
- `docs/chunk_streaming_setup.md` (in repo root) — full architecture and protocol.

## Run and test (two terminals)

**Terminal 1 — start Minecraft server with the mod**

- Option A: Use a Fabric 1.21.11 server; put `microcosm-streamer-0.1.0.jar` and Fabric API in `mods/`; start the server; accept EULA if needed.
- Option B: Dev server (Java 21 in PATH): `cd fabric-mod; .\gradlew.bat runServer`  
  - First run may download assets and create `run/`; accept EULA in `run/eula.txt` if prompted.

**Terminal 2 — run the test client**

```powershell
cd fabric-mod; pnpm install; pnpm run test
```

- If the server is running and the mod is loaded, you should see `Connected.`, then `HELLO 1`, `SET_ORIGIN ...`, and `CHUNK_SECTION_SNAPSHOT` lines.
- If nothing is listening on 25566, you’ll see `Error:` and `Disconnected.` (start the server first).

## Next steps

- Your teammate: implement Unity WebSocket client, parse binary messages, build `ChunkData` and meshes (see `docs/chunk_streaming_setup.md`).
- Optional: add block *place* events so BLOCK_DELTA is sent when blocks are placed in range.
- Optional: make port and region size configurable (e.g. config file or more commands).

# Web Visualizer

Vite + TypeScript + React Three Fiber frontend that visualizes the Minecraft stream from the Fabric mod. Use it to iterate on block streaming and rendering without touching Unity.

## Layout

- **`shared/`** — Protocol types and parser (Fabric mod WebSocket messages). Used by server and client.
- **`server/`** — Node server: WebSocket relay to the Fabric mod, serves client in production.
- **`client/`** — Vite + React + R3F app: connects to relay, holds world state, renders chunks.

## Prerequisites

- Node 18+
- pnpm (or use corepack: `corepack enable && corepack prepare pnpm@latest --activate`)
- Fabric mod running (Minecraft server with microcosm-streamer, origin set via `/mr_start`)

## Setup

```bash
cd web-visualizer
pnpm install
```

## Run (dev)

1. **Build shared** (once, or when protocol changes):
   ```bash
   pnpm --filter shared build
   ```

2. **Start the relay server** (connects to mod at `localhost:25566`):
   ```bash
   pnpm dev:server
   ```
   Optional env: `MOD_HOST`, `MOD_PORT`, `PORT` (default 3000).

3. **Start the client** (Vite dev server at http://localhost:5173):
   ```bash
   pnpm dev:client
   ```
   This builds `shared` if needed, then starts Vite. The client proxies `/ws` to the relay server.

4. In the browser: open http://localhost:5173, click **Connect**. You should see chunk sections as textured meshes (visible faces only); orbit with mouse.

## Quest 3 + WebXR AR (table preview)

You can view the same scene in **passthrough AR** on a Quest 3 to see how the mesh looks on your table.

1. **Same LAN:** Quest and your PC must be on the same Wi‑Fi.
2. **Start dev as above:** `pnpm dev:server` and `pnpm dev:client`. The client is bound to all interfaces (`host: true`) so Quest can reach it.
3. **PC IP:** On your PC, get your local IP (e.g. `ipconfig` → IPv4, or `hostname -I` on Linux).
4. **On Quest:** Open the **Meta Quest Browser**, go to `http://YOUR_PC_IP:5173` (e.g. `http://192.168.1.100:5173`). Click **Connect**, then **Enter AR**. Allow camera/permissions if prompted.
5. The world is drawn at table scale (~1 m) in front of you; use **Exit AR** to return.

WebSocket goes through the same URL (Vite proxies `/ws` to the relay on your PC), so no extra relay config is needed.

### Quest + ngrok (HTTPS for WebXR)

Many browsers require a **secure context (HTTPS)** for WebXR immersive AR. If `http://YOUR_PC_IP:5173` fails on Quest (e.g. "only secure contexts" or AR not starting), use **ngrok** so the frontend is served over HTTPS:

1. **Tunnel the client:** `ngrok http 5173` (or sign up and use `ngrok config add-authtoken ...` if needed).
2. **Open the HTTPS URL** ngrok gives you (e.g. `https://abc123.ngrok-free.app`) in the Quest browser.
3. Click **Connect**, then **Enter AR**. The client uses `wss://` when the page is `https://`, so the WebSocket goes through the same tunnel; relay and mod stay on your PC.

Keep **relay** (`pnpm dev:server`) and **Minecraft server** running on your PC; only the browser/Quest talks to your machine via ngrok.

## Production

```bash
pnpm build
pnpm start
```

Serves the built client from the server; connect to `http://localhost:3000`. WebSocket: `ws://localhost:3000/ws`.

## Scale (Minecraft in the renderer)

The client uses **1 unit = 1 Minecraft block** (1 block = 1 meter in MC). So:

- **Chunk** = 16×16 blocks per section; one section = 16×16×16 units in the scene.
- **Mesh** = visible faces only (no inner faces); each face uses the atlas texture.
- **Camera** = 100 units from origin by default so ~5 chunks (80 units) fit in view.

The server’s `SET_ORIGIN` scale is not used for display; the renderer always uses this fixed scale so chunk dimensions match Minecraft.

## Textures (Minecraft-style)

The client renders **visible faces only** with a **texture atlas** (512×512, 32×32 tiles of 16×16 px each).

- **Real Minecraft textures (recommended):** From the repo root, run:
  ```bash
  cd web-visualizer
  pnpm build-atlas
  ```
  Or with a custom path/version:
  ```bash
  cd web-visualizer/scripts && pnpm install && node build-atlas.mjs "C:\Users\...\\.minecraft" 1.21.11
  ```
  This reads block textures from your Minecraft version JAR (`assets/minecraft/textures/block/`), builds `client/public/atlas.png` and `client/src/blockAtlas.generated.ts`. Uses your `.minecraft` path (default: `%APPDATA%\.minecraft`) and version folder (default: `1.21.11`).
- **Default:** If `public/atlas.png` is missing, a **placeholder atlas** (colored tiles) is used so blocks still look distinct.
- **Adding blocks:** After running `build-atlas`, block IDs map automatically from texture filenames (e.g. `stone.png` → `minecraft:stone`). To add fallbacks before generating, edit `BLOCK_TILE_FALLBACK` in `client/src/blockAtlas.ts`.

## Protocol

Same as Fabric mod: text `HELLO`, `SET_ORIGIN`; binary `CHUNK_SECTION_SNAPSHOT`, `BLOCK_DELTA`, `BLOCK_ENTITY`, `ENTITY_SPAWN`. See `fabric-mod/README.md` and `docs/streaming_verification.md`.

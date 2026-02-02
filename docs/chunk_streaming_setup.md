# Chunk Streaming into Unity MR — Proper Setup

This doc is the single source of truth for **streaming Minecraft chunks into the MicrocosmXR Unity scene** on Quest 3 (co-located mixed reality). It aligns with the Fabric 1.21.11 approach and the “Minecraft as authoritative simulation, Unity as replicated renderer” architecture.

---

## 1) Core idea

- **Minecraft (Java, 1.21.11)** = authoritative world state (server or client-hosted).
- **Fabric mod** = “world state exporter”: reads chunks/blocks/entities, sends snapshots + deltas.
- **Unity (Quest 3 MR)** = replicated voxel renderer: receives data, builds meshes, renders on the table.
- **Photon** = use only for co-location (shared anchor) and player presence; **not** for chunk payloads.

You do **not** need a separate Node server or FastAPI for the core streaming pipeline. Start with **WebSocket from inside the Fabric mod** to Unity.

---

## 2) Where Node / FastAPI / pnpm fit (and don’t)

| Use case | Use Node/FastAPI? | Notes |
|----------|-------------------|--------|
| **Chunk/block/entity streaming** | **No (MVP)** | Fabric mod → WebSocket → Unity. Simplest and lowest latency on LAN. |
| Session host UI, matchmaking, room codes | Optional later | Node can host a small “lobby” or admin UI. |
| REST API (list worlds, set origin, save replay) | Optional later | FastAPI is fine for that. |
| High-frequency binary chunk data | No | Keep streaming in the Fabric ↔ Unity path; don’t route through HTTP. |

So: **skip Node + FastAPI for “streaming chunks into Unity.”** Add them later if you want a control plane or dashboard.

---

## 3) Architecture diagram (MVP)

```
┌─────────────────────────────────────────────────────────────────┐
│  Minecraft 1.21.11 (Java)                                        │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Fabric mod (server-side or integrated server)                ││
│  │  • Picks stream origin (e.g. /mr_start x y z)                ││
│  │  • Interest: fixed region (e.g. 32×32×16 or 64×64×32)       ││
│  │  • On chunk load → send CHUNK_SECTION_SNAPSHOT                ││
│  │  • On block change → send BLOCK_DELTA                        ││
│  │  • Optional: ENTITY_SPAWN / UPDATE / DESPAWN                 ││
│  └───────────────────────────┬─────────────────────────────────┘│
└──────────────────────────────┼──────────────────────────────────┘
                               │ WebSocket (binary frames)
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  Unity (Quest 3 MR)                                              │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Chunk streaming client                                       ││
│  │  • Connects to Minecraft mod’s WebSocket server               ││
│  │  • Dictionary<ChunkCoord, ChunkData>                         ││
│  │  • On SNAPSHOT → store section palette + indices → mark dirty││
│  │  • On DELTA → patch chunk → mark chunk dirty                 ││
│  └───────────────────────────┬─────────────────────────────────┘│
│                              │                                   │
│  ┌───────────────────────────▼─────────────────────────────────┐│
│  │ Chunk mesher (per chunk or per section)                      ││
│  │  • Only visible faces (cull internal)                        ││
│  │  • Greedy meshing (merge coplanar same-material faces)       ││
│  │  • One mesh per chunk/section, atlas material                 ││
│  └───────────────────────────┬─────────────────────────────────┘│
│                              │                                   │
│  ┌───────────────────────────▼─────────────────────────────────┐│
│  │ Table anchor (shared via Photon)                            ││
│  │  • P_unity = TableAnchor + scale * (P_mc - origin_mc)        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

---

## 4) Minimal message protocol (binary over WebSocket)

Use **binary frames** (not JSON) for snapshots/deltas. Text is fine for HELLO/SET_ORIGIN if you want quick debugging.

| Message | Direction | Purpose |
|---------|-----------|---------|
| `HELLO` | Either | Protocol version, optional capabilities. |
| `SET_ORIGIN` | Server → Client | `(x0, y0, z0, scale)` — Minecraft origin and block-to-Unity scale. |
| `CHUNK_SECTION_SNAPSHOT` | Server → Client | One 16×16×16 section: `(cx, cz, sy)`, block palette + 4096 indices, block/sky light (4096 bytes each), biome palette + 64 indices. |
| `BLOCK_DELTA` | Server → Client | Single block change: `(x, y, z)`, new block state ID. Sent on break and place. |
| `BLOCK_ENTITY` | Server → Client | Block entity at `(x, y, z)` with type ID and optional NBT. |
| `ENTITY_SPAWN` | Server → Client | `(id, type, x, y, z, yaw, pitch)` — sent for entities in range when client connects. |
| `ENTITY_UPDATE` | Server → Client | `(id, x, y, z, yaw, pitch)` — optional later. |
| `ENTITY_DESPAWN` | Server → Client | `(id)` — optional later. |

**Chunk section format (implemented):**

- **Chunk coords:** `cx`, `cz` (int), `sy` (section Y index).
- **Block palette:** list of block state IDs (e.g. `minecraft:stone`). Length-prefixed UTF-8 strings.
- **Block indices:** 4096 × `ushort` (one per block in the section).
- **Light:** 4096 bytes block light, 4096 bytes sky light (0–15 per block).
- **Biome palette:** list of biome IDs (e.g. `minecraft:plains`). 4×4×4 quart grid = 64 indices × `ushort`.

Start with **HELLO**, **SET_ORIGIN**, **CHUNK_SECTION_SNAPSHOT**, **BLOCK_DELTA**. Add entities once the block pipeline is stable.

---

## 5) Fabric mod responsibilities

- **Stream origin:** e.g. command `/mr_start <x> <y> <z>` or config; defines `(x0, y0, z0)` and optionally scale.
- **Interest region:** e.g. axis-aligned box or radius around origin (e.g. 32×32×16 blocks). Only chunks intersecting this region are streamed.
- **Chunk load:** when a chunk section enters the region and is loaded, serialize its palette + 4096 indices and send `CHUNK_SECTION_SNAPSHOT`.
- **Block updates:** subscribe to block set/change in the region; for each change send `BLOCK_DELTA` with world `(x,y,z)` and new block state (or palette index if client already has that chunk’s palette).
- **WebSocket server:** embedded in the mod; Unity connects to `ws://<server-ip>:<port>`. One connection per Unity client; broadcast same snapshots/deltas to all connected clients if you have multiple Quests.

Use **Mojang mappings** and Loom 1.14 for 1.21.11 so you’re ready for post–1.21.11 unobfuscated versions.

---

## 6) Unity side: getting “chunks into the scene”

1. **Connect** to the Fabric mod’s WebSocket (e.g. on play or via a “Connect to world” button).
2. **Receive** `SET_ORIGIN` → store `origin_mc` and `scale`; use them for all Minecraft → Unity position math.
3. **Receive** `CHUNK_SECTION_SNAPSHOT` → decode into a `ChunkData` (or section) structure:
   - Store palette and indices (e.g. `BlockId[4096]` or section-local array).
   - Map chunk/section key `(cx, cz, sy)` to a `ChunkData` (or `SectionData`) and mark it dirty.
4. **Receive** `BLOCK_DELTA` → compute chunk/section from `(x,y,z)`, update the single block in your stored section, mark that chunk/section dirty.
5. **Meshing (the actual “chunks in the scene”):**
   - For each dirty chunk/section: build a mesh (only visible faces, greedy-mesh same-material quads), assign to a `MeshFilter`/`MeshRenderer` (or combined mesh per chunk).
   - Use one material with a **block texture atlas**; map block state ID → UV rect.
   - Position the chunk in world space: `TableAnchor + scale * (chunk_world_pos - origin_mc)`.
6. **Co-location:** Table anchor transform is shared via Photon (or your chosen MR sync). All clients use the same origin and scale so the Minecraft-derived geometry lines up on the real table.

So “chunks get into the Unity scene” by: **network → ChunkData → dirty flag → mesher → MeshRenderer positioned relative to table anchor.**

---

## 7) Interest management (what to stream)

- **Tabletop MVP:** stream a **fixed region** (e.g. 32×32×16 or 64×64×32 blocks) centered on the chosen origin. No view-frustum or LOD for day one.
- Optionally: stream in a **priority order** (e.g. sections near y=origin first) so the table surface appears before distant vertical sections.

---

## 8) What to use Photon for (and not)

- **Use Photon for:** room/matchmaking, player presence, **shared spatial anchor** (table pose), small RPCs (e.g. “god tool” cursor, teleport).
- **Do not use Photon for:** chunk snapshots and block deltas (payload size and frequency are a bad fit). Keep that on the direct WebSocket from the Fabric mod to each Unity client.

---

## 9) Build order (MVP)

1. **Fabric mod (1.21.11, Mojang mappings)**  
   - WebSocket server (e.g. Jetty or another small Java WebSocket lib).  
   - `/mr_start` (or config) to set origin.  
   - Hook chunk load for a fixed region; send `CHUNK_SECTION_SNAPSHOT` (palette + 4096 indices).  
   - Hook block change in that region; send `BLOCK_DELTA`.

2. **Unity client**  
   - WebSocket client; connect to mod.  
   - Parse `SET_ORIGIN`, `CHUNK_SECTION_SNAPSHOT`, `BLOCK_DELTA`.  
   - `Dictionary<ChunkCoord, ChunkData>` (or per-section); apply snapshots and deltas; set dirty flags.

3. **Unity mesher**  
   - Per-chunk (or per-section) mesh: visible faces only, greedy meshing, one atlas material.  
   - Place chunks using `TableAnchor + scale * (pos_mc - origin_mc)`.

4. **Table anchor + Photon**  
   - One device sets table anchor; share via Photon.  
   - All clients use same origin + scale + anchor so the streamed world lines up on the table.

5. **Later:** entities (spawn/update/despawn), block editing from MR (send place/break to mod, mod applies and sends deltas), then LOD/view-based streaming if needed.

---

## 10) Direct answers to your setup question

- **“Proper way to stream chunks into Unity MR?”**  
  Fabric mod as the only “server” for world state; WebSocket from mod to Unity; Unity maintains ChunkData and turns it into meshes; table pose from Photon.

- **“Node + pnpm + FastAPI + WebSockets?”**  
  Not required for streaming chunks. Use WebSockets **from the Fabric mod** to Unity. Add Node/FastAPI later for session UI or REST if you want.

- **“How do I get the chunks into the Unity scene?”**  
  Receive snapshot/delta → update in-memory ChunkData → mark dirty → mesher builds/updates mesh → MeshRenderer at `TableAnchor + scale * (chunk_pos - origin_mc)`.

This gives you a single reference for the proper chunk-streaming setup and how it fits with Photon and your existing MicrocosmXR goals (Quest 3, 1.21.11, co-located tabletop “micro civilization”).

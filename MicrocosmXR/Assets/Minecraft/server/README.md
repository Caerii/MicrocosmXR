# Minecraft 1.21.11 server with Fabric (for MicrocosmXR streaming)

This folder contains the vanilla server JAR. To run the **Fabric** server with the Microcosm streamer mod (and Fabric API), follow these steps.

## Prerequisites

- **Java 21** (required for Minecraft 1.21.x and Fabric). Set `JAVA_HOME` or have `java` on PATH.
- **Fabric Loader 0.18.x** — when using the Fabric server installer, pick **Loader 0.18.x** (e.g. **0.18.4** is fine; the streamer mod was built against 0.18.1 and is compatible).
- **Fabric API** JAR for 1.21.1/1.21.11 — download from [CurseForge Fabric API](https://www.curseforge.com/minecraft/mc-mods/fabric-api) and pick the version that matches (e.g. `fabric-api-0.139.4+1.21.11.jar` or latest 1.21.1).

## 1. Download the Fabric server launcher (1.21.11, Loader 0.18.4)

Use the **executable server JAR** from [Fabric](https://fabricmc.net/use/server/) — no separate installer needed. In this folder (`Assets/Minecraft/server/`):

**CLI download (PowerShell):**

```powershell
# From this server folder
curl -OJ https://meta.fabricmc.net/v2/versions/loader/1.21.11/0.18.4/1.1.1/server/jar
```

This downloads `fabric-server-mc.1.21.11-loader.0.18.4-launcher.1.1.1.jar` (or similar) into the current directory. If you don’t have `curl`, download the **Executable Server (.jar)** from [fabricmc.net/use/server](https://fabricmc.net/use/server/) with Minecraft **1.21.11**, Loader **0.18.4**, and save it here.

## 2. Create `mods/` and add mods

Create a `mods` folder in this directory if it doesn’t exist. Put these JARs in it:

| Mod | Where to get it |
|-----|------------------|
| **Microcosm Streamer** | Build from repo: `fabric-mod/build/libs/microcosm-streamer-0.1.0.jar`. Use the **Copy mod into server** script below, or copy manually. |
| **Fabric API** | [CurseForge Fabric API](https://www.curseforge.com/minecraft/mc-mods/fabric-api) — choose the JAR for 1.21.1 / 1.21.11. |

**Copy Microcosm Streamer into this server (from repo root):**

```powershell
# From repo root (MicrocosmXR)
copy ..\..\..\fabric-mod\build\libs\microcosm-streamer-0.1.0.jar .\mods\
```

Or run the provided script (see below).

## 3. Accept EULA and start the server

1. The first time you run the Fabric server, it will create `eula.txt`. Open it and set `eula=true` to accept the Minecraft EULA.
2. Launch the server (2GB RAM; increase `-Xmx2G` if needed):
   ```powershell
   java -Xmx2G -jar fabric-server-mc.1.21.11-loader.0.18.4-launcher.1.1.1.jar nogui
   ```
   Use the exact JAR name you downloaded if it differs (e.g. from the curl command above).
3. When the world is loaded, in-game or via RCON run:
   ```
   /mr_start <x> <y> <z>
   ```
   (Use your player position so the streamed region has loaded chunks.) The mod will listen for WebSocket connections on **port 25566**.

## 4. Connect Unity / test client

- **Unity:** Use the streaming client in `Assets/Minecraft/Streaming/`; set the server host/port (e.g. `localhost` / `25566` or your PC’s LAN IP).
- **Node test client:** From repo root: `cd fabric-mod; pnpm run test` (connects to `localhost:25566`).

## Folder layout after setup

```
server/
  fabric-server-mc.1.21.11-loader.0.18.4-launcher.1.1.1.jar
  mods/
    microcosm-streamer-0.1.0.jar
    fabric-api-*.jar
  eula.txt
  ...
```

## Troubleshooting

- **“Gradle requires JVM 17+”** when building the mod — use Java 21 and set `JAVA_HOME` (see repo root `.vscode/settings.json` or main README).
- **Mod not loading** — Ensure both Fabric API and Microcosm Streamer are in `mods/` and the server is Fabric 1.21.1/1.21.11.
- **No chunks streaming** — Run `/mr_start <x> <y> <z>` in-game so the origin is set and the region is loaded; then connect the client.

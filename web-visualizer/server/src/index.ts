import express from "express";
import { createServer } from "http";
import { WebSocketServer, WebSocket } from "ws";
import path from "path";
import { fileURLToPath } from "url";
import { DEFAULT_MOD_PORT } from "@web-visualizer/shared";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const HTTP_PORT = Number(process.env.PORT) || 3000;
const MOD_HOST = process.env.MOD_HOST || "localhost";
const MOD_PORT = Number(process.env.MOD_PORT) || DEFAULT_MOD_PORT;

const app = express();
const httpServer = createServer(app);

// WebSocket relay: browser clients connect here; server connects to Fabric mod and forwards frames
const wss = new WebSocketServer({ server: httpServer, path: "/ws" });

wss.on("connection", (clientWs, _req) => {
  const modUrl = `ws://${MOD_HOST}:${MOD_PORT}`;
  const modWs = new WebSocket(modUrl);

  modWs.on("open", () => {
    clientWs.on("message", (data: Buffer | ArrayBuffer) => {
      if (modWs.readyState === modWs.OPEN) modWs.send(data);
    });
  });

  modWs.on("message", (data: Buffer | ArrayBuffer) => {
    if (clientWs.readyState === clientWs.OPEN) clientWs.send(data);
  });

  modWs.on("close", () => {
    if (clientWs.readyState === clientWs.OPEN) clientWs.close();
  });
  modWs.on("error", () => {
    if (clientWs.readyState === clientWs.OPEN) clientWs.close(1011, "Mod connection failed");
  });

  clientWs.on("close", () => modWs.close());
  clientWs.on("error", () => modWs.close());
});

// In dev, serve client from Vite; in prod, serve built client
const isProd = process.env.NODE_ENV === "production";
const clientDist = path.join(__dirname, "..", "..", "client", "dist");

if (isProd) {
  app.use(express.static(clientDist));
  app.get("*", (_req, res) => {
    res.sendFile(path.join(clientDist, "index.html"));
  });
} else {
  app.get("/", (_req, res) => {
    res.redirect(302, "http://localhost:5173");
  });
}

httpServer.listen(HTTP_PORT, () => {
  console.log(`Web visualizer server http://localhost:${HTTP_PORT}`);
  console.log(`WebSocket relay /ws → ws://${MOD_HOST}:${MOD_PORT}`);
  if (!isProd) console.log("Dev: run 'pnpm dev:client' and open http://localhost:5173");
});

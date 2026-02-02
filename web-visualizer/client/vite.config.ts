import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: true, // listen on 0.0.0.0 so Quest can reach via http://YOUR_PC_IP:5173
    allowedHosts: [".ngrok-free.app", ".ngrok-free.dev", ".ngrok.io"], // ngrok tunnel hostnames (leading dot = any subdomain)
    proxy: {
      "/ws": {
        target: "ws://localhost:3000",
        ws: true,
      },
    },
  },
});

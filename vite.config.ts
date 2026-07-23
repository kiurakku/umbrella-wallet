import tailwindcss from "@tailwindcss/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import viteReact from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import tsconfigPaths from "vite-tsconfig-paths";

export default defineConfig({
  plugins: [
    tsconfigPaths(),
    tailwindcss(),
    tanstackStart({
      server: { entry: "server" },
    }),
    viteReact(),
  ],
  server: {
    proxy: {
      "/auth": { target: "http://localhost:3001", changeOrigin: true },
      "/users": { target: "http://localhost:3001", changeOrigin: true },
      "/wallets": { target: "http://localhost:3001", changeOrigin: true },
      "/bank-accounts": { target: "http://localhost:3001", changeOrigin: true },
      "^/p2p/": { target: "http://localhost:3001", changeOrigin: true },
      "/rates": { target: "http://localhost:3001", changeOrigin: true },
      "/kyc": { target: "http://localhost:3001", changeOrigin: true },
      "/webhooks": { target: "http://localhost:3001", changeOrigin: true },
      "/telegram": { target: "http://localhost:3001", changeOrigin: true },
      "/health": { target: "http://localhost:3001", changeOrigin: true },
    },
  },
});

import { useEffect, useState } from "react";
import * as THREE from "three";

const TILES = 32;
const TILE_PX = 16;
const SIZE = TILES * TILE_PX; // 512

/** Colors for placeholder tiles (matches blockAtlas tile order). */
const TILE_COLORS: [number, number, number][] = [
  [0.4, 0.4, 0.4], // stone
  [0.6, 0.5, 0.45],
  [0.55, 0.5, 0.48],
  [0.5, 0.5, 0.5],
  [0.6, 0.45, 0.3], // planks
  [0.2, 0.2, 0.2], // bedrock
  [0.45, 0.35, 0.25], // dirt
  [0.3, 0.5, 0.2], // grass
  [0.5, 0.48, 0.45], // gravel
  [0.35, 0.35, 0.35],
  [0.55, 0.45, 0.35],
  [0.65, 0.5, 0.35],
  [0.5, 0.45, 0.35],
  [0.45, 0.35, 0.25],
  [0.25, 0.45, 0.2],
  [0.2, 0.3, 0.6],
  [0.9, 0.4, 0.1],
  [0.76, 0.7, 0.5],
  [0.8, 0.75, 0.6],
  [0.35, 0.35, 0.38],
  [0.4, 0.38, 0.35],
  [0.35, 0.35, 0.35],
  [0.4, 0.38, 0.35],
  [0.6, 0.5, 0.35],
  [0.5, 0.35, 0.3],
  [0.4, 0.5, 0.6],
  [0.35, 0.4, 0.6],
  [0.5, 0.55, 0.45],
  [0.55, 0.52, 0.5],
  [0.5, 0.5, 0.5],
  [0.4, 0.4, 0.35],
  [0.45, 0.5, 0.4],
  [0.35, 0.45, 0.35],
  [0.3, 0.5, 0.25],
  [0.35, 0.55, 0.4],
];

/** Create a placeholder 256×256 atlas (16×16 tiles, 16px each) with colored squares. */
function createPlaceholderAtlas(): THREE.DataTexture {
  const data = new Uint8Array(SIZE * SIZE * 4);
  for (let ty = 0; ty < TILES; ty++) {
    for (let tx = 0; tx < TILES; tx++) {
      const idx = ty * TILES + tx;
      const [r, g, b] = TILE_COLORS[idx % TILE_COLORS.length] ?? [0.5, 0.5, 0.5];
      for (let py = 0; py < TILE_PX; py++) {
        for (let px = 0; px < TILE_PX; px++) {
          const i = ((ty * TILE_PX + py) * SIZE + (tx * TILE_PX + px)) * 4;
          data[i] = Math.floor(r * 255);
          data[i + 1] = Math.floor(g * 255);
          data[i + 2] = Math.floor(b * 255);
          data[i + 3] = 255;
        }
      }
    }
  }
  const tex = new THREE.DataTexture(data, SIZE, SIZE);
  tex.format = THREE.RGBAFormat;
  tex.type = THREE.UnsignedByteType;
  tex.minFilter = THREE.NearestFilter;
  tex.magFilter = THREE.NearestFilter;
  tex.wrapS = THREE.ClampToEdgeWrapping;
  tex.wrapT = THREE.ClampToEdgeWrapping;
  tex.needsUpdate = true;
  return tex;
}

/** Use atlas texture: load /atlas.png or use placeholder (colored tiles). Always returns a texture. */
export function useAtlasTexture(): THREE.Texture {
  const [tex, setTex] = useState<THREE.Texture>(() => createPlaceholderAtlas());

  useEffect(() => {
    const loader = new THREE.TextureLoader();
    // Cache-bust in dev so we pick up rebuilt atlas
    const url = "/atlas.png?t=" + (import.meta.env.DEV ? Date.now() : "0");
    loader.load(
      url,
      (loaded) => {
        loaded.minFilter = THREE.NearestFilter;
        loaded.magFilter = THREE.NearestFilter;
        loaded.wrapS = THREE.ClampToEdgeWrapping;
        loaded.wrapT = THREE.ClampToEdgeWrapping;
        if ("colorSpace" in loaded) (loaded as THREE.Texture).colorSpace = "srgb";
        setTex(loaded);
      },
      undefined,
      (err) => {
        console.warn("Block atlas /atlas.png failed to load, using placeholder. Run: pnpm run build-atlas", err);
      }
    );
  }, []);

  return tex;
}

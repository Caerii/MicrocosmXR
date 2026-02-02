#!/usr/bin/env node
/**
 * Extract block textures from Minecraft version JAR and build atlas.png + blockAtlas.generated.ts.
 * Usage: node build-atlas.mjs [.minecraft path] [version folder name]
 * Example: node build-atlas.mjs "C:\\Users\\locke\\AppData\\Roaming\\.minecraft" 1.21.11
 * Env: MINECRAFT_PATH, MC_VERSION (default 1.21.11).
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import yauzl from "yauzl";
import sharp from "sharp";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const MINECRAFT_PATH = process.env.MINECRAFT_PATH || process.argv[2] || path.join(process.env.APPDATA || "", ".minecraft");
const VERSION = process.env.MC_VERSION || process.argv[3] || "1.21.11";
const BLOCK_PREFIX = "assets/minecraft/textures/block/";
const TILE_PX = 16;
const TILES_PER_ROW = 32;
const ATLAS_SIZE = TILES_PER_ROW * TILE_PX; // 512
const MAX_TILES = TILES_PER_ROW * TILES_PER_ROW;

const jarPath = path.join(MINECRAFT_PATH, "versions", VERSION, `${VERSION}.jar`);
const outAtlas = path.join(__dirname, "..", "client", "public", "atlas.png");
const outGenerated = path.join(__dirname, "..", "client", "src", "blockAtlas.generated.ts");

if (!fs.existsSync(jarPath)) {
  console.error("JAR not found:", jarPath);
  console.error("Use: node build-atlas.mjs [.minecraft path] [version]");
  process.exit(1);
}

function pathToBlockId(entryPath) {
  const name = path.basename(entryPath, ".png");
  return "minecraft:" + name;
}

// 1) Collect block PNG paths from JAR, sort; prioritize water/lava so they're always in the atlas
const PRIORITY_NAMES = new Set(["water_still", "water_flow", "water_overlay", "lava_still", "lava_flow"]);
const allPaths = await new Promise((resolve, reject) => {
  const list = [];
  yauzl.open(jarPath, { lazyEntries: true }, (err, zipfile) => {
    if (err) return reject(err);
    zipfile.readEntry();
    zipfile.on("entry", (entry) => {
      if (entry.fileName.startsWith(BLOCK_PREFIX) && entry.fileName.endsWith(".png")) list.push(entry.fileName);
      zipfile.readEntry();
    });
    zipfile.on("end", () => resolve(list.sort()));
  });
});
const priorityPaths = allPaths.filter((p) => PRIORITY_NAMES.has(path.basename(p, ".png")));
const restPaths = allPaths.filter((p) => !priorityPaths.includes(p));
const toProcess = [...priorityPaths, ...restPaths].slice(0, MAX_TILES);
console.log("Building atlas from", toProcess.length, "block textures (JAR:", jarPath, ")");

// 2) Open JAR again: for each entry whose path is in toProcess, read buffer into Map
const fileBuffers = await new Promise((resolve, reject) => {
  const wanted = new Set(toProcess);
  const results = new Map();
  let pending = toProcess.length;

  yauzl.open(jarPath, { lazyEntries: true }, (err, zipfile) => {
    if (err) return reject(err);
    zipfile.readEntry();
    zipfile.on("entry", (entry) => {
      if (!wanted.has(entry.fileName)) {
        zipfile.readEntry();
        return;
      }
      zipfile.openReadStream(entry, (errRead, stream) => {
        if (errRead) {
          pending--;
          zipfile.readEntry();
          if (pending === 0) resolve(results);
          return;
        }
        const chunks = [];
        stream.on("data", (c) => chunks.push(c));
        stream.on("end", () => {
          results.set(entry.fileName, Buffer.concat(chunks));
          pending--;
          zipfile.readEntry();
          if (pending === 0) resolve(results);
        });
        stream.on("error", () => {
          pending--;
          zipfile.readEntry();
          if (pending === 0) resolve(results);
        });
      });
    });
    zipfile.on("end", () => {
      if (pending !== 0) resolve(results);
    });
  });
});

// Official Minecraft Java colors (multiply tint). Refs: minecraft.wiki Block_colors, Color.
// Water: predefined per biome; default blue from wiki #3F76E4 (e.g. Plains/Ocean).
// Lava: standard lava orange.
// Foliage: leaf textures are grayscale; game multiplies by colormap or constant. We bake one default.
function hexToTint(hex) {
  const r = parseInt(hex.slice(1, 3), 16) / 255;
  const g = parseInt(hex.slice(3, 5), 16) / 255;
  const b = parseInt(hex.slice(5, 7), 16) / 255;
  return [r, g, b];
}
const WATER_TINT = hexToTint("#3F76E4");   // Java default water (wiki Block_colors)
const LAVA_TINT = hexToTint("#FF6B00");    // Lava orange
const FOLIAGE_DEFAULT = hexToTint("#59AE30"); // Forest oak/jungle/acacia/dark_oak/mangrove/vines (wiki)
const FOLIAGE_BIRCH = hexToTint("#80A755");   // Birch leaves constant (wiki)
const FOLIAGE_SPRUCE = hexToTint("#619961");  // Spruce leaves constant (wiki)
const GRASS_DEFAULT = hexToTint("#79C05A");   // Forest grass (wiki Block_colors) — grass block top/side, tall grass, fern

const waterNames = new Set(["water_still", "water_flow", "water_overlay"]);
const lavaNames = new Set(["lava_still", "lava_flow"]);
const grassNames = new Set([
  "grass_block_top", "grass_block_side", "grass", "tall_grass", "fern", "large_fern", "sugar_cane",
  "grass_block_carried", "potted_fern",
]);
const foliageColormapNames = new Set([
  "oak_leaves", "jungle_leaves", "acacia_leaves", "dark_oak_leaves", "mangrove_leaves", "vines", "azalea_leaves",
]);
const foliageBirchNames = new Set(["birch_leaves"]);
const foliageSpruceNames = new Set(["spruce_leaves"]);

function tintPixels(data, channels, tint) {
  const len = data.length;
  for (let i = 0; i < len; i += channels) {
    data[i] = Math.min(255, Math.round(data[i] * tint[0]));
    data[i + 1] = Math.min(255, Math.round(data[i + 1] * tint[1]));
    data[i + 2] = Math.min(255, Math.round(data[i + 2] * tint[2]));
  }
}

// 3) Resize each to 16x16 and build composite inputs + blockTile map
const compositeInputs = [];
const blockTile = {};
for (let i = 0; i < toProcess.length; i++) {
  const fileName = toProcess[i];
  const buf = fileBuffers.get(fileName);
  if (!buf) continue;
  const tx = i % TILES_PER_ROW;
  const ty = Math.floor(i / TILES_PER_ROW);
  const baseName = path.basename(fileName, ".png");
  blockTile[pathToBlockId(fileName)] = [tx, ty];
  const resized = await sharp(buf)
    .resize(TILE_PX, TILE_PX)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const channels = resized.info.channels;
  const pixelData = resized.data;
  if (waterNames.has(baseName)) tintPixels(pixelData, channels, WATER_TINT);
  else if (lavaNames.has(baseName)) tintPixels(pixelData, channels, LAVA_TINT);
  else if (grassNames.has(baseName)) tintPixels(pixelData, channels, GRASS_DEFAULT);
  else if (foliageBirchNames.has(baseName)) tintPixels(pixelData, channels, FOLIAGE_BIRCH);
  else if (foliageSpruceNames.has(baseName)) tintPixels(pixelData, channels, FOLIAGE_SPRUCE);
  else if (foliageColormapNames.has(baseName)) tintPixels(pixelData, channels, FOLIAGE_DEFAULT);
  compositeInputs.push({
    input: pixelData,
    raw: { width: TILE_PX, height: TILE_PX, channels },
    left: tx * TILE_PX,
    top: ty * TILE_PX,
  });
}

// 4) Composite onto 512x512 and write PNG
const atlasBuffer = await sharp({
  create: {
    width: ATLAS_SIZE,
    height: ATLAS_SIZE,
    channels: 4,
    background: { r: 0, g: 0, b: 0, alpha: 0 },
  },
})
  .composite(compositeInputs)
  .png()
  .toBuffer();

fs.mkdirSync(path.dirname(outAtlas), { recursive: true });
fs.writeFileSync(outAtlas, atlasBuffer);
console.log("Wrote", outAtlas);

// 5) Write blockAtlas.generated.ts
const tsContent = `/** Auto-generated by scripts/build-atlas.mjs from Minecraft ${VERSION} JAR. Do not edit by hand. */
export const BLOCK_TILE_GENERATED: Record<string, [number, number]> = ${JSON.stringify(blockTile)};
export const ATLAS_TILES_PER_ROW = ${TILES_PER_ROW};
`;
fs.writeFileSync(outGenerated, tsContent);
console.log("Wrote", outGenerated);

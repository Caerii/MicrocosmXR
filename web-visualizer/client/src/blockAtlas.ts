/**
 * Block state ID → atlas tile. Atlas is 512×512 with 32×32 tiles (16px each).
 * Uses shared normalizeBlockStateToBaseId so any server format becomes "minecraft:block_id".
 * Run scripts/build-atlas.mjs to extract Minecraft textures and generate blockAtlas.generated.ts.
 */
import { normalizeBlockStateToBaseId } from "@web-visualizer/shared";
import { BLOCK_TILE_GENERATED, ATLAS_TILES_PER_ROW as GEN_TILES } from "./blockAtlas.generated";

const TILES_PER_ROW = GEN_TILES;
const TILE_SIZE = 1 / TILES_PER_ROW;

/** Fallback tiles when generated atlas is empty. Merged with BLOCK_TILE_GENERATED. */
const BLOCK_TILE_FALLBACK: Record<string, [number, number]> = {
  "minecraft:stone": [0, 0],
  "minecraft:dirt": [1, 0],
  "minecraft:grass_block": [2, 0],
  "minecraft:bedrock": [3, 0],
  "minecraft:oak_planks": [4, 0],
  "minecraft:cobblestone": [0, 1],
  "minecraft:gravel": [1, 1],
  "minecraft:sand": [2, 1],
  "minecraft:water": [3, 1],
  "minecraft:lava": [4, 1],
  "minecraft:deepslate": [0, 2],
  "minecraft:tuff": [1, 2],
};

/** Base block id → tile (tx, ty). Generated from JAR takes precedence. */
const BLOCK_TILE: Record<string, [number, number]> = { ...BLOCK_TILE_FALLBACK, ...BLOCK_TILE_GENERATED };

/** Normalize any server string to "minecraft:block_id" for atlas lookup (single source of truth in shared). */
export function blockStateToBaseId(blockStateId: string): string {
  return normalizeBlockStateToBaseId(blockStateId);
}

/** Face index: +X=0, -X=1, +Y=2, -Y=3, +Z=4, -Z=5 (matches mesher order). */
export type BlockFace = 0 | 1 | 2 | 3 | 4 | 5;

/** Get atlas tile (tx, ty) for base block id. Tries exact id, then id_top (e.g. grass_block → grass_block_top). */
export function getBlockTile(baseId: string): [number, number] {
  return getBlockTileForFace(baseId, 0);
}

/** Block IDs that use _still / _flow texture names in the atlas (water, lava). */
const FLUID_ALIASES: Record<string, string> = {
  "minecraft:water": "minecraft:water_still",
  "minecraft:lava": "minecraft:lava_still",
};

/** Get atlas tile for a specific face so grass_block gets top/side/bottom textures. */
export function getBlockTileForFace(baseId: string, face: BlockFace): [number, number] {
  const lower = baseId.toLowerCase();
  const lookupId = FLUID_ALIASES[lower] ?? lower;
  const exact = BLOCK_TILE[lookupId] ?? BLOCK_TILE[lower];
  if (face === 2) {
    const top = BLOCK_TILE[lookupId + "_top"] ?? BLOCK_TILE[lower + "_top"];
    if (top) return top;
  }
  if (face === 3) {
    const bottom = BLOCK_TILE[lookupId + "_bottom"] ?? BLOCK_TILE[lower + "_bottom"];
    if (bottom) return bottom;
  }
  if (face >= 0 && face <= 5 && face !== 2 && face !== 3) {
    const side = BLOCK_TILE[lookupId + "_side"] ?? BLOCK_TILE[lower + "_side"];
    if (side) return side;
  }
  if (exact) return exact;
  const withTop = BLOCK_TILE[lookupId + "_top"] ?? BLOCK_TILE[lower + "_top"];
  if (withTop) return withTop;
  const withSide = BLOCK_TILE[lookupId + "_side"] ?? BLOCK_TILE[lower + "_side"];
  if (withSide) return withSide;
  return BLOCK_TILE["minecraft:stone"] ?? [0, 0];
}

/** UV rect (u0, v0, u1, v1) for a tile. Three.js: (0,0) bottom-left; we use V flipped for typical atlas. */
export function tileToUV(tx: number, ty: number): [number, number, number, number] {
  const u0 = tx * TILE_SIZE;
  const u1 = u0 + TILE_SIZE;
  const v1 = 1 - ty * TILE_SIZE;
  const v0 = v1 - TILE_SIZE;
  return [u0, v0, u1, v1];
}

export const TILES_PER_ROW_ATLAS = TILES_PER_ROW;

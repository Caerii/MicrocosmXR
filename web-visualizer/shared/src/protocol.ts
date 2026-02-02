/**
 * Protocol constants matching the Fabric mod (StreamerWebSocketHandler).
 * Text: HELLO <version>, SET_ORIGIN <x0> <y0> <z0> <scale>.
 * Binary: byte type, then type-specific payload (big-endian).
 */
export const MSG_CHUNK_SECTION_SNAPSHOT = 2;
export const MSG_BLOCK_DELTA = 3;
export const MSG_BLOCK_ENTITY = 4;
export const MSG_ENTITY_SPAWN = 5;

export const SECTION_BLOCK_COUNT = 16 * 16 * 16; // 4096
export const SECTION_BIOME_COUNT = 4 * 4 * 4; // 64
export const DEFAULT_MOD_PORT = 25566;

export interface ChunkSectionCoord {
  cx: number;
  cz: number;
  sy: number;
}

export function chunkSectionKey(coord: ChunkSectionCoord): string {
  return `${coord.cx},${coord.cz},${coord.sy}`;
}

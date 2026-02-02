import type { ChunkSectionCoord } from "./protocol.js";

export interface StreamCallbacks {
  onHello(version: string): void;
  onSetOrigin(x0: number, y0: number, z0: number, scale: number): void;
  onChunkSectionSnapshot(
    cx: number,
    cz: number,
    sy: number,
    palette: string[],
    indices: Uint16Array,
    blockLight: Uint8Array,
    skyLight: Uint8Array,
    biomePalette: string[],
    biomeIndices: Uint16Array
  ): void;
  onBlockDelta(x: number, y: number, z: number, blockStateId: string): void;
  onBlockEntity(x: number, y: number, z: number, typeId: string, nbt: Uint8Array | null): void;
  onEntitySpawn(entityId: number, typeId: string, x: number, y: number, z: number, yaw: number, pitch: number): void;
}

export interface SectionData {
  palette: string[];
  indices: Uint16Array;
  blockLight: Uint8Array;
  skyLight: Uint8Array;
  biomePalette: string[];
  biomeIndices: Uint16Array;
}

export interface WorldState {
  protocolVersion: string | null;
  originX: number;
  originY: number;
  originZ: number;
  scale: number;
  sections: Map<string, SectionData>;
  dirtySectionKeys: Set<string>;
  blockEntities: Array<{ x: number; y: number; z: number; typeId: string; nbt: Uint8Array | null }>;
  entities: Array<{
    entityId: number;
    typeId: string;
    x: number;
    y: number;
    z: number;
    yaw: number;
    pitch: number;
  }>;
}

import { normalizeBlockStateToBaseId } from "./block-id.js";
import { chunkSectionKey } from "./protocol.js";
import type { StreamCallbacks } from "./types.js";
import type { SectionData, WorldState } from "./types.js";

export function createWorldState(): WorldState {
  return {
    protocolVersion: null,
    originX: 0,
    originY: 0,
    originZ: 0,
    scale: 0.2,
    sections: new Map(),
    dirtySectionKeys: new Set(),
    blockEntities: [],
    entities: [],
  };
}

export function createStreamCallbacks(state: WorldState): StreamCallbacks {
  return {
    onHello(version) {
      state.protocolVersion = version;
    },
    onSetOrigin(x0, y0, z0, scale) {
      state.originX = x0;
      state.originY = y0;
      state.originZ = z0;
      state.scale = scale;
    },
    onChunkSectionSnapshot(cx, cz, sy, palette, indices, blockLight, skyLight, biomePalette, biomeIndices) {
      const key = chunkSectionKey({ cx, cz, sy });
      const data: SectionData = {
        palette: palette.map((id) => normalizeBlockStateToBaseId(id)),
        indices: new Uint16Array(indices),
        blockLight: new Uint8Array(blockLight),
        skyLight: new Uint8Array(skyLight),
        biomePalette: [...biomePalette],
        biomeIndices: new Uint16Array(biomeIndices),
      };
      state.sections.set(key, data);
      state.dirtySectionKeys.add(key);
    },
    onBlockDelta(x, y, z, blockStateId) {
      const cx = x >> 4;
      const cz = z >> 4;
      const sy = y >> 4;
      const key = chunkSectionKey({ cx, cz, sy });
      const section = state.sections.get(key);
      if (!section) return;
      const lx = x & 15;
      const ly = y & 15;
      const lz = z & 15;
      const normalizedId = normalizeBlockStateToBaseId(blockStateId);
      let idx = section.palette.indexOf(normalizedId);
      if (idx < 0) {
        idx = section.palette.length;
        section.palette.push(normalizedId);
      }
      const i = lx + lz * 16 + ly * 256;
      section.indices[i] = idx;
      state.dirtySectionKeys.add(key);
    },
    onBlockEntity(x, y, z, typeId, nbt) {
      state.blockEntities.push({ x, y, z, typeId, nbt });
    },
    onEntitySpawn(entityId, typeId, x, y, z, yaw, pitch) {
      state.entities.push({ entityId, typeId, x, y, z, yaw, pitch });
    },
  };
}

export function getBlockStateId(section: SectionData, localX: number, localY: number, localZ: number): string | null {
  const i = localX + localZ * 16 + localY * 256;
  const idx = section.indices[i];
  if (idx >= 0 && idx < section.palette.length) return section.palette[idx] ?? null;
  return null;
}

export function clearDirty(state: WorldState, key: string): void {
  state.dirtySectionKeys.delete(key);
}

export function clearAllDirty(state: WorldState): void {
  state.dirtySectionKeys.clear();
}

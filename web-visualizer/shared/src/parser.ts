import {
  MSG_CHUNK_SECTION_SNAPSHOT,
  MSG_BLOCK_DELTA,
  MSG_BLOCK_ENTITY,
  MSG_ENTITY_SPAWN,
  SECTION_BLOCK_COUNT,
  SECTION_BIOME_COUNT,
} from "./protocol.js";
import type { StreamCallbacks } from "./types.js";

function readInt32BE(buf: Uint8Array, offset: number): number {
  return (buf[offset]! << 24) | (buf[offset + 1]! << 16) | (buf[offset + 2]! << 8) | buf[offset + 3]!;
}
function readUInt16BE(buf: Uint8Array, offset: number): number {
  return (buf[offset]! << 8) | buf[offset + 1]!;
}
function readInt64BE(buf: Uint8Array, offset: number): bigint {
  const h = (buf[offset]! << 24) | (buf[offset + 1]! << 16) | (buf[offset + 2]! << 8) | buf[offset + 3]!;
  const l = (buf[offset + 4]! << 24) | (buf[offset + 5]! << 16) | (buf[offset + 6]! << 8) | buf[offset + 7]!;
  return (BigInt(h >>> 0) << 32n) | BigInt(l >>> 0);
}
function readFloatBE(buf: Uint8Array, offset: number): number {
  const view = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  return view.getFloat32(offset, false);
}
function readDoubleBE(buf: Uint8Array, offset: number): number {
  const view = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  return view.getFloat64(offset, false);
}

export function parseText(text: string, callbacks: StreamCallbacks): void {
  const trimmed = text.trim();
  if (!trimmed) return;
  const parts = trimmed.split(/\s+/);
  if (parts.length === 0) return;

  if (parts[0] === "HELLO" && parts.length >= 2) {
    callbacks.onHello(parts[1]!);
    return;
  }
  if (parts[0] === "SET_ORIGIN" && parts.length >= 5) {
    const x0 = parseInt(parts[1]!, 10);
    const y0 = parseInt(parts[2]!, 10);
    const z0 = parseInt(parts[3]!, 10);
    const scale = parseFloat(parts[4]!);
    if (!Number.isNaN(x0) && !Number.isNaN(y0) && !Number.isNaN(z0) && !Number.isNaN(scale)) {
      callbacks.onSetOrigin(x0, y0, z0, scale);
    }
  }
}

export function parseBinary(buf: Uint8Array, callbacks: StreamCallbacks): boolean {
  if (buf.length < 1) return false;
  const type = buf[0]!;

  switch (type) {
    case MSG_CHUNK_SECTION_SNAPSHOT:
      return parseChunkSectionSnapshot(buf, callbacks);
    case MSG_BLOCK_DELTA:
      return parseBlockDelta(buf, callbacks);
    case MSG_BLOCK_ENTITY:
      return parseBlockEntity(buf, callbacks);
    case MSG_ENTITY_SPAWN:
      return parseEntitySpawn(buf, callbacks);
    default:
      return false;
  }
}

function parseChunkSectionSnapshot(buf: Uint8Array, callbacks: StreamCallbacks): boolean {
  if (buf.length < 1 + 4 + 4 + 4 + 4) return false;
  let offset = 1;
  const cx = readInt32BE(buf, offset);
  offset += 4;
  const cz = readInt32BE(buf, offset);
  offset += 4;
  const sy = readInt32BE(buf, offset);
  offset += 4;
  const paletteLen = readInt32BE(buf, offset);
  offset += 4;

  const palette: string[] = [];
  for (let i = 0; i < paletteLen; i++) {
    if (offset + 2 > buf.length) return false;
    const strLen = readUInt16BE(buf, offset);
    offset += 2;
    if (offset + strLen > buf.length) return false;
    palette.push(new TextDecoder().decode(buf.subarray(offset, offset + strLen)));
    offset += strLen;
  }

  const indices = new Uint16Array(SECTION_BLOCK_COUNT);
  for (let i = 0; i < SECTION_BLOCK_COUNT; i++) {
    if (offset + 2 > buf.length) return false;
    indices[i] = readUInt16BE(buf, offset);
    offset += 2;
  }

  const blockLight = new Uint8Array(SECTION_BLOCK_COUNT);
  const skyLight = new Uint8Array(SECTION_BLOCK_COUNT);
  let biomePalette: string[] = ["minecraft:plains"];
  const biomeIndices = new Uint16Array(SECTION_BIOME_COUNT);

  if (offset + 4096 + 4096 <= buf.length) {
    blockLight.set(buf.subarray(offset, offset + 4096));
    offset += 4096;
    skyLight.set(buf.subarray(offset, offset + 4096));
    offset += 4096;
    if (offset + 4 <= buf.length) {
      const biomePaletteLen = readInt32BE(buf, offset);
      offset += 4;
      biomePalette = [];
      for (let i = 0; i < biomePaletteLen; i++) {
        if (offset + 2 > buf.length) break;
        const strLen = readUInt16BE(buf, offset);
        offset += 2;
        if (offset + strLen > buf.length) break;
        biomePalette.push(new TextDecoder().decode(buf.subarray(offset, offset + strLen)));
        offset += strLen;
      }
      for (let i = 0; i < SECTION_BIOME_COUNT && offset + 2 <= buf.length; i++) {
        biomeIndices[i] = readUInt16BE(buf, offset);
        offset += 2;
      }
    }
  }

  callbacks.onChunkSectionSnapshot(cx, cz, sy, palette, indices, blockLight, skyLight, biomePalette, biomeIndices);
  return true;
}

function parseBlockDelta(buf: Uint8Array, callbacks: StreamCallbacks): boolean {
  if (buf.length < 1 + 4 + 4 + 4 + 2) return false;
  const x = readInt32BE(buf, 1);
  const y = readInt32BE(buf, 5);
  const z = readInt32BE(buf, 9);
  const strLen = readUInt16BE(buf, 13);
  if (15 + strLen > buf.length) return false;
  const blockStateId = new TextDecoder().decode(buf.subarray(15, 15 + strLen));
  callbacks.onBlockDelta(x, y, z, blockStateId);
  return true;
}

function parseBlockEntity(buf: Uint8Array, callbacks: StreamCallbacks): boolean {
  if (buf.length < 1 + 4 + 4 + 4 + 2 + 4) return false;
  let offset = 1;
  const x = readInt32BE(buf, offset);
  offset += 4;
  const y = readInt32BE(buf, offset);
  offset += 4;
  const z = readInt32BE(buf, offset);
  offset += 4;
  const typeLen = readUInt16BE(buf, offset);
  offset += 2;
  if (offset + typeLen + 4 > buf.length) return false;
  const typeId = new TextDecoder().decode(buf.subarray(offset, offset + typeLen));
  offset += typeLen;
  const nbtLen = readInt32BE(buf, offset);
  offset += 4;
  let nbt: Uint8Array | null = null;
  if (nbtLen > 0 && offset + nbtLen <= buf.length) {
    nbt = buf.subarray(offset, offset + nbtLen);
  }
  callbacks.onBlockEntity(x, y, z, typeId, nbt);
  return true;
}

function parseEntitySpawn(buf: Uint8Array, callbacks: StreamCallbacks): boolean {
  if (buf.length < 1 + 4 + 2 + 8 + 8 + 8 + 4 + 4) return false;
  let offset = 1;
  const entityId = readInt32BE(buf, offset);
  offset += 4;
  const typeLen = readUInt16BE(buf, offset);
  offset += 2;
  if (offset + typeLen + 8 + 8 + 8 + 4 + 4 > buf.length) return false;
  const typeId = new TextDecoder().decode(buf.subarray(offset, offset + typeLen));
  offset += typeLen;
  const x = readDoubleBE(buf, offset);
  offset += 8;
  const y = readDoubleBE(buf, offset);
  offset += 8;
  const z = readDoubleBE(buf, offset);
  offset += 8;
  const yaw = readFloatBE(buf, offset);
  offset += 4;
  const pitch = readFloatBE(buf, offset);
  callbacks.onEntitySpawn(entityId, typeId, x, y, z, yaw, pitch);
  return true;
}

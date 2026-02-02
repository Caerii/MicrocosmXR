import { blockStateToBaseId, getBlockTileForFace, tileToUV, type BlockFace } from "./blockAtlas";

const BLOCKS_PER_CHUNK = 16;
const DISPLAY_SCALE = 1;

const AIR_LIKE = new Set(["minecraft:air", "minecraft:cave_air", "minecraft:void_air"]);

/** Face order: +X, -X, +Y, -Y, +Z, -Z. */
const FACE_NORMALS: [number, number, number][] = [
  [1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1],
];

function isSolid(palette: string[], indices: Uint16Array, lx: number, ly: number, lz: number): boolean {
  if (lx < 0 || lx >= BLOCKS_PER_CHUNK || ly < 0 || ly >= BLOCKS_PER_CHUNK || lz < 0 || lz >= BLOCKS_PER_CHUNK) return false;
  const i = lx + lz * BLOCKS_PER_CHUNK + ly * 256;
  const idx = indices[i];
  const id = idx < palette.length ? palette[idx] : "minecraft:air";
  return !AIR_LIKE.has(id);
}

function getStateId(palette: string[], indices: Uint16Array, lx: number, ly: number, lz: number): string {
  const i = lx + lz * BLOCKS_PER_CHUNK + ly * 256;
  const idx = indices[i];
  return idx < palette.length ? palette[idx] ?? "minecraft:air" : "minecraft:air";
}

/** Emit 6 vertices (two triangles) for a quad. */
function pushQuad(
  positions: number[],
  normals: number[],
  uvs: number[],
  p0: [number, number, number],
  p1: [number, number, number],
  p2: [number, number, number],
  p3: [number, number, number],
  nx: number,
  ny: number,
  nz: number,
  u0: number,
  v0: number,
  u1: number,
  v1: number
) {
  positions.push(p0[0], p0[1], p0[2], p1[0], p1[1], p1[2], p2[0], p2[1], p2[2], p0[0], p0[1], p0[2], p2[0], p2[1], p2[2], p3[0], p3[1], p3[2]);
  for (let i = 0; i < 6; i++) normals.push(nx, ny, nz);
  uvs.push(u1, v0, u1, v1, u0, v1, u1, v0, u0, v1, u0, v0);
}

/** Build positions, normals, uvs for visible faces only (6 vertices per face = two triangles). */
export function buildSectionMesh(
  palette: string[],
  indices: Uint16Array,
  cx: number,
  cz: number,
  sy: number,
  originX: number,
  originY: number,
  originZ: number
): { position: Float32Array; normal: Float32Array; uv: Float32Array } {
  const positions: number[] = [];
  const normals: number[] = [];
  const uvs: number[] = [];

  for (let ly = 0; ly < BLOCKS_PER_CHUNK; ly++) {
    for (let lz = 0; lz < BLOCKS_PER_CHUNK; lz++) {
      for (let lx = 0; lx < BLOCKS_PER_CHUNK; lx++) {
        const stateId = getStateId(palette, indices, lx, ly, lz);
        if (AIR_LIKE.has(stateId)) continue;

        const baseId = blockStateToBaseId(stateId);
        const wx = (cx * BLOCKS_PER_CHUNK + lx - originX) * DISPLAY_SCALE;
        const wy = (sy * BLOCKS_PER_CHUNK + ly - originY) * DISPLAY_SCALE;
        const wz = (cz * BLOCKS_PER_CHUNK + lz - originZ) * DISPLAY_SCALE;

        const x0 = wx,
          x1 = wx + DISPLAY_SCALE;
        const y0 = wy,
          y1 = wy + DISPLAY_SCALE;
        const z0 = wz,
          z1 = wz + DISPLAY_SCALE;

        const pushFace = (face: BlockFace) => {
          const [tx, ty] = getBlockTileForFace(baseId, face);
          const [u0, v0, u1, v1] = tileToUV(tx, ty);
          const [nx, ny, nz] = FACE_NORMALS[face];
          if (face === 0) pushQuad(positions, normals, uvs, [x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1], nx, ny, nz, u0, v0, u1, v1);
          else if (face === 1) pushQuad(positions, normals, uvs, [x0, y0, z1], [x0, y1, z1], [x0, y1, z0], [x0, y0, z0], nx, ny, nz, u0, v0, u1, v1);
          else if (face === 2) pushQuad(positions, normals, uvs, [x0, y1, z1], [x1, y1, z1], [x1, y1, z0], [x0, y1, z0], nx, ny, nz, u0, v0, u1, v1);
          else if (face === 3) pushQuad(positions, normals, uvs, [x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1], nx, ny, nz, u0, v0, u1, v1);
          else if (face === 4) pushQuad(positions, normals, uvs, [x1, y0, z1], [x1, y1, z1], [x0, y1, z1], [x0, y0, z1], nx, ny, nz, u0, v0, u1, v1);
          else pushQuad(positions, normals, uvs, [x0, y0, z0], [x0, y1, z0], [x1, y1, z0], [x1, y0, z0], nx, ny, nz, u0, v0, u1, v1);
        };
        if (!isSolid(palette, indices, lx + 1, ly, lz)) pushFace(0);
        if (!isSolid(palette, indices, lx - 1, ly, lz)) pushFace(1);
        if (!isSolid(palette, indices, lx, ly + 1, lz)) pushFace(2);
        if (!isSolid(palette, indices, lx, ly - 1, lz)) pushFace(3);
        if (!isSolid(palette, indices, lx, ly, lz + 1)) pushFace(4);
        if (!isSolid(palette, indices, lx, ly, lz - 1)) pushFace(5);
      }
    }
  }

  return {
    position: new Float32Array(positions),
    normal: new Float32Array(normals),
    uv: new Float32Array(uvs),
  };
}

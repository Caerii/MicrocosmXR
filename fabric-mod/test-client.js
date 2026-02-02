/**
 * Simple Node WebSocket test client to verify chunk/block streaming.
 * Run: node test-client.js [host] [port]
 * Example: node test-client.js localhost 25566
 *
 * Requires: pnpm install (or npm install ws)
 */
const WebSocket = require('ws');

const host = process.argv[2] || 'localhost';
const port = process.argv[3] || '25566';
const url = `ws://${host}:${port}`;

console.log('Connecting to', url);
const ws = new WebSocket(url);

ws.on('open', () => {
  console.log('Connected.');
});

ws.on('message', (data) => {
  if (Buffer.isBuffer(data) || data instanceof ArrayBuffer) {
    const buf = Buffer.from(data);
    const type = buf[0];
    if (type === 2) {
      // CHUNK_SECTION_SNAPSHOT: blocks; then optionally 4096 blockLight + 4096 skyLight + biome palette + 64 indices
      const cx = buf.readInt32BE(1);
      const cz = buf.readInt32BE(5);
      const sy = buf.readInt32BE(9);
      let offset = 13;
      const paletteLen = buf.readInt32BE(offset);
      offset += 4;
      const palette = [];
      for (let i = 0; i < paletteLen; i++) {
        if (offset + 2 > buf.length) break;
        const len = buf.readUInt16BE(offset);
        offset += 2;
        if (offset + len > buf.length) break;
        palette.push(buf.toString('utf8', offset, offset + len));
        offset += len;
      }
      for (let i = 0; i < 4096; i++) {
        if (offset + 2 > buf.length) break;
        offset += 2;
      }
      let hasLight = offset + 4096 + 4096 <= buf.length;
      if (hasLight) {
        offset += 4096; // blockLight
        offset += 4096; // skyLight
      }
      let hasBiomes = offset + 4 <= buf.length;
      if (hasBiomes) {
        const biomePaletteLen = buf.readInt32BE(offset);
        offset += 4;
        for (let i = 0; i < biomePaletteLen && offset + 2 <= buf.length; i++) {
          const len = buf.readUInt16BE(offset);
          offset += 2 + len;
        }
        if (offset + 64 * 2 <= buf.length) offset += 64 * 2;
      }
      console.log('CHUNK_SECTION_SNAPSHOT cx=%d cz=%d sy=%d palette=%d light=%s biomes=%s sample=%s',
        cx, cz, sy, palette.length, hasLight ? 'yes' : 'no', hasBiomes ? 'yes' : 'no', palette.slice(0, 5).join(', '));
    } else if (type === 3) {
      // BLOCK_DELTA
      const x = buf.readInt32BE(1);
      const y = buf.readInt32BE(5);
      const z = buf.readInt32BE(9);
      const len = buf.readUInt16BE(13);
      const blockStateId = buf.toString('utf8', 15, 15 + len);
      console.log('BLOCK_DELTA %d %d %d -> %s', x, y, z, blockStateId);
    } else if (type === 4) {
      // BLOCK_ENTITY
      const x = buf.readInt32BE(1);
      const y = buf.readInt32BE(5);
      const z = buf.readInt32BE(9);
      const len = buf.readUInt16BE(13);
      const typeId = buf.toString('utf8', 15, 15 + len);
      const nbtLen = buf.readInt32BE(15 + len);
      console.log('BLOCK_ENTITY %d %d %d type=%s nbtLen=%d', x, y, z, typeId, nbtLen);
    } else if (type === 5) {
      // ENTITY_SPAWN: int id, short typeLen, utf8 typeId, double x,y,z, float yaw, pitch
      const eid = buf.readInt32BE(1);
      const len = buf.readUInt16BE(5);
      const typeId = buf.toString('utf8', 7, 7 + len);
      const base = 7 + len;
      const x = buf.readDoubleBE(base);
      const y = buf.readDoubleBE(base + 8);
      const z = buf.readDoubleBE(base + 16);
      const yaw = buf.readFloatBE(base + 24);
      const pitch = buf.readFloatBE(base + 28);
      console.log('ENTITY_SPAWN id=%d type=%s pos=(%.2f,%.2f,%.2f) yaw=%.2f pitch=%.2f', eid, typeId, x, y, z, yaw, pitch);
    } else {
      console.log('Binary message type', type, 'length', buf.length);
    }
  } else {
    console.log('Text:', data.toString());
  }
});

ws.on('error', (err) => {
  console.error('Error:', err.message);
});

ws.on('close', () => {
  console.log('Disconnected.');
  process.exit(0);
});

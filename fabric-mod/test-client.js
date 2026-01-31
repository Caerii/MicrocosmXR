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
      // CHUNK_SECTION_SNAPSHOT
      const cx = buf.readInt32BE(1);
      const cz = buf.readInt32BE(5);
      const sy = buf.readInt32BE(9);
      let offset = 13;
      const paletteLen = buf.readInt32BE(offset);
      offset += 4;
      const palette = [];
      for (let i = 0; i < paletteLen; i++) {
        const len = buf.readUInt16BE(offset);
        offset += 2;
        palette.push(buf.toString('utf8', offset, offset + len));
        offset += len;
      }
      const indices = [];
      for (let i = 0; i < 4096; i++) {
        indices.push(buf.readUInt16BE(offset));
        offset += 2;
      }
      console.log('CHUNK_SECTION_SNAPSHOT cx=%d cz=%d sy=%d palette=%d blocks sample=%s',
        cx, cz, sy, palette.length, palette.slice(0, 5).join(', '));
    } else if (type === 3) {
      // BLOCK_DELTA
      const x = buf.readInt32BE(1);
      const y = buf.readInt32BE(5);
      const z = buf.readInt32BE(9);
      const len = buf.readUInt16BE(13);
      const blockStateId = buf.toString('utf8', 15, 15 + len);
      console.log('BLOCK_DELTA %d %d %d -> %s', x, y, z, blockStateId);
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

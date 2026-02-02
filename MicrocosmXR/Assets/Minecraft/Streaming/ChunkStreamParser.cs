using System;
using System.Collections.Generic;
using System.Text;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// Parses protocol messages from the Fabric mod. Binary payloads are big-endian.
    /// </summary>
    public static class ChunkStreamParser
    {
        public static void ParseText(string text, ChunkStreamClient client)
        {
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            if (parts[0] == "HELLO" && parts.Length >= 2)
            {
                client.OnHello(parts[1]);
                return;
            }

            if (parts[0] == "SET_ORIGIN" && parts.Length >= 5)
            {
                if (int.TryParse(parts[1], out int x0) &&
                    int.TryParse(parts[2], out int y0) &&
                    int.TryParse(parts[3], out int z0) &&
                    double.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double scale))
                {
                    client.OnSetOrigin(x0, y0, z0, scale);
                }
            }
        }

        /// <summary>
        /// Parse binary message. Buffer is big-endian. Returns true if parsed successfully.
        /// </summary>
        public static bool ParseBinary(byte[] buffer, ChunkStreamClient client)
        {
            if (buffer == null || buffer.Length < 1) return false;
            byte type = buffer[0];

            if (type == Protocol.MsgChunkSectionSnapshot)
                return ParseChunkSectionSnapshot(buffer, client);
            if (type == Protocol.MsgBlockDelta)
                return ParseBlockDelta(buffer, client);
            if (type == Protocol.MsgBlockEntity)
                return ParseBlockEntity(buffer, client);
            if (type == Protocol.MsgEntitySpawn)
                return ParseEntitySpawn(buffer, client);

            return false;
        }

        static bool ParseChunkSectionSnapshot(byte[] buf, ChunkStreamClient client)
        {
            // type(1) + cx(4) + cz(4) + sy(4) + paletteLen(4) + ...
            if (buf.Length < 1 + 4 + 4 + 4 + 4) return false;
            int offset = 1;
            int cx = ReadInt32BE(buf, offset); offset += 4;
            int cz = ReadInt32BE(buf, offset); offset += 4;
            int sy = ReadInt32BE(buf, offset); offset += 4;
            int paletteLen = ReadInt32BE(buf, offset); offset += 4;

            var palette = new List<string>();
            for (int i = 0; i < paletteLen; i++)
            {
                if (offset + 2 > buf.Length) return false;
                int strLen = ReadUInt16BE(buf, offset); offset += 2;
                if (offset + strLen > buf.Length) return false;
                string s = Encoding.UTF8.GetString(buf, offset, strLen);
                offset += strLen;
                palette.Add(s);
            }

            var indices = new ushort[Protocol.SectionBlockCount];
            for (int i = 0; i < indices.Length; i++)
            {
                if (offset + 2 > buf.Length) return false;
                indices[i] = (ushort)ReadUInt16BE(buf, offset);
                offset += 2;
            }

            // Optional: block light (4096) + sky light (4096) + biome palette + 64 indices (legacy format omits these)
            var blockLight = new byte[4096];
            var skyLight = new byte[4096];
            var biomePalette = new List<string> { "minecraft:plains" };
            var biomeIndices = new ushort[Protocol.SectionBiomeCount];
            if (offset + 4096 + 4096 <= buf.Length)
            {
                Buffer.BlockCopy(buf, offset, blockLight, 0, 4096); offset += 4096;
                Buffer.BlockCopy(buf, offset, skyLight, 0, 4096); offset += 4096;
                if (offset + 4 <= buf.Length)
                {
                    int biomePaletteLen = ReadInt32BE(buf, offset); offset += 4;
                    biomePalette.Clear();
                    for (int i = 0; i < biomePaletteLen; i++)
                    {
                        if (offset + 2 > buf.Length) break;
                        int strLen = ReadUInt16BE(buf, offset); offset += 2;
                        if (offset + strLen > buf.Length) break;
                        biomePalette.Add(Encoding.UTF8.GetString(buf, offset, strLen));
                        offset += strLen;
                    }
                    for (int i = 0; i < Protocol.SectionBiomeCount && offset + 2 <= buf.Length; i++)
                    {
                        biomeIndices[i] = (ushort)ReadUInt16BE(buf, offset);
                        offset += 2;
                    }
                }
            }

            client.OnChunkSectionSnapshot(cx, cz, sy, palette, indices, blockLight, skyLight, biomePalette, biomeIndices);
            return true;
        }

        static bool ParseBlockDelta(byte[] buf, ChunkStreamClient client)
        {
            // type(1) + x(4) + y(4) + z(4) + len(2) + utf8
            if (buf.Length < 1 + 4 + 4 + 4 + 2) return false;
            int offset = 1;
            int x = ReadInt32BE(buf, offset); offset += 4;
            int y = ReadInt32BE(buf, offset); offset += 4;
            int z = ReadInt32BE(buf, offset); offset += 4;
            int strLen = ReadUInt16BE(buf, offset); offset += 2;
            if (offset + strLen > buf.Length) return false;
            string blockStateId = Encoding.UTF8.GetString(buf, offset, strLen);

            client.OnBlockDelta(x, y, z, blockStateId);
            return true;
        }

        static bool ParseBlockEntity(byte[] buf, ChunkStreamClient client)
        {
            if (buf.Length < 1 + 4 + 4 + 4 + 2 + 4) return false;
            int offset = 1;
            int x = ReadInt32BE(buf, offset); offset += 4;
            int y = ReadInt32BE(buf, offset); offset += 4;
            int z = ReadInt32BE(buf, offset); offset += 4;
            int typeLen = ReadUInt16BE(buf, offset); offset += 2;
            if (offset + typeLen + 4 > buf.Length) return false;
            string typeId = Encoding.UTF8.GetString(buf, offset, typeLen);
            offset += typeLen;
            int nbtLen = ReadInt32BE(buf, offset); offset += 4;
            byte[] nbt = null;
            if (nbtLen > 0)
            {
                if (offset + nbtLen > buf.Length) return false;
                nbt = new byte[nbtLen];
                Buffer.BlockCopy(buf, offset, nbt, 0, nbtLen);
            }
            client.OnBlockEntity(x, y, z, typeId, nbt);
            return true;
        }

        static bool ParseEntitySpawn(byte[] buf, ChunkStreamClient client)
        {
            if (buf.Length < 1 + 4 + 2 + 8 + 8 + 8 + 4 + 4) return false;
            int offset = 1;
            int entityId = ReadInt32BE(buf, offset); offset += 4;
            int typeLen = ReadUInt16BE(buf, offset); offset += 2;
            if (offset + typeLen + 8 + 8 + 8 + 4 + 4 > buf.Length) return false;
            string typeId = Encoding.UTF8.GetString(buf, offset, typeLen);
            offset += typeLen;
            double x = ReadDoubleBE(buf, offset); offset += 8;
            double y = ReadDoubleBE(buf, offset); offset += 8;
            double z = ReadDoubleBE(buf, offset); offset += 8;
            float yaw = ReadFloatBE(buf, offset); offset += 4;
            float pitch = ReadFloatBE(buf, offset);
            client.OnEntitySpawn(entityId, typeId, x, y, z, yaw, pitch);
            return true;
        }

        static long ReadInt64BE(byte[] buf, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return ((long)buf[offset] << 56) | ((long)buf[offset + 1] << 48) | ((long)buf[offset + 2] << 40) | ((long)buf[offset + 3] << 32)
                    | ((long)buf[offset + 4] << 24) | ((long)buf[offset + 5] << 16) | ((long)buf[offset + 6] << 8) | buf[offset + 7];
            }
            return BitConverter.ToInt64(buf, offset);
        }

        static double ReadDoubleBE(byte[] buf, int offset)
        {
            return BitConverter.Int64BitsToDouble(ReadInt64BE(buf, offset));
        }

        static float ReadFloatBE(byte[] buf, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                int i = (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];
                return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
            }
            return BitConverter.ToSingle(buf, offset);
        }

        static int ReadInt32BE(byte[] buf, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];
            }
            return BitConverter.ToInt32(buf, offset);
        }

        static int ReadUInt16BE(byte[] buf, int offset)
        {
            if (BitConverter.IsLittleEndian)
                return (buf[offset] << 8) | buf[offset + 1];
            return BitConverter.ToUInt16(buf, offset);
        }
    }
}

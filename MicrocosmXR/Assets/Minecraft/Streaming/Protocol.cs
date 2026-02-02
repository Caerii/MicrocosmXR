namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// Protocol constants matching the Fabric mod (StreamerWebSocketHandler).
    /// Text: HELLO &lt;version&gt;, SET_ORIGIN &lt;x0&gt; &lt;y0&gt; &lt;z0&gt; &lt;scale&gt;.
    /// Binary: byte type, then type-specific payload (big-endian).
    /// </summary>
    public static class Protocol
    {
        public const byte MsgHello = 0;
        public const byte MsgSetOrigin = 1;
        public const byte MsgChunkSectionSnapshot = 2;
        public const byte MsgBlockDelta = 3;
        public const byte MsgBlockEntity = 4;
        public const byte MsgEntitySpawn = 5;

        public const int SectionBlockCount = 16 * 16 * 16; // 4096
        public const int SectionBiomeCount = 4 * 4 * 4; // 64
        public const int DefaultPort = 25566;
    }

    /// <summary>
    /// Chunk section coordinate (cx, cz, section Y index).
    /// </summary>
    public struct ChunkSectionCoord
    {
        public int cx;
        public int cz;
        public int sy;

        public ChunkSectionCoord(int cx, int cz, int sy)
        {
            this.cx = cx;
            this.cz = cz;
            this.sy = sy;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkSectionCoord other && cx == other.cx && cz == other.cz && sy == other.sy;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + cx;
                h = h * 31 + cz;
                h = h * 31 + sy;
                return h;
            }
        }
    }
}

using System.Collections.Generic;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// One 16×16×16 chunk section: block palette + indices, block/sky light, biome palette + 64 indices.
    /// Block index layout: (localX + localZ*16 + localY*256). Light same layout. Biome: 4×4×4 quart grid.
    /// </summary>
    public class SectionData
    {
        public List<string> Palette { get; } = new List<string>();
        /// <summary> 4096 indices (x + z*16 + y*256), each index into Palette. </summary>
        public ushort[] Indices { get; } = new ushort[Protocol.SectionBlockCount];
        /// <summary> Block light 0–15 per block (4096 bytes). </summary>
        public byte[] BlockLight { get; } = new byte[Protocol.SectionBlockCount];
        /// <summary> Sky light 0–15 per block (4096 bytes). </summary>
        public byte[] SkyLight { get; } = new byte[Protocol.SectionBlockCount];
        public List<string> BiomePalette { get; } = new List<string>();
        /// <summary> 64 biome indices (4×4×4 quart grid). </summary>
        public ushort[] BiomeIndices { get; } = new ushort[Protocol.SectionBiomeCount];

        public string GetBlockStateId(int localX, int localY, int localZ)
        {
            int i = localX + localZ * 16 + localY * 256;
            int idx = Indices[i];
            if (idx >= 0 && idx < Palette.Count)
                return Palette[idx];
            return null;
        }

        public void SetBlockIndex(int localX, int localY, int localZ, ushort paletteIndex)
        {
            int i = localX + localZ * 16 + localY * 256;
            Indices[i] = paletteIndex;
        }

        /// <summary> Get block light at local position (0–15). </summary>
        public byte GetBlockLight(int localX, int localY, int localZ)
        {
            int i = localX + localZ * 16 + localY * 256;
            return i >= 0 && i < BlockLight.Length ? BlockLight[i] : (byte)0;
        }

        /// <summary> Get biome ID at quart position (qx,qy,qz in 0..3). </summary>
        public string GetBiomeId(int qx, int qy, int qz)
        {
            int i = qx + qz * 4 + qy * 16;
            if (i >= 0 && i < BiomeIndices.Length && BiomeIndices[i] < BiomePalette.Count)
                return BiomePalette[BiomeIndices[i]];
            return null;
        }

        /// <summary> Get or add palette index for a block state ID (for BLOCK_DELTA). </summary>
        public ushort GetOrAddPaletteIndex(string blockStateId)
        {
            for (int i = 0; i < Palette.Count; i++)
            {
                if (Palette[i] == blockStateId)
                    return (ushort)i;
            }
            Palette.Add(blockStateId);
            return (ushort)(Palette.Count - 1);
        }
    }
}

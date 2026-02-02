using System;
using System.Collections.Generic;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// Holds stream state: origin, scale, section data, and dirty sections for meshing.
    /// Feed with ChunkStreamParser.ParseText / ParseBinary after receiving WebSocket messages.
    /// </summary>
    public class ChunkStreamClient
    {
        public int OriginX { get; private set; }
        public int OriginY { get; private set; }
        public int OriginZ { get; private set; }
        public double Scale { get; private set; } = 0.02;
        public string ProtocolVersion { get; private set; }

        readonly Dictionary<ChunkSectionCoord, SectionData> sections = new Dictionary<ChunkSectionCoord, SectionData>();
        readonly HashSet<ChunkSectionCoord> dirty = new HashSet<ChunkSectionCoord>();

        public IReadOnlyDictionary<ChunkSectionCoord, SectionData> Sections => sections;
        public IReadOnlyCollection<ChunkSectionCoord> DirtySections => dirty;

        public event Action<string> OnHelloReceived;
        public event Action<int, int, int, double> OnOriginReceived;
        public event Action<ChunkSectionCoord> OnSectionReceived;
        public event Action<int, int, int, string> OnBlockDeltaReceived;
        public event Action<int, int, int, string, byte[]> OnBlockEntityReceived;
        public event Action<int, string, double, double, double, float, float> OnEntitySpawnReceived;

        public void OnHello(string version)
        {
            ProtocolVersion = version;
            OnHelloReceived?.Invoke(version);
        }

        public void OnSetOrigin(int x0, int y0, int z0, double scale)
        {
            OriginX = x0;
            OriginY = y0;
            OriginZ = z0;
            Scale = scale;
            OnOriginReceived?.Invoke(x0, y0, z0, scale);
        }

        public void OnChunkSectionSnapshot(int cx, int cz, int sy, List<string> palette, ushort[] indices)
        {
            var key = new ChunkSectionCoord(cx, cz, sy);
            var data = new SectionData();
            data.Palette.AddRange(palette);
            if (indices != null && indices.Length >= Protocol.SectionBlockCount)
                Array.Copy(indices, data.Indices, Protocol.SectionBlockCount);
            sections[key] = data;
            dirty.Add(key);
            OnSectionReceived?.Invoke(key);
        }

        public void OnBlockDelta(int x, int y, int z, string blockStateId)
        {
            int cx = x >> 4;
            int cz = z >> 4;
            int sy = y >> 4;
            var key = new ChunkSectionCoord(cx, cz, sy);
            if (!sections.TryGetValue(key, out var section))
                return;
            int lx = x & 15;
            int ly = y & 15;
            int lz = z & 15;
            ushort idx = section.GetOrAddPaletteIndex(blockStateId);
            section.SetBlockIndex(lx, ly, lz, idx);
            dirty.Add(key);
            OnBlockDeltaReceived?.Invoke(x, y, z, blockStateId);
        }

        public void OnBlockEntity(int x, int y, int z, string typeId, byte[] nbt)
        {
            OnBlockEntityReceived?.Invoke(x, y, z, typeId, nbt);
        }

        public void OnEntitySpawn(int entityId, string typeId, double x, double y, double z, float yaw, float pitch)
        {
            OnEntitySpawnReceived?.Invoke(entityId, typeId, x, y, z, yaw, pitch);
        }

        /// <summary> Call after meshing a section to clear its dirty flag. </summary>
        public void ClearDirty(ChunkSectionCoord key)
        {
            dirty.Remove(key);
        }

        /// <summary> Clear all dirty flags. </summary>
        public void ClearAllDirty()
        {
            dirty.Clear();
        }

        public bool TryGetSection(ChunkSectionCoord key, out SectionData data)
        {
            return sections.TryGetValue(key, out data);
        }
    }
}

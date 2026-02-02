using UnityEngine;
using System.Collections.Generic;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// Stub for chunk meshing. Subscribe to ChunkStreamClient and build meshes for dirty sections.
    /// TODO: Implement visible-faces-only meshing, greedy quad merging, and block atlas UVs.
    /// Position chunks at: TableAnchor + scale * (chunkWorldPos - origin_mc).
    /// </summary>
    public class ChunkMesherStub : MonoBehaviour
    {
        [Tooltip("MinecraftStreamBehaviour that holds the ChunkStreamClient.")]
        public MinecraftStreamBehaviour stream;
        [Tooltip("Scale from Minecraft blocks to Unity units (from SET_ORIGIN).")]
        public float scale = 0.02f;
        [Tooltip("Origin in Minecraft coords (from SET_ORIGIN).")]
        public Vector3 originMc;
        [Tooltip("Optional table anchor; if set, chunk root is under this transform.")]
        public Transform tableAnchor;

        void Start()
        {
            if (stream != null)
            {
                stream.Client.OnSectionReceived += OnSectionReceived;
                stream.Client.OnBlockDeltaReceived += OnBlockDeltaReceived;
                stream.Client.OnOriginReceived += OnOriginReceived;
            }
        }

        void OnDestroy()
        {
            if (stream != null)
            {
                stream.Client.OnSectionReceived -= OnSectionReceived;
                stream.Client.OnBlockDeltaReceived -= OnBlockDeltaReceived;
                stream.Client.OnOriginReceived -= OnOriginReceived;
            }
        }

        void OnOriginReceived(int x0, int y0, int z0, double s)
        {
            originMc = new Vector3(x0, y0, z0);
            scale = (float)s;
        }

        void OnSectionReceived(ChunkSectionCoord key)
        {
            // TODO: Build mesh for section (cx, cz, sy). Mark non-dirty when done.
            // World position of section: (key.cx * 16, key.sy * 16, key.cz * 16)
            // Unity position: tableAnchor.position + scale * (sectionWorldMc - originMc)
            if (stream.Client.TryGetSection(key, out var section))
            {
                // Placeholder: just log. Replace with MeshFilter + MeshRenderer per section.
                Debug.Log($"[ChunkMesher] Section received cx={key.cx} cz={key.cz} sy={key.sy} palette={section.Palette.Count}");
                stream.Client.ClearDirty(key);
            }
        }

        void OnBlockDeltaReceived(int x, int y, int z, string blockStateId)
        {
            // Section is already marked dirty by ChunkStreamClient. Mesher will rebuild on next update.
            // TODO: Only rebuild the affected section mesh.
        }

        /// <summary>
        /// Call from Update or coroutine to process dirty sections and build/update meshes.
        /// </summary>
        public void ProcessDirtySections()
        {
            if (stream?.Client == null) return;
            foreach (var key in new List<ChunkSectionCoord>(stream.Client.DirtySections))
            {
                if (stream.Client.TryGetSection(key, out var section))
                {
                    BuildSectionMesh(key, section);
                    stream.Client.ClearDirty(key);
                }
            }
        }

        void BuildSectionMesh(ChunkSectionCoord key, SectionData section)
        {
            // Stub: create a placeholder cube or empty mesh per section.
            // Full impl: iterate 16^3 blocks, emit only visible faces, greedy mesh same material, atlas UVs.
            Vector3 sectionWorldMc = new Vector3(key.cx * 16, key.sy * 16, key.cz * 16);
            Vector3 unityPos = (tableAnchor != null ? tableAnchor.position : Vector3.zero)
                + scale * (sectionWorldMc - originMc);
            // TODO: Instantiate mesh at unityPos or add to combined chunk mesh.
        }
    }
}

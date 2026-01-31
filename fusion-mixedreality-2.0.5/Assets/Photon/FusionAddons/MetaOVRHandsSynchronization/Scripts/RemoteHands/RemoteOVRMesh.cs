using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE

    /**
     * Replacement of OVRMesh to handle an issue with ShouldInitialize: cannot be used for remote hands in the editor, as there is a check on local finger tracking
     * See https://communityforums.atmeta.com/t5/Unity-VR-Development/Using-OVRHand-for-remote-hands-in-editor-issue-in-OVRSkeleton/td-p/1101040 
     */
    [RequireComponent(typeof(RemoteOVRSkeleton))]
    public class RemoteOVRMesh : MonoBehaviour
    {
        public interface IOVRMeshDataProvider
        {
            MeshType GetMeshType();
        }

        public enum MeshType
        {
            None = OVRPlugin.MeshType.None,
            HandLeft = OVRPlugin.MeshType.HandLeft,
            HandRight = OVRPlugin.MeshType.HandRight,
        }

        [SerializeField]
        private IOVRMeshDataProvider _dataProvider;

        [SerializeField]
        private MeshType _meshType = MeshType.None;

        private Mesh _mesh;

        public bool IsInitialized { get; private set; }

        public Mesh Mesh
        {
            get => _mesh;
        }

        internal MeshType GetMeshType()
        {
            return _meshType;
        }

        internal void SetMeshType(MeshType type)
        {
            _meshType = type;
        }

        private void Awake()
        {
            if (_dataProvider == null)
            {
                _dataProvider = GetComponent<IOVRMeshDataProvider>();
            }

            if (_dataProvider != null)
            {
                _meshType = _dataProvider.GetMeshType();
            }

            if (ShouldInitialize())
            {
                Initialize(_meshType);
            }
        }

        private bool ShouldInitialize()
        {
            if (IsInitialized)
            {
                return false;
            }

            if (_meshType == MeshType.None)
            {
                return false;
            }
            else if (_meshType == MeshType.HandLeft || _meshType == MeshType.HandRight)
            {
                return true;
            }
            else
            {
                return true;
            }
        }

        private void Initialize(MeshType meshType)
        {
            _mesh = new Mesh();
            if (OVRPlugin.GetMesh((OVRPlugin.MeshType)_meshType, out var ovrpMesh))
            {
                TransformOvrpMesh(ovrpMesh, _mesh);
                IsInitialized = true;
            }
        }

        private void TransformOvrpMesh(OVRPlugin.Mesh ovrpMesh, Mesh mesh)
        {
            int numVertices = (int)ovrpMesh.NumVertices;
            int numIndices = (int)ovrpMesh.NumIndices;

            using (var verticesNativeArray =
                   new OVRMeshJobs.NativeArrayHelper<OVRPlugin.Vector3f>(ovrpMesh.VertexPositions, numVertices))
            using (var normalsNativeArray =
                   new OVRMeshJobs.NativeArrayHelper<OVRPlugin.Vector3f>(ovrpMesh.VertexNormals, numVertices))
            using (var uvNativeArray =
                   new OVRMeshJobs.NativeArrayHelper<OVRPlugin.Vector2f>(ovrpMesh.VertexUV0, numVertices))
            using (var weightsNativeArray =
                   new OVRMeshJobs.NativeArrayHelper<OVRPlugin.Vector4f>(ovrpMesh.BlendWeights, numVertices))
            using (var indicesNativeArray =
                   new OVRMeshJobs.NativeArrayHelper<OVRPlugin.Vector4s>(ovrpMesh.BlendIndices, numVertices))
            using (var trianglesNativeArray = new OVRMeshJobs.NativeArrayHelper<short>(ovrpMesh.Indices, numIndices))
            using (var vertices = new NativeArray<Vector3>(numVertices, Unity.Collections.Allocator.TempJob))
            using (var normals = new NativeArray<Vector3>(numVertices, Unity.Collections.Allocator.TempJob))
            using (var uv = new NativeArray<Vector2>(numVertices, Unity.Collections.Allocator.TempJob))
            using (var boneWeights = new NativeArray<BoneWeight>(numVertices, Unity.Collections.Allocator.TempJob))
            using (var triangles = new NativeArray<uint>(numIndices, Unity.Collections.Allocator.TempJob))
            {
                var job = new OVRMeshJobs.TransformToUnitySpaceJob
                {
                    Vertices = vertices,
                    Normals = normals,
                    UV = uv,
                    BoneWeights = boneWeights,
                    MeshVerticesPosition = verticesNativeArray.UnityNativeArray,
                    MeshNormals = normalsNativeArray.UnityNativeArray,
                    MeshUV = uvNativeArray.UnityNativeArray,
                    MeshBoneWeights = weightsNativeArray.UnityNativeArray,
                    MeshBoneIndices = indicesNativeArray.UnityNativeArray
                };

                var jobTransformTriangle = new OVRMeshJobs.TransformTrianglesJob
                {
                    Triangles = triangles,
                    MeshIndices = trianglesNativeArray.UnityNativeArray,
                    NumIndices = numIndices
                };

                var handle = job.Schedule(numVertices, 20);
                var handleTriangleJob = jobTransformTriangle.Schedule(numIndices, 60);
                JobHandle.CombineDependencies(handle, handleTriangleJob).Complete();

                mesh.SetVertices(job.Vertices);
                mesh.SetNormals(job.Normals);
                mesh.SetUVs(0, job.UV);
                mesh.boneWeights = job.BoneWeights.ToArray();

                mesh.SetIndexBufferParams(numIndices, IndexFormat.UInt32);
                mesh.SetIndexBufferData(jobTransformTriangle.Triangles, 0, 0, numIndices);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, numIndices));
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (ShouldInitialize())
            {
                Initialize(_meshType);
            }
        }
#endif
    }
#else
    public class RemoteOVRMesh : MonoBehaviour {}
#endif
}

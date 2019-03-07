using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Chunks;
using VoxelEngine.Chunks.MeshGeneration;

namespace VoxelEngine.Components {
    /// <summary>
    /// Represents a chunk in the Unity game world.
    /// Wraps the VoxelChunk object.
    /// </summary>
    public class ChunkGameObject : MonoBehaviour {     
        // Chunk Reference & Data
        private Vector2Int _chunkPos;
        private Chunk _chunk;
        
        // Required Sub-Mesh Objects
        [SerializeField] private ChunkMeshObject _solid;
        [SerializeField] private ChunkMeshObject _nonSolid;
        
        // Mesh Data
        private Mesh _solidMesh;
        private Mesh _nonSolidMesh;

        // Latest Job
        private int _latestJob;
        private bool _waitingForLight;
        
        // Initialization
        public void Initialize(Vector2Int chunkPos) {
            // Reference chunk from the world system
            _chunk = WorldSystem.Instance.VoxelWorld.TryGetChunk(chunkPos, true);
            
            // Initialize mesh sub-objects
            _solid.Initialize(_chunk);
            _nonSolid.Initialize(_chunk);
            
            // Initialize meshes
            _solidMesh = new Mesh();
            _nonSolidMesh = new Mesh();
            
            // Register with the chunk update event
            _chunk.OnChunkUpdate += OnChunkUpdate;
        }
        
        /// <summary>
        /// Chunk update event handler.
        /// </summary>
        private void OnChunkUpdate() {
            // Create a chunk mesh job and dispatch it to the world object
            WorldSystem.Instance.UpdateJobs.Add(new ChunkUpdateJob(_latestJob, WorldSystem.Instance.VoxelWorld, _chunk, OnMeshComplete, null));
        }
        
        /// <summary>
        /// Processes mesh result data and updates the mesh renderer and collider.
        /// </summary>
        private void OnMeshComplete(ChunkMeshResult result) {
            // Discard results from outdated jobs
            if (result.Id != _latestJob) return;
            
            // Update solid mesh and sub-object
            UpdateMeshData(_solidMesh, result.Solid);
            _solid.UpdateMesh(_solidMesh);
            
            // Update non-solid mesh and sub-object
            UpdateMeshData(_nonSolidMesh, result.NonSolid);
            _nonSolid.UpdateMesh(_nonSolidMesh);
            
            // Flag chunk light as dirty when we receive a mesh update
            // TODO: This is currently done to avoid missed light updates
            _chunk.IsLightDirty = true;
            _latestJob++;
        }

        private void UpdateMeshData(Mesh mesh, MeshData data) {
            // Create a new mesh object if null
            if (mesh == null) {
                mesh = new Mesh {
                    vertices = data.Vertices, 
                    uv = data.Uv0,
                    uv2 = data.Uv1,
                    uv3 = data.Uv2,
                    uv4 = data.Uv3,
                    colors = data.VertexColors
                };
            }
            
            // Update the existing mesh
            else {
                mesh.Clear();
                mesh.vertices = data.Vertices;
                mesh.uv = data.Uv0;
                mesh.uv2 = data.Uv1;
                mesh.uv3 = data.Uv2;
                mesh.uv4 = data.Uv3;
                mesh.colors = data.VertexColors;
            }
            
            // Set triangles and sub-meshes based on data
            mesh.subMeshCount = data.Triangles.Length;
            for (var i = 0; i < data.Triangles.Length; i++) {
                mesh.SetTriangles(data.Triangles[i], i);
            }
            
            // Calculate normals
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            mesh.MarkDynamic();
        }
        
        // Unity Update
        private void Update() {     
            // Check if the chunk mesh is dirty and invoke an update
            if (_chunk.IsMeshDirty) _chunk.Update();
            
            // Update mesh vertex colors if light is dirty
            if (_chunk.IsLightDirty) {
                lock (_chunk) {
                    _solid.UpdateVertexColors();
                    _nonSolid.UpdateVertexColors();
                    _chunk.IsLightDirty = false;
                }
            }
            
            if (Input.GetKeyDown(KeyCode.F5)) {
                // Create a chunk mesh job and dispatch it to the world object
                _chunk.Update();
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Chunks;
using VoxelEngine.MeshGeneration;

namespace VoxelEngine {
    /// <summary>
    /// Represents a chunk in the Unity game world.
    /// Wraps the VoxelChunk object.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ChunkGameObject : MonoBehaviour {
        // World Object and Chunk References
        private WorldGameObject _worldObject;
        private Chunk _chunk;
        
        // Required References
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        
        // Mesh Data
        private Mesh _visualMesh;
        private Mesh _colliderMesh;
        
        // Latest Job
        private int _latestJob;
        
        // State
        private bool _pendingInitialMesh;
        
        // Initialization
        public void Initialize(WorldGameObject worldObject, Chunk chunk) {
            // Assign world object and chunk references
            _worldObject = worldObject;
            _chunk = chunk;
            
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
            _chunk.OnChunkUpdate += OnChunkUpdate;
            _pendingInitialMesh = true;
        }
        
        /// <summary>
        /// Chunk update event handler.
        /// </summary>
        public void OnChunkUpdate() {
            // Create a chunk mesh job and dispatch it to the world object
            _latestJob = _latestJob + 1;
            _worldObject.MeshJobs.Add(new MeshJob(_latestJob + 1, WorldGameObject.World, _chunk, OnMeshComplete));
        }
        
        /// <summary>
        /// Processes mesh result data and updates the mesh renderer and collider.
        /// </summary>
        /// <param name="result"></param>
        public void OnMeshComplete(MeshResult result) {
            // Discard results from outdated jobs
            if (result.Id < _latestJob) return;
            
            // Create or clear the visual mesh object
            if (_visualMesh == null) {
                _visualMesh = new Mesh {
                    vertices = result.Vertices, 
                    colors = result.Colors,
                    triangles = new int[result.Triangles.Length], 
                    uv = result.UVs
                    
                };
            }
            else {
                _visualMesh.Clear();
                _visualMesh.vertices = result.Vertices;
                _visualMesh.colors = result.Colors;
                _visualMesh.triangles = new int[result.Triangles.Length];
                _visualMesh.uv = result.UVs;
            }
            
            // Parse submesh data into proper indices
            var submeshData = new Dictionary<int, List<int>>();    
            foreach (var triangleIndex in result.Triangles) {
                if (!submeshData.ContainsKey(triangleIndex.SubmeshIndex)) {
                    submeshData.Add(triangleIndex.SubmeshIndex, new List<int>());
                }     
                submeshData[triangleIndex.SubmeshIndex].Add(triangleIndex.VertexIndex);
            }
            
            // Set triangles based on submesh data
            _visualMesh.subMeshCount = submeshData.Count + 1;
            foreach (var kvp in submeshData) {
                _visualMesh.SetTriangles(kvp.Value, kvp.Key);
            }
            
            // Calculate normals
            _visualMesh.RecalculateNormals();
            
            // Update mesh renderer and collider references
            //if (_meshFilter.mesh == null || _meshFilter.mesh != _visualMesh) 
                _meshFilter.sharedMesh = _visualMesh;
            
            //if (_meshCollider.sharedMesh == null || _meshCollider.sharedMesh != _visualMesh) 
                _meshCollider.sharedMesh = _visualMesh;
        }
        
        // Unity Update
        private void Update() {
            // Request generation on initial mesh as soon as we can
            if (_pendingInitialMesh && _chunk.Initialized) {
                OnChunkUpdate();
                _pendingInitialMesh = false;
            }
            
            if (Input.GetKeyDown(KeyCode.F5)) {
                // Create a chunk mesh job and dispatch it to the world object
                _chunk.Update();
            }
        }
    }
}
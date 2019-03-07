using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Chunks;

namespace VoxelEngine.Components {
    /// <summary>
    /// Represents a mesh containing all voxels in a chunk of s specific render type.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ChunkMeshObject : MonoBehaviour {
        // Required Components
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        
        // Chunk Reference
        private Chunk _chunk;
        
        // Mesh Data
        private Mesh _mesh;
        private List<Color> _vertexColors;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        
        // State
        private bool _initialized;
        
        // Initialization
        public void Initialize(Chunk chunk) {
            // Exit if already initialized
            if (_initialized) return;
            
            // Reference required components
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();
            
            // Set chunk reference
            _chunk = chunk;
            
            // Initialize vertex color buffer
            _vertexColors = new List<Color>(16384);
            
            // Set material texture references
            for (var i = 0; i < 3; i++) {
                _meshRenderer.sharedMaterials[i].SetTexture("_MainTex",  WorldSystem.Instance.AtlasGenerator.AlbedoAtlas); 
                _meshRenderer.sharedMaterials[i].SetTexture("_EmitTex",  WorldSystem.Instance.AtlasGenerator.EmissionAtlas); 
                _meshRenderer.sharedMaterials[i].SetTexture("_MetalTex",  WorldSystem.Instance.AtlasGenerator.MetallicAtlas); 
                _meshRenderer.sharedMaterials[i].SetTexture("_SmoothTex",  WorldSystem.Instance.AtlasGenerator.SmoothnessAtlas); 
            }
            
            // Set initialized flag
            _initialized = true;
        }

        // Update Meshes
        public void UpdateMesh(Mesh mesh) {
            _mesh = mesh;
            _meshFilter.mesh = mesh;
            _meshCollider.sharedMesh = mesh;
            _vertices = mesh.vertices;
            _normals = mesh.normals;
        }

        /// <summary>
        /// Updates mesh vertex colors with the latest light map data.
        /// </summary>
        public void UpdateVertexColors() {
            if (_vertices == null) return;
            // Iterate over the vertex colors of the current mesh
            // 4 vertices per face
            for (var i = 0; i < _vertices.Length; i += 4) {
                // Get positions of the 4 face vertices and normal
                var n0 = _normals[i];
                var v0 = _vertices[i];
                var v1 = _vertices[i + 1];
                var v2 = _vertices[i + 2];
                var v3 = _vertices[i + 3];

                // Calculate average of the 4 face vertices
                var vAvg = (v0 + v1 + v2 + v3) / 4f;

                // Calculate voxel position from vAvg
                var voxelPos = new Vector3Int(Mathf.RoundToInt(vAvg.x + n0.x * 0.1f),
                    Mathf.RoundToInt(vAvg.y + n0.y * 0.1f),
                    Mathf.RoundToInt(vAvg.z + n0.z * 0.1f));

                // Determine if the voxel lies in a neighboring chunk
                var voxelInAdjacent = voxelPos.x < 0 || voxelPos.x >= Chunk.CHUNK_LENGTH ||
                                      voxelPos.z < 0 || voxelPos.z >= Chunk.CHUNK_LENGTH;
                
                // Declare light values
                var blockLight = 0;
                var sunlight = 0;

                // Fetch light data from the containing chunk depending on locality
                if (voxelInAdjacent) {
                    var worldPos = new Vector3Int(voxelPos.x + _chunk.WorldPosition.x,
                        voxelPos.y,
                        voxelPos.z + _chunk.WorldPosition.y);

                    var neighborChunk = WorldSystem.Instance.VoxelWorld.TryGetChunk(new Vector2Int(worldPos.x >> Chunk.CHUNK_LENGTH_LOG, 
                                                                                                   worldPos.z >> Chunk.CHUNK_LENGTH_LOG));

                    // Determine if the neighbor is in a neighboring chunk
                    if (neighborChunk != null) {
                        // Calculate voxel position relative to the neighbor chunk
                        var neighborLocalPos = new Vector3Int(worldPos.x - neighborChunk.WorldPosition.x,
                                                              worldPos.y,
                                                              worldPos.z - neighborChunk.WorldPosition.y);
                        
                        // Fetch voxel and lighting data
                        blockLight = neighborChunk.GetBlockLight(neighborLocalPos);
                        sunlight = neighborChunk.GetSunlight(neighborLocalPos);
                    }
                }
                else {
                    blockLight = _chunk.GetBlockLight(voxelPos);
                    sunlight = _chunk.GetSunlight(voxelPos);
                }

                // Update vertex colors
                var vColor = new Color(sunlight / 15f, blockLight / 15f, 0f);
                _vertexColors.Add(vColor);
                _vertexColors.Add(vColor);
                _vertexColors.Add(vColor);
                _vertexColors.Add(vColor);
            }

            // Update mesh vertex colors
            _mesh.SetColors(_vertexColors);
            _vertexColors.Clear();
        }
    }
}
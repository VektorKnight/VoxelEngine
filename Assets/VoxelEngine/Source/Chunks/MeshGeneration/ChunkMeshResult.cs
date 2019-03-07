using System;
using UnityEngine;

namespace VoxelEngine.Chunks.MeshGeneration {
    public struct ChunkMeshResult {
        // Job ID
        public int Id;
        
        // Mesh Data
        public MeshData Solid;
        public MeshData NonSolid;
        
        // Callback
        public Action<ChunkMeshResult> Callback;
    }
}
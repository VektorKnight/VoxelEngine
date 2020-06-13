using System;
using UnityEngine;

namespace VoxelEngine.Chunks.MeshGeneration {
    public readonly struct ChunkMeshResult {
        // Job ID
        public readonly int Id;
        
        // Mesh Data
        public readonly MeshData Solid;
        public readonly MeshData NonSolid;
        
        // Callback
        public readonly Action<ChunkMeshResult> Callback;

        public ChunkMeshResult(int id, MeshData solid, MeshData nonSolid, Action<ChunkMeshResult> callback) {
            Id = id;
            Solid = solid;
            NonSolid = nonSolid;
            Callback = callback;
        }
    }
}
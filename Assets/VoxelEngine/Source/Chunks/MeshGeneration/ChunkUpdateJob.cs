using System;
using UnityEngine;
using VoxelEngine.Chunks.LightMapping;

namespace VoxelEngine.Chunks.MeshGeneration {
    public struct ChunkUpdateJob {
        public readonly int Id;
        public readonly VoxelWorld World;
        public readonly Chunk Chunk;
        public readonly Action<ChunkMeshResult> MeshCallback;
        public readonly Action<ChunkLightResult> LightCallback;

        public ChunkUpdateJob(int id, VoxelWorld world, Chunk chunk, Action<ChunkMeshResult> meshCallback, Action<ChunkLightResult> lightCallback) {
            Id = id;
            World = world;
            Chunk = chunk;
            MeshCallback = meshCallback;
            LightCallback = lightCallback;
        }
    }
}
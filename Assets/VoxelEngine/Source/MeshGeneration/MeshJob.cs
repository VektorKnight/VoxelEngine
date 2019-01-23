using System;
using VoxelEngine.Chunks;

namespace VoxelEngine.MeshGeneration {
    public struct MeshJob {
        public readonly int Id;
        public readonly VoxelWorld World;
        public readonly Chunk Chunk;
        public readonly Action<MeshResult> Callback;

        public MeshJob(int id, VoxelWorld world, Chunk chunk, Action<MeshResult> callback) {
            Id = id;
            World = world;
            Chunk = chunk;
            Callback = callback;
        }
    }
}
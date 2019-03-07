using System;
using UnityEngine;

namespace VoxelEngine.Chunks.LightMapping {
    public struct ChunkLightResult {
        public readonly int Id;
        public readonly Action<ChunkLightResult> LightCallback;

        public ChunkLightResult(int id, Action<ChunkLightResult> lightCallback) {
            Id = id;
            LightCallback = lightCallback;
        }
    }
}
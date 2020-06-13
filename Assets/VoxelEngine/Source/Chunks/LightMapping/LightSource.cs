using UnityEngine;

namespace VoxelEngine.Chunks.LightMapping {
    public readonly struct LightSource {
        public readonly Vector3Int Position;
        public readonly int Value;

        public LightSource(Vector3Int position, int value) {
            Position = position;
            Value = value;
        }
    }
}
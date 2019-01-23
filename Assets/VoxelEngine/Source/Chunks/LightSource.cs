using UnityEngine;

namespace VoxelEngine.Chunks {
    public struct LightSource {
        public readonly Vector3Int Position;
        public readonly int Value;

        public LightSource(Vector3Int position, int value) {
            Position = position;
            Value = value;
        }
    }
}
using System.Runtime.InteropServices;
using UnityEngine;
using VoxelEngine.Chunks;

namespace VoxelEngine {
    [StructLayout(LayoutKind.Sequential)]
    public struct LightRemovalNode {
        public readonly Vector3Int Position;
        public readonly int Value;
        public readonly Chunk Chunk;

        public LightRemovalNode(Vector3Int position, int value, Chunk chunk) {
            Position = position;
            Value = value;
            Chunk = chunk;
        }
    }
}
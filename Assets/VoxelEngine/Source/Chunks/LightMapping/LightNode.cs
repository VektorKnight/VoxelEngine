using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelEngine.Chunks.LightMapping {
    [StructLayout(LayoutKind.Sequential)]
    public struct LightNode {
        public readonly Vector3Int Position;
        public readonly Chunk Chunk;

        public LightNode(Vector3Int position, Chunk chunk) {
            Position = position;
            Chunk = chunk;
        }
    }
}
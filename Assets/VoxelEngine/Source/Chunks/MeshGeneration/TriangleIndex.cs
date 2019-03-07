using System.Runtime.InteropServices;

namespace VoxelEngine.Chunks.MeshGeneration {
    [StructLayout(LayoutKind.Sequential)]
    public struct TriangleIndex {
        public readonly int VertexIndex;
        public readonly int SubmeshIndex;

        public TriangleIndex(int vertexIndex, int submeshIndex) {
            VertexIndex = vertexIndex;
            SubmeshIndex = submeshIndex;
        }
    }
}
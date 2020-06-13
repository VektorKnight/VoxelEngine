using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Chunks.MeshGeneration {
    public readonly struct MeshData {
        // Mesh Vertices / Triangles
        public readonly Vector3[] Vertices;

        public readonly int[][] Triangles;

        // Mesh UV Data
        public readonly Vector2[] Uv0;
        public readonly Vector2[] Uv1;
        public readonly Vector2[] Uv2;
        public readonly Vector2[] Uv3;
        public readonly Color[] VertexColors;

        public MeshData(Vector3[] vertices, int[][] triangles, Vector2[] uv0, Vector2[] uv1, Vector2[] uv2, Vector2[] uv3, Color[] vertexColors) {
            Vertices = vertices;
            Triangles = triangles;
            Uv0 = uv0;
            Uv1 = uv1;
            Uv2 = uv2;
            Uv3 = uv3;
            VertexColors = vertexColors;
        }
    }
}
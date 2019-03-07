using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Chunks.MeshGeneration {
    public struct MeshData {
        // Mesh Vertices / Triangles
        public Vector3[] Vertices;

        public int[][] Triangles;

        // Mesh UV Data
        public Vector2[] Uv0;
        public Vector2[] Uv1;
        public Vector2[] Uv2;
        public Vector2[] Uv3;
        public Color[] VertexColors;
    }
}
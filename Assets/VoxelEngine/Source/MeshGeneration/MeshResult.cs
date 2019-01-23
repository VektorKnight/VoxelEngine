using System;
using UnityEngine;

namespace VoxelEngine.MeshGeneration {
    public struct MeshResult {
        public readonly int Id;
        public readonly Vector3[] Vertices;
        public readonly Color[] Colors;
        public readonly TriangleIndex[] Triangles;
        public readonly Vector2[] UVs;
        public readonly Action<MeshResult> Callback;

        public MeshResult(int id, Vector3[] vertices, Color[] colors, TriangleIndex[] triangles, Vector2[] uvs, Action<MeshResult> callback) {
            Id = id;
            Vertices = vertices;
            Colors = colors;
            Triangles = triangles;
            UVs = uvs;
            Callback = callback;
        }
    }
}
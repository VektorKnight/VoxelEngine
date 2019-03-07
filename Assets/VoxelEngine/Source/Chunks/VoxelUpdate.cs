using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Chunks {
    public struct VoxelUpdate {
        public readonly Vector3Int Position;
        public readonly Voxel Voxel;

        public VoxelUpdate(Vector3Int position, Voxel voxel) {
            Position = position;
            Voxel = voxel;
        }
    }
}
using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Biomes {
    public interface IBiome {
        void SetSeed(int seed);
        float GetNoiseValue(int x, int y);
        Voxel GetVoxelAtHeight(int height, int y);
    }
}
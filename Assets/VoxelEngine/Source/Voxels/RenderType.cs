using System;

namespace VoxelEngine.Voxels {
    [Serializable]
    public enum RenderType : byte {
        None,
        Opaque,
        Cutout,
        Alpha,
        Custom
    }
}
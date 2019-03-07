using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelEngine.Voxels {
    /// <summary>
    /// Holds all general data for a particular voxel.
    /// Side-specific texture names should be regarded as suffixes to the voxel name.
    /// If the voxel references another voxel for texture data, you can prefix the texture name with a $ symbol.
    /// If the associated voxel uses per-side textures, you will need to include the suffix.
    /// Ex: "$dirt", "$grass_side", "$gravel".
    /// Side textures are wrapped in the following order:
    /// Z+, X+, Z-, X-, Y+, Y- (North, East, South, West, Top, Bottom)
    /// </summary>
    public class VoxelDefinition {
        // General Data
        public string InternalName;    // Name used internally for lookups
        public string FriendlyName;    // Name visible to players
        public string Description;     // Basic description of the voxel.
        
        // Type Data
        public VoxelType VoxelType;
        public RenderType RenderType;
        public byte Hardness;
        
        
        // Lighting Data
        public int LightValue;
        public int Attenuation;
        
        // Texture Atlas Flags
        public bool UsesTextureAtlas;
        public bool PerSideTextures;
        public string[] SideTextureNames;
        
        // Texture Atlas Indices
        public int[] AlbedoIndicies;
        public int[] EmissionIndicies;
        public int[] MetallicIndicies;
        public int[] SmoothnessIndicies;
        
        // Atlas UV Rects
        public Rect[] AlbedoRects;
        public Rect[] EmissionRects;
        public Rect[] MetallicRects;
        public Rect[] SmoothnessRects;
    }
}
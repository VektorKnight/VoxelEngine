using System.Runtime.InteropServices;

namespace VoxelEngine.Voxels {
    public class MaterialData {
        // General Stuff
        public ushort Id;
        public MaterialType Type;
        public byte Hardness;
        public bool IsTransparent;
        
        // Light Stuff
        public bool IsLightSource;
        public int LightValue;
        public int LightAttenuation;
        
        // Texture Atlas Stuff
        public bool UsesTextureAtlas;
        public bool PerSideTextures;
        public int[] TextureIndices; // Top, Sides, Bottom

        public MaterialData(ushort id, MaterialType materialType, byte hardness, bool isTransparent, bool usesTextureAtlas, bool perSideTextures, bool isLightSource = false, int lightValue = 0, int lightAttenuation = 0) {
            Id = id;
            Type = materialType;
            Hardness = hardness;
            IsTransparent = isTransparent;
            UsesTextureAtlas = usesTextureAtlas;
            PerSideTextures = perSideTextures;
            TextureIndices = perSideTextures ? new int[3] : new int[1];
            IsLightSource = isLightSource;
            LightValue = lightValue;
            LightAttenuation = 0;
        }
    }
}
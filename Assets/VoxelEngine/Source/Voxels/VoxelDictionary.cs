using System.Collections.Generic;

namespace VoxelEngine.Voxels {
    /// <summary>
    /// Contains definitions and name-id mappings for all voxels in an instance of the game.
    /// Voxels should never be referenced by numerical ID in code as these are subject to change.
    /// Instead the voxel ID should be queried at runtime via string name with the ID cached.
    /// Any saved games or blueprints of any type should store a copy of the name-id mapping
    /// for reconciliation with future versions of the game or instances of the game which have been modified.
    /// </summary>
    public static class VoxelDictionary {
        private static Dictionary<string, ushort> _idMapping = new Dictionary<string, ushort>();
        public static  List<VoxelDefinition> VoxelData;
        
        /// <summary>
        /// Returns the numerical ID linked to the given voxel name.
        /// </summary>
        public static ushort IdByName(string name) {
            return (ushort)_idMapping[name];
        }
        
        /// <summary>
        /// Initializes the voxel dictionary, registers the internal voxels,
        /// and prepares the dictionary for additional voxel definitions (mods).
        /// </summary>
        public static void InitializeVoxelDictionary() {
            _idMapping = new Dictionary<string, ushort>();
            VoxelData = new List<VoxelDefinition>();
            
            // Register internal voxels at initialization
            RegisterInternalVoxels();
        }
        
        /// <summary>
        /// Registers all internal voxel definitions for the base game.
        /// </summary>
        private static void RegisterInternalVoxels() {
            // Declare list to hold internal voxel definitions
            var internalVoxels = new List<VoxelDefinition> {
                // Air
                new VoxelDefinition {
                    InternalName = "air",
                    FriendlyName = "Air",
                    Description = "Represents empty space.",
                    VoxelType = VoxelType.None,
                    RenderType = RenderType.None,
                },
                
                // Stone
                new VoxelDefinition {
                    InternalName = "stone",
                    FriendlyName = "Stone",
                    Description = "Versatile building material.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 8,
                    UsesTextureAtlas = true
                },
                
                // Dirt
                new VoxelDefinition {
                    InternalName = "dirt",
                    FriendlyName = "Dirt",
                    Description = "Common surface material.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 0,
                    UsesTextureAtlas = true
                },
                
                // Grass
                new VoxelDefinition {
                    InternalName = "grass",
                    FriendlyName = "Grass",
                    Description = "Common surface material.\n" +
                                  "Spreads to neighboring dirt blocks when exposed to light.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 1,
                    UsesTextureAtlas = true,
                    PerSideTextures = true,
                    SideTextureNames = new[] {"side", "side", "side", "side", "top", "$dirt"},
                },
                
                // Sand
                new VoxelDefinition {
                    InternalName = "sand",
                    FriendlyName = "Sand",
                    Description = "Commonly found near water.\n" +
                                  "Can be smelted to form glass.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 0,
                    UsesTextureAtlas = true
                },
                
                // Gravel
                new VoxelDefinition {
                    InternalName = "gravel",
                    FriendlyName = "Gravel",
                    Description = "Found randomly throughout the world in small pockets or layers.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 1,
                    UsesTextureAtlas = true
                },
                
                // Cobblestone
                new VoxelDefinition {
                    InternalName = "cobblestone",
                    FriendlyName = "Cobblestone",
                    Description = "Collection of loose stones often obtained from mining stone.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 8,
                    UsesTextureAtlas = true
                },
                
                // Water
                new VoxelDefinition {
                    InternalName = "water",
                    FriendlyName = "Water",
                    Description = "Vital for life!",
                    VoxelType = VoxelType.Liquid,
                    RenderType = RenderType.Custom,
                    Hardness = 0,
                    Attenuation = 1
                },
                
                // Flowing Water
                new VoxelDefinition {
                    InternalName = "flowing-water",
                    FriendlyName = string.Empty,
                    Description = string.Empty,
                    VoxelType = VoxelType.Liquid,
                    RenderType = RenderType.Custom,
                    Hardness = 0,
                    Attenuation = 1
                },
                
                // Bedrock
                new VoxelDefinition {
                    InternalName = "bedrock",
                    FriendlyName = "Bedrock",
                    Description = "Prevents the world from falling.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 255,
                    UsesTextureAtlas = true
                },
                
                // Light Stone
                new VoxelDefinition {
                    InternalName = "light_stone",
                    FriendlyName = "Light Stone",
                    Description = "A strange material which seems to emit light on it's own.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 8,
                    UsesTextureAtlas = true,
                    LightValue = 8
                },
                
                // Light Stone Lamp
                new VoxelDefinition() {
                    InternalName = "light_stone_lamp",
                    FriendlyName = "Light Stone Lamp",
                    Description = "A lamp filled with refined light stone.\n" +
                                  "Emits a strong light on its own.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Opaque,
                    Hardness = 8,
                    UsesTextureAtlas = true,
                    LightValue = 15
                },
                
                // Glass
                new VoxelDefinition {
                    InternalName = "glass",
                    FriendlyName = "Glass",
                    Description = "Clear.",
                    VoxelType = VoxelType.Solid,
                    RenderType = RenderType.Alpha,
                    Hardness = 8,
                    UsesTextureAtlas = true,
                    Attenuation = 1
                }
            };
            
            // Add internal voxels to the list and generate ID mappings
            for (var i = 0; i < internalVoxels.Count; i++) {
                _idMapping.Add(internalVoxels[i].InternalName, (ushort)i);
                VoxelData.Add(internalVoxels[i]);
            }
        }
    }
}
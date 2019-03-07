using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Textures {
    /// <summary>
    /// Generates a texture atlas from individual texture per block.
    /// </summary>
    public class RuntimeTextureAtlas {
        // Textures per row
        private const int TEXTURES_PER_ROW = 32;
        
        // Texture Atlas
        public readonly Texture2D AlbedoAtlas;
        public readonly Texture2D EmissionAtlas;
        public readonly Texture2D MetallicAtlas;
        public readonly Texture2D SmoothnessAtlas;
        
        // Default / Error Textures
        private readonly Texture2D _defaultBlack;
        private readonly Texture2D _defaultMagenta;
        private readonly Texture2D _errorTexture;
        
        // Loaded Texture Cache
        private readonly Dictionary<string, Texture2D> _textureCache;
        
        // PBR Texture Lists
        private readonly List<Texture2D> _albedoTextures;
        private readonly List<Texture2D> _emissionTextures;
        private readonly List<Texture2D> _metallicTextures;
        private readonly List<Texture2D> _smoothnessTextures;
        
        // Texture UV Rects
        private Rect[] _albedoRects;
        private Rect[] _emissionRects;
        private Rect[] _metallicRects;
        private Rect[] _smoothnessRects;

        public RuntimeTextureAtlas(int textureSize = 16) {
            // Initialize atlas textures
            AlbedoAtlas = new Texture2D(textureSize * TEXTURES_PER_ROW, textureSize * TEXTURES_PER_ROW) { filterMode = FilterMode.Point };
            EmissionAtlas = new Texture2D(textureSize * TEXTURES_PER_ROW, textureSize * TEXTURES_PER_ROW) { filterMode = FilterMode.Point };
            MetallicAtlas = new Texture2D(textureSize * TEXTURES_PER_ROW, textureSize * TEXTURES_PER_ROW) { filterMode = FilterMode.Point };
            SmoothnessAtlas = new Texture2D(textureSize * TEXTURES_PER_ROW, textureSize * TEXTURES_PER_ROW) { filterMode = FilterMode.Point };
            
            // Initialize texture cache
            _textureCache = new Dictionary<string, Texture2D>();
            
            // Initialize texture lists
            _albedoTextures = new List<Texture2D>(64);
            _emissionTextures = new List<Texture2D>(64);
            _metallicTextures = new List<Texture2D>(64);
            _smoothnessTextures = new List<Texture2D>(64);
            
            // Load default / error textures
            _defaultBlack = Texture2D.blackTexture;
            _defaultMagenta = Resources.Load<Texture2D>("Textures/default_magenta");
            _errorTexture = Resources.Load<Texture2D>("Textures/error");
            
            GenerateAtlas();
        }
        
        /// <summary>
        /// Tries to fetch the texture from the cache before loading from disk.
        /// If both fail, returns null.
        /// </summary>
        private Texture2D TryLoadTexture(string name) {
            // Try to fetch the texture from the cache
            Texture2D texture;
            _textureCache.TryGetValue(name, out texture);
            
            // If the cache misses, try to load from disk and add to cache
            if (texture == null) {
                texture = Resources.Load<Texture2D>($"Textures/{name}");
                
                if (!_textureCache.ContainsKey(name))
                    _textureCache.Add(name, texture);
            }
            
            // Return the texture
            return texture;
        }
        
        /// <summary>
        /// Generates the texture atlases
        /// </summary>
        private void GenerateAtlas() {
            // Load textures for each voxel
            foreach (var voxelData in VoxelDictionary.VoxelData) {
                // Skip voxels which do not use the texture atlas
                if (!voxelData.UsesTextureAtlas) continue;

                // Initialize voxel texture index and rect arrays
                voxelData.AlbedoRects = new Rect[voxelData.PerSideTextures ? 6 : 1];
                voxelData.AlbedoIndicies = new int[voxelData.PerSideTextures ? 6 : 1];
                voxelData.EmissionRects = new Rect[voxelData.PerSideTextures ? 6 : 1];
                voxelData.EmissionIndicies = new int[voxelData.PerSideTextures ? 6 : 1];
                voxelData.MetallicRects = new Rect[voxelData.PerSideTextures ? 6 : 1];
                voxelData.MetallicIndicies = new int[voxelData.PerSideTextures ? 6 : 1];
                voxelData.SmoothnessRects = new Rect[voxelData.PerSideTextures ? 6 : 1];
                voxelData.SmoothnessIndicies = new int[voxelData.PerSideTextures ? 6 : 1];

                // Branch for multi-texture voxels
                if (voxelData.PerSideTextures) {
                    // Iterate over the voxel side texture names and try to load them
                    for (var i = 0; i < 6; i++) {
                        // Reference side texture name at current index
                        var sideName = voxelData.SideTextureNames[i];

                        // Declare textures
                        Texture2D texA, texE, texM, texS;
                        // Branch if side texture is a reference to another voxel texture
                        if (sideName.Contains("$")) {
                            texA = TryLoadTexture(sideName.Replace("$", "")) ?? _errorTexture;
                            texE = TryLoadTexture($"{sideName.Replace("$", "")}_e") ?? _defaultBlack;
                            texM = TryLoadTexture($"{sideName.Replace("$", "")}_m") ?? _defaultBlack;
                            texS = TryLoadTexture($"{sideName.Replace("$", "")}_s") ?? _defaultBlack;
                        }
                        else {
                            texA = TryLoadTexture($"{voxelData.InternalName}_{sideName}") ?? _errorTexture;
                            texE = TryLoadTexture($"{voxelData.InternalName}_{sideName}_e") ?? _defaultBlack;
                            texM = TryLoadTexture($"{voxelData.InternalName}_{sideName}_m") ?? _defaultBlack;
                            texS = TryLoadTexture($"{voxelData.InternalName}_{sideName}_s") ?? _defaultBlack;
                        }

                        // Add the textures to the list and update the index in the material data
                        _albedoTextures.Add(texA);
                        voxelData.AlbedoIndicies[i] = _albedoTextures.Count - 1;
                        _emissionTextures.Add(texE);
                        voxelData.EmissionIndicies[i] = _emissionTextures.Count - 1;
                        _metallicTextures.Add(texM);
                        voxelData.MetallicIndicies[i] = _metallicTextures.Count - 1;
                        _smoothnessTextures.Add(texS);
                        voxelData.SmoothnessIndicies[i] = _smoothnessTextures.Count - 1;
                    }
                }
                else {
                    // Declare texture name based on internal name or a given voxel reference
                    var textureName = voxelData.SideTextureNames != null ? voxelData.SideTextureNames[0] : voxelData.InternalName;

                    // Declare textures
                    Texture2D texA, texE, texM, texS;
                    // Branch if texture name is a reference to another voxel texture
                    if (textureName.Contains("$")) {
                        texA = TryLoadTexture(textureName.Replace("$", "")) ?? _errorTexture;
                        texE = TryLoadTexture($"{textureName.Replace("$", "")}_e") ?? _defaultBlack;
                        texM = TryLoadTexture($"{textureName.Replace("$", "")}_m") ?? _defaultBlack;
                        texS = TryLoadTexture($"{textureName.Replace("$", "")}_s") ?? _defaultBlack;
                    }
                    else {
                        texA = TryLoadTexture(textureName) ?? _errorTexture;
                        texE = TryLoadTexture($"{textureName}_e") ?? _defaultBlack;
                        texM = TryLoadTexture($"{textureName}_m") ?? _defaultBlack;
                        texS = TryLoadTexture($"{textureName}_s") ?? _defaultBlack;
                    }

                    // Add the textures to the list and update the index in the material data
                    _albedoTextures.Add(texA);
                    voxelData.AlbedoIndicies[0] = _albedoTextures.Count - 1;
                    _emissionTextures.Add(texE);
                    voxelData.EmissionIndicies[0] = _emissionTextures.Count - 1;
                    _metallicTextures.Add(texM);
                    voxelData.MetallicIndicies[0] = _metallicTextures.Count - 1;
                    _smoothnessTextures.Add(texS);
                    voxelData.SmoothnessIndicies[0] = _smoothnessTextures.Count - 1;
                }
            }
            
            // Pack loaded textures into the proper atlas
            _albedoRects = AlbedoAtlas.PackTextures(_albedoTextures.ToArray(), 4, AlbedoAtlas.width);
            _emissionRects = EmissionAtlas.PackTextures(_emissionTextures.ToArray(), 4, EmissionAtlas.width);
            _metallicRects = MetallicAtlas.PackTextures(_metallicTextures.ToArray(), 4, MetallicAtlas.width);
            _smoothnessRects = SmoothnessAtlas.PackTextures(_smoothnessTextures.ToArray(), 4, SmoothnessAtlas.width);
            
            // Set texture rects in the material data
            foreach (var materialData in VoxelDictionary.VoxelData) {
                // Skip voxel if it does not use the atlas
                if (!materialData.UsesTextureAtlas) continue;
                
                // Albedo rects
                for (var i = 0; i < materialData.AlbedoIndicies.Length; i++) {
                    materialData.AlbedoRects[i] = _albedoRects[materialData.AlbedoIndicies[i]];
                    materialData.EmissionRects[i] = _emissionRects[materialData.EmissionIndicies[i]];
                    materialData.MetallicRects[i] = _metallicRects[materialData.MetallicIndicies[i]];
                    materialData.SmoothnessRects[i] = _smoothnessRects[materialData.SmoothnessIndicies[i]];
                }
            }
            
            // Clear memory used by initial generation
            Resources.UnloadAsset(_errorTexture);
            
            // Unload individual textures from each list
            foreach (var texture in _albedoTextures) {
                if (texture == null) continue;
                Resources.UnloadAsset(texture);
            }
            foreach (var texture in _emissionTextures) {
                if (texture == null || texture == _defaultBlack) continue;
                Resources.UnloadAsset(texture);
            }
            foreach (var texture in _metallicTextures) {
                if (texture == null || texture == _defaultBlack) continue;
                Resources.UnloadAsset(texture);
            }
            foreach (var texture in _smoothnessTextures) {
                if (texture == null || texture == _defaultBlack) continue;
                Resources.UnloadAsset(texture);
            }
            _albedoTextures.Clear();
            _emissionTextures.Clear();
            _metallicTextures.Clear();
            _smoothnessTextures.Clear();
            _textureCache.Clear();
            
            // Unload unused assets
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}
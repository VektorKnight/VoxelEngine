using System;
using System.Collections.Concurrent;
using UnityEngine;
using VoxelEngine.Biomes;
using VoxelEngine.Chunks;
using VoxelEngine.Utility;

namespace VoxelEngine {
    public class VoxelWorld {
        // FastNoise object for generating noise
        private readonly IBiome[] _biomes;
        private readonly FastNoise _biomeMap;
        
        // Chunk map of loaded chunks
        private readonly ConcurrentDictionary<Vector2Int, Chunk> _chunkMap;
        
        // Constructor
        public VoxelWorld(int seed) {
            _biomes = new IBiome[3];
            _biomes[0] = new RollingHills(seed);
            _biomes[1] = new Ocean(seed);
            _biomes[2] = new Crag(seed);
            
            // Configure Biome Map
            _biomeMap = new FastNoise(seed);
            _biomeMap.SetNoiseType(FastNoise.NoiseType.PerlinFractal);
            _biomeMap.SetFrequency(0.01f);
            //_biomeMap.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.Natural);
            //_biomeMap.SetCellularReturnType(FastNoise.CellularReturnType.Distance);
            
            _chunkMap = new ConcurrentDictionary<Vector2Int, Chunk>();
        }
        
        /// <summary>
        /// Returns the raw noise value from the appropriate biome at the given coordinates.
        /// </summary>
        public float GetNoiseValue(int x, int y) {
            var biomeRaw = _biomeMap.GetNoise(x, y);
            var biomeValue = 0; //Mathf.FloorToInt(biomeRaw * (_biomes.Length - 1));

            if (biomeRaw < 0f) {
                biomeValue = 1;
            }
            else if (biomeRaw > 0f) {
                biomeValue = 0;
            }
            else {
                biomeValue = 2;
            }
            
            biomeValue = Mathf.Clamp(biomeValue, 0, 2);
            return _biomes[biomeValue].GetNoiseValue(x, y);
        }
        
        /// <summary>
        /// Generates and returns a height map for a given chunk at world x, y.
        /// </summary>
        public int[,] GetChunkHeightMap(int chunkX, int chunkY) {
            // Initialize the new height map
            var heightMap = new int[Chunk.CHUNK_LENGTH, Chunk.CHUNK_LENGTH];
            
            // Populate height map values
            PopulateHeightMapGauss(heightMap, chunkX, chunkY, 2);

            return heightMap;
        }
        
        /// <summary>
        /// Populates a height map using gaussian-filtered samples from source noise.
        /// </summary>
        private void PopulateHeightMapGauss(int[,] map, int chunkX, int chunkY, int blurSize) {
            for (var x = 0; x < Chunk.CHUNK_LENGTH; x++) {
                for (var y = 0; y < Chunk.CHUNK_LENGTH; y++) {
                    // Horizontal Gaussian
                    var h = GetNoiseValue(x + chunkX, y + chunkY)                * 0.208019f;
                    h += GetNoiseValue(x + chunkX + (blurSize * 2), y + chunkY)  * 0.192077f;
                    h += GetNoiseValue(x + chunkX + blurSize, y + chunkY)        * 0.203914f;
                    h += GetNoiseValue(x + chunkX + (blurSize * -1), y + chunkY) * 0.203914f;
                    h += GetNoiseValue(x + chunkX + (blurSize * -2), y + chunkY) * 0.192077f;
                    
                    // Vertical Gaussian
                    var v = GetNoiseValue(x + chunkX, y + chunkY)                * 0.208019f;
                    v += GetNoiseValue(x + chunkX, y + chunkY + (blurSize * 2))  * 0.192077f;
                    v += GetNoiseValue(x + chunkX, y + chunkY + blurSize)        * 0.203914f;
                    v += GetNoiseValue(x + chunkX, y + chunkY + (blurSize * -1)) * 0.203914f;
                    v += GetNoiseValue(x + chunkX, y + chunkY + (blurSize * -2)) * 0.192077f;

                    var final = (h + v) / 2f;
                    map[x, y] = Mathf.FloorToInt((final + 1.0f) / 2.0f * (Chunk.CHUNK_HEIGHT / 2f));
                }
            }
        }
        
        public IBiome GetBiomeAtPosition(int x, int y) {
            var biomeRaw = _biomeMap.GetNoise(x, y);

            var biomeValue = 0; //Mathf.FloorToInt(biomeRaw * (_biomes.Length - 1));

            if (biomeRaw < 0f) {
                biomeValue = 1;
            }
            else if (biomeRaw > 0f) {
                biomeValue = 0;
            }
            else {
                biomeValue = 2;
            }

            biomeValue = Mathf.Clamp(biomeValue, 0, 2);

            return _biomes[biomeValue];
        }
        
        /// <summary>
        /// Returns the chunk containing the given world position if possible.
        /// Returns null if the chunk is not loaded.
        /// Use GetOrLoadChunk if you want the chunk to be loaded automatically.
        /// </summary>
        public Chunk TryGetChunk(Vector3Int worldPos) {
            // Convert world coordinate to chunk coordinate
            var chunkPos = new Vector2Int(worldPos.x >> Chunk.CHUNK_LENGTH_LOG, worldPos.z >> Chunk.CHUNK_LENGTH_LOG);
            return _chunkMap.ContainsKey(chunkPos) ? _chunkMap[chunkPos] : null;
        }
        
        /// <summary>
        /// Determines whether or not the chunk at the specified position is loaded.
        /// </summary>
        public bool IsChunkLoaded(Vector2Int chunkPos) {
            return _chunkMap.ContainsKey(chunkPos);
        }
        
        /// <summary>
        /// Determines whether or not the chunk at the specified position is loaded.
        /// </summary>
        public bool IsChunkLoaded(Vector3Int worldPos) {
            // Convert world coordinate to chunk coordinate
            var chunkPos = new Vector2Int(worldPos.x >> Chunk.CHUNK_LENGTH_LOG, worldPos.z >> Chunk.CHUNK_LENGTH_LOG);
            return _chunkMap.ContainsKey(chunkPos);
        }
        
        /// <summary>
        /// Tries to return the chunk at the specified chunk position.
        /// </summary>
        public Chunk GetOrLoadChunk(Vector2Int chunkPos) {
            if (_chunkMap.ContainsKey(chunkPos)) {
                return _chunkMap[chunkPos];
            }
            
            var chunk = _chunkMap.GetOrAdd(chunkPos, new Chunk(this, chunkPos));
            if (!chunk.Initialized) chunk.Initialize(this);
            return chunk;
        }
        
        /// <summary>
        /// Tries to return the chunk at the specified chunk position.
        /// </summary>
        public Chunk GetOrLoadChunk(Vector3Int worldPos) {
            // Convert world coordinate to chunk coordinate
            var chunkPos = new Vector2Int(worldPos.x >> Chunk.CHUNK_LENGTH_LOG, worldPos.z >> Chunk.CHUNK_LENGTH_LOG);
            if (_chunkMap.ContainsKey(chunkPos)) {
                return _chunkMap[chunkPos];
            }
            
            var chunk = _chunkMap.GetOrAdd(chunkPos, new Chunk(this, chunkPos));
            if (!chunk.Initialized) chunk.Initialize(this);
            return chunk;
        }
        
        /// <summary>
        /// Tries to return the voxel at a given fixed-point world position.
        /// This will result in a chunk load if the voxel is contained within an unloaded chunk.
        /// </summary>
        public Voxel GetVoxel(Vector3Int voxelPos) {
            if (voxelPos.y >= Chunk.CHUNK_HEIGHT || voxelPos.y < 0) {
                return new Voxel(0);
            }
            
            // Calculate chunk position
            var chunkPosition = new Vector2Int(voxelPos.x >> Chunk.CHUNK_LENGTH_LOG, voxelPos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);

            lock (chunk) {
                // Determine the voxels local position using the chunk's transform matrix
                var voxelLocal = new Vector3Int(voxelPos.x - chunk.WorldPosition.x, voxelPos.y, voxelPos.z - chunk.WorldPosition.y);

                // Return the voxel
                return chunk.GetVoxel(voxelLocal);
            }
        }
        
        /// <summary>
        /// Tries to return the voxel at a given floating-point world position.
        /// This will result in a chunk load if the voxel is contained within an unloaded chunk.
        /// </summary>
        public Voxel GetVoxel(Vector3 worldPos) {
            // Round floating-point position to fixed position
            var voxelPosition = new Vector3Int(Mathf.RoundToInt(worldPos.x), 
                                               Mathf.RoundToInt(worldPos.y), 
                                               Mathf.RoundToInt(worldPos.z));
            
            // Return the voxel
            return GetVoxel(voxelPosition);
        }
        
        /// <summary>
        /// Sets the voxel at the given position.
        /// </summary>
        /// <param name="voxelPos"></param>
        /// <param name="voxel"></param>
        public void SetVoxel(Vector3Int voxelPos, Voxel voxel) {
            if (voxelPos.y >= Chunk.CHUNK_HEIGHT || voxelPos.y < 0) {
                return;
            }
            
            // Calculate chunk position
            var chunkPosition = new Vector2Int(voxelPos.x >> Chunk.CHUNK_LENGTH_LOG, voxelPos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);

            lock (chunk) {
                // Determine the voxels local position using the chunk's transform matrix
                var voxelLocal = new Vector3Int(voxelPos.x - chunk.WorldPosition.x, voxelPos.y, voxelPos.z - chunk.WorldPosition.y);

                // Return the voxel
                chunk.SetVoxel(new Vector3Int(voxelLocal.x, voxelLocal.y, voxelLocal.z), voxel);
            }
        }
        
        #region Lighting Functions
        /// <summary>
        /// Gets sunlight value for a particular voxel.
        /// </summary>
        public int GetSunlight(Vector3Int pos) {
            // Calculate chunk position
            var chunkPosition = new Vector2Int(pos.x >> Chunk.CHUNK_LENGTH_LOG, pos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);
            
            // Return the light value
            var localPos = new Vector3Int(pos.x - chunk.WorldPosition.x, pos.y, pos.z - chunk.WorldPosition.y);
            return chunk.GetSunlight(localPos);
        }
        
        /// <summary>
        /// Sets sunlight value for a particular voxel.
        /// </summary>
        public Chunk SetSunlight(Vector3Int pos, int val) {
            // Calculate chunk position
            var chunkPosition = new Vector2Int(pos.x >> Chunk.CHUNK_LENGTH_LOG, pos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);
            
            // Set the light value
            var localPos = new Vector3Int(pos.x - chunk.WorldPosition.x, pos.y, pos.z - chunk.WorldPosition.y);
            chunk.SetSunlight(localPos, val);
            return chunk;
        }
        
        /// <summary>
        /// Gets block light value for a particular voxel.
        /// </summary>
        public int GetBlockLight(Vector3Int pos) {
            // Calculate chunk position
            var chunkPosition = new Vector2Int(pos.x >> Chunk.CHUNK_LENGTH_LOG, pos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);
            
            // Return the light value
            var localPos = new Vector3Int(pos.x - chunk.WorldPosition.x, pos.y, pos.z - chunk.WorldPosition.y);
            return chunk.GetBlockLight(localPos);
        }
        
        /// <summary>
        /// Sets block light value for a particular voxel.
        /// </summary>
        public Chunk SetBlockLight(Vector3Int pos, int val, bool addNode = true) {
            // Calculate chunk position
            var chunkPosition = new Vector2Int(pos.x >> Chunk.CHUNK_LENGTH_LOG, pos.z >> Chunk.CHUNK_LENGTH_LOG);
            
            // Reference the chunk
            var chunk = GetOrLoadChunk(chunkPosition);
            
            // Set the light value
            var localPos = new Vector3Int(pos.x - chunk.WorldPosition.x, pos.y, pos.z - chunk.WorldPosition.y);
            chunk.SetBlockLight(localPos, val, addNode);
            return chunk;
        }
        #endregion
    }
}
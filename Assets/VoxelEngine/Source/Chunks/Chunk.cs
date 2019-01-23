using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Chunks {
    /// <summary>
    /// Represents a single chunk of voxels in the world.
    /// Chunk dimensions should be powers of 2 with the X,Z pair being equal.
    /// </summary>
    public class Chunk {
        // Chunk Dimension Constants
        public const int CHUNK_LENGTH = 16;     // Side-length of a chunk in voxels
        public const int CHUNK_HEIGHT = 256;    // Height of a chunk in voxels
        public const int SEA_LEVEL = 64;        // Height considered to be sea level
        
        // Log2(X) Dimensions (Bitwise Stuff)
        public const int CHUNK_LENGTH_LOG = 4;
        public const int CHUNK_HEIGHT_LOG = 8;

        // Transform
        private readonly Vector2Int _position;
        private Matrix4x4 _matrix;
        
        // World Data
        private readonly VoxelWorld _world;
        private int[,] _heightMap;
        private int[,] _biomeMap;
        private readonly Voxel[] _voxels;
        
        // Lighting Data
        private readonly byte[] _lightMap;
        public readonly Dictionary<Vector3Int, int> LightSources;
        public bool NeedsLightUpdate;
        
        // Flags
        private bool _initialized;
        private bool _isDirty;
        private bool _isVisible;
        
        // Organize this sh*t
        public Matrix4x4 Matrix => _matrix;
        public bool Initialized => _initialized;
        public Vector2Int Position => _position;
        public Vector2Int WorldPosition => _position * CHUNK_LENGTH;
        public static int VoxelCount => CHUNK_LENGTH * CHUNK_HEIGHT * CHUNK_LENGTH;
        
        // Chunk update event
        public delegate void ChunkUpdate();
        public event ChunkUpdate OnChunkUpdate;
        
        /// <summary>
        /// Creates a new chunk of voxels.
        /// </summary>
        public Chunk(VoxelWorld world, Vector2Int position) {
            // Set world reference
            _world = world;
            
            // Set position
            _position = position;
            
            _matrix.SetTRS(new Vector3(WorldPosition.x, 0f, WorldPosition.y), Quaternion.identity, Vector3.one);
            
            // Initialize height map data array
            //_heightMap = new float[CHUNK_LENGTH, CHUNK_LENGTH];
            _biomeMap = new int[CHUNK_LENGTH, CHUNK_LENGTH];
            
            // Initialize voxel data array
            _voxels = new Voxel[CHUNK_LENGTH * CHUNK_HEIGHT * CHUNK_LENGTH];
            
            // Initialize light map and BFS queues
            _lightMap = new byte[CHUNK_LENGTH * CHUNK_HEIGHT * CHUNK_LENGTH];
            LightSources = new Dictionary<Vector3Int, int>();
            
            for (int i = 0; i < _voxels.Length; i++) {
                _voxels[i] = new Voxel(0);
            }
        }
        
        /// <summary>
        /// Initializes the chunk building the voxel data based on heightmap data from the world generator.
        /// WARNING: Invoking this on a modified chunk will wipe out all changes to the voxel data.
        /// </summary>
        public void Initialize(VoxelWorld world) {
            // Initialize height map
            _heightMap = _world.GetChunkHeightMap(WorldPosition.x, WorldPosition.y);
            
            
            
            // Populate voxel data
            GenerationFirstPass();
            
            // Invoke a chunk update and set initialized flag
            _initialized = true;
        }
        
        /// <summary>
        /// Forces a chunk update.
        /// </summary>
        public void Update() {
            OnChunkUpdate?.Invoke();
        }
        
        /// <summary>
        /// Handles stone, sea-level water, and eventually caves.
        /// Maybe Grass, dirt, etc
        /// </summary>
        private void GenerationFirstPass() {
            for (var z = 0; z < CHUNK_LENGTH; z++) {
                for (var y = 0; y < CHUNK_HEIGHT; y++) {
                    for (var x = 0; x < CHUNK_LENGTH; x++) {
                        var seq = x + CHUNK_LENGTH * (y + CHUNK_HEIGHT * z);
                        // Fill according to height map
                        var height = _heightMap[x, z];

                        // Skip voxels above the height map and sea level
                        if (y > height && y > SEA_LEVEL) continue;
                        
                        
                        var voxel = _world.GetBiomeAtPosition(WorldPosition.x + x, WorldPosition.y + z).GetVoxelAtHeight(height, y);
                        _voxels[seq] = voxel;
                    }
                }
            }
        }

        public Vector3Int LocalToWorld(Vector3Int local) {
            return new Vector3Int(local.x + WorldPosition.x, local.y, local.z + WorldPosition.y);
        }

        public Vector3Int WorldToLocal(Vector3Int world) {
            return new Vector3Int(world.x - WorldPosition.x, world.y, world.z - WorldPosition.y);
        }
        
        /// <summary>
        /// Returns the voxel height at the specified coordinates.
        /// </summary>
        public int GetVoxelHeight(int x, int z) {
            return _heightMap[x, z];
        }
        
        /// <summary>
        /// Gets a voxel at the specified position.
        /// Will throw an exception if the position is out of range.
        /// </summary>
        /// <returns></returns>
        public Voxel GetVoxel(Vector3Int pos) {
            return _voxels[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)];
        }

        /// <summary>
        /// Sets a voxel at the specified position.
        /// Will throw an exception if the position is out of range.
        /// Chunk update event is invoked by default and should only be changed
        /// under special circumstances.
        /// </summary>
        /// <param name="pos">Local position of the voxel.</param>
        /// <param name="voxel">The voxel data to set.</param>
        /// <param name="update">Whether or not to invoke the chunk update event.</param>
        public void SetVoxel(Vector3Int pos, Voxel voxel, bool update = true) {
            // Disallow editing of bedrock voxels
            if (_voxels[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)].Id == MaterialDictionary.DataByName("bedrock").Id) return;
            
            // Set the voxel
            _voxels[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] = voxel;
            
            // Determine if this was a placement or removal (set to air)
            if (voxel.Id == 0) {
                // Update light sources
                lock (LightSources) {
                    if (LightSources.ContainsKey(pos)) {
                        LightSources.Remove(pos);
                    }
                }
            }
            
            // Update light sources if necessary
            var voxelData = MaterialDictionary.DataById(voxel.Id);
            if (voxelData.IsLightSource) {
                SetBlockLight(pos, voxelData.LightValue);
                lock (LightSources) {
                    if (!LightSources.ContainsKey(pos))
                        LightSources.Add(pos, voxelData.LightValue);
                    else
                        LightSources[pos] = voxelData.LightValue;
                }
            }
            
            // Update height map
            if (_heightMap[pos.x, pos.z] < pos.y) {
                _heightMap[pos.x, pos.z] = pos.y;
            }
            
            // Skip updating if specified
            if (!update) return;
            
            // Invoke this chunks update event
            Update();
                
            // Determine if an adjacent chunk should be updated
            var delta = new Vector2Int {
                x = pos.x == 0 ? -1 : pos.x == CHUNK_LENGTH - 1 ? 1 : 0,
                y = pos.z == 0 ? -1 : pos.z == CHUNK_LENGTH - 1 ? 1 : 0
            };
            
            // No need to update a neighbor if delta is zero
            if (delta.sqrMagnitude == 0) return;
            
            // Reference the neighbor chunk and invoke an update
            var neighbor = _world.GetOrLoadChunk(_position + delta);
            neighbor.Update();
        }

        #region Lightmap Functions
        /// <summary>
        /// Gets sunlight value for a particular voxel.
        /// </summary>
        public int GetSunlight(Vector3Int pos) {
            return (_lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] >> 4) & 0xF;
        }
        
        /// <summary>
        /// Sets sunlight value for a particular voxel.
        /// </summary>
        public void SetSunlight(Vector3Int pos, int val) {
            _lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)]
                = (byte) ((_lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] & 0xF) | (val << 4));
        }
        
        /// <summary>
        /// Gets block light value for a particular voxel.
        /// </summary>
        public int GetBlockLight(Vector3Int pos) {
            return _lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] & 0xF;
        }
        
        /// <summary>
        /// Sets block light value for a particular voxel.
        /// </summary>
        public void SetBlockLight(Vector3Int pos, int val, bool addNode = true) {
            // Set light value
            _lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)]
                = (byte) ((_lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] & 0xF0) | (byte) val);
        }
        
        /// <summary>
        /// Clears all block light data from the light map.
        /// </summary>
        public void ClearBlockLight() {
            for (var i = 0; i < _lightMap.Length; i++) {
                _lightMap[i] = (byte) ((_lightMap[i] & 0xF0) | 0);
            }
        }
        #endregion
        
    }
}
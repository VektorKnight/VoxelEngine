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
        
        public static readonly Vector2Int[] ChunkNeighbors = {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1), 
        };

        // World Data
        private readonly VoxelWorld _world;
        private readonly int[] _heightMap;
        private int[,] _biomeMap;
        private readonly Voxel[] _voxels;
        
        // Lighting Data
        private readonly byte[] _lightMap;
        public readonly Dictionary<Vector3Int, int> LightSources;
        
        // Voxel Edits
        private readonly ConcurrentQueue<VoxelUpdate> _voxelEdits;
        
        // Flags
        public bool PendingVoxelEdits => _voxelEdits.Count > 0;
        public bool IsMeshDirty;
        public volatile bool IsLightDirty;
        private bool _isVisible;
        
        // Organize this sh*t
        public bool Initialized { get; private set; }
        public Vector2Int Position { get; }
        public Vector2Int WorldPosition { get; }
        public int HeightMapMax { get; private set; }
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
            Position = position;
            WorldPosition = position * CHUNK_LENGTH;
            
            
            _biomeMap = new int[CHUNK_LENGTH, CHUNK_LENGTH];
            _heightMap = new int[CHUNK_LENGTH * CHUNK_LENGTH];
            
            // Initialize voxel data array
            _voxels = new Voxel[CHUNK_LENGTH * CHUNK_HEIGHT * CHUNK_LENGTH];
            
            // Initialize light map and BFS queues
            _lightMap = new byte[CHUNK_LENGTH * CHUNK_HEIGHT * CHUNK_LENGTH];
            LightSources = new Dictionary<Vector3Int, int>();
            
            // Initialize voxel edit queue
            _voxelEdits = new ConcurrentQueue<VoxelUpdate>();
            
            for (var i = 0; i < _voxels.Length; i++) {
                _voxels[i] = new Voxel(0);
            }
        }
        
        /// <summary>
        /// Initializes the chunk building the voxel data based on heightmap data from the world generator.
        /// WARNING: Invoking this on a modified chunk will wipe out all changes to the voxel data.
        /// </summary>
        public void Initialize() {
            // Initialize height map
            _world.PopulateChunkHeightMap(WorldPosition.x, WorldPosition.y, _heightMap);
            
            // Populate voxel data
            GenerationFirstPass();
            
            // Mark chunk as dirty and set initialized flag
            //OnChunkUpdate?.Invoke();
            IsMeshDirty = true;
            Initialized = true;
        }
        
        /// <summary>
        /// Forces a chunk update.
        /// </summary>
        public void Update() {
            lock (this) {
                // Process all queued voxel updates
                while (_voxelEdits.Count > 0) {
                    if (!_voxelEdits.TryDequeue(out var update)) continue;
                    SetVoxel(update.Position, update.Voxel);
                }

                // Invoke the chunk update event
                OnChunkUpdate?.Invoke();

                IsMeshDirty = false;
            }
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
                        var height = _heightMap[x + z * CHUNK_LENGTH];
                        HeightMapMax = Mathf.Max(HeightMapMax, height);

                        // Skip voxels above the height map and sea level
                        if (y > height && y > SEA_LEVEL) continue;
                        
                        var voxel = _world.GetBiomeAtPosition(WorldPosition.x + x, WorldPosition.y + z).GetVoxelAtHeight(height, y);
                        if (voxel.Id == VoxelDictionary.IdByName("water")) {
                            _heightMap[x + z * CHUNK_LENGTH] = Mathf.Max(height, y);
                        }
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
        public int GetVoxelHeight(Vector2Int pos) {
            return _heightMap[pos.x + pos.y * CHUNK_LENGTH];
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
        /// Queues a voxel edit to be applied on the next update.
        /// Sets the dirty flag on this chunk.
        /// </summary>
        public void QueueVoxelUpdate(VoxelUpdate voxelEdit) {
            _voxelEdits.Enqueue(voxelEdit);
            IsMeshDirty = true;
            
            // Mark neighbors as dirty
            for (var i = 0; i < ChunkNeighbors.Length; i++) {
                // Try to reference the neighboring chunk
                var neighbor = _world.TryGetChunk(Position + ChunkNeighbors[i]);
        
                // Invoke update event on neighbor if loaded
                if (neighbor == null) continue;
                neighbor.IsMeshDirty = true;
            }
        }
        
        public void QueueVoxelUpdate(in Vector3Int pos, in Voxel voxel) {
            QueueVoxelUpdate(new VoxelUpdate(pos, voxel));
        }

        /// <summary>
        /// Sets a voxel at the specified position.
        /// Will throw an exception if the position is out of range.
        /// Chunk update event is invoked by default and should only be changed
        /// under special circumstances.
        /// </summary>
        private void SetVoxel(in Vector3Int pos, in Voxel voxel) {
            // Disallow editing of bedrock voxels
            if (_voxels[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)].Id == VoxelDictionary.IdByName("bedrock")) return;
            
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
                
                // Update height map
                HeightMapMax = Mathf.Max(HeightMapMax, pos.y);
                if (_heightMap[pos.x + pos.z * CHUNK_LENGTH] == pos.y) {
                    _heightMap[pos.x + pos.z * CHUNK_LENGTH] = pos.y - 1;
                }
            }
            else {
                // Update height map
                HeightMapMax = Mathf.Max(HeightMapMax, pos.y);
                if (_heightMap[pos.x + pos.z * CHUNK_LENGTH] < pos.y) {
                    _heightMap[pos.x + pos.z * CHUNK_LENGTH] = pos.y;
                }
            }
            
            // Update light sources if necessary
            var voxelData = VoxelDictionary.VoxelData[voxel.Id];
            if (voxelData.LightValue > 0) {
                SetBlockLight(pos, voxelData.LightValue);
                lock (LightSources) {
                    if (!LightSources.ContainsKey(pos))
                        LightSources.Add(pos, voxelData.LightValue);
                    else
                        LightSources[pos] = voxelData.LightValue;
                }
            }
        }

        #region Lightmap Functions
        /// <summary>
        /// Gets sunlight value for a particular voxel.
        /// </summary>
        public int GetSunlight(in Vector3Int pos) {
            return (_lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] >> 4) & 0xF;
        }
        
        /// <summary>
        /// Sets sunlight value for a particular voxel.
        /// </summary>
        public void SetSunlight(in Vector3Int pos, int val) {
            _lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)]
                = (byte) ((_lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] & 0xF) | (val << 4));
        }
        
        /// <summary>
        /// Clears all block light data from the light map.
        /// </summary>
        public void ClearSunLight() {
            for (var i = 0; i < _lightMap.Length; i++) {
                _lightMap[i] = (byte) ((_lightMap[i] & 0xF) | (0 << 4));
            }
        }
        
        /// <summary>
        /// Gets block light value for a particular voxel.
        /// </summary>
        public int GetBlockLight(in Vector3Int pos) {
            return _lightMap[pos.x + CHUNK_LENGTH * (pos.y + CHUNK_HEIGHT * pos.z)] & 0xF;
        }
        
        /// <summary>
        /// Sets block light value for a particular voxel.
        /// </summary>
        public void SetBlockLight(in Vector3Int pos, int val) {
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
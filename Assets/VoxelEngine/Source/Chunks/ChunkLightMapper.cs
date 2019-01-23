using System.Collections.Concurrent;
using System.Collections.Generic;
using RTSEngine;
using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Chunks {
    /// <summary>
    /// Handles lighting calculations for chunks.
    /// </summary>
    public class ChunkLightMapper {
        private static readonly Vector3Int[] VoxelNeighbors = {
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 0), 
            new Vector3Int(0, 0, -1), 
            new Vector3Int(-1, 0, 0), 
            new Vector3Int(0, 1, 0), 
            new Vector3Int(0, -1, 0),
        };

        private static readonly Vector2Int[] ChunkNeighbors = {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1), 
        };
        
        // BFS Queues for Propagation
        private readonly Queue<LightNode> _blockAddQueue;
        
        // Temporary data buffers
        private readonly Dictionary<Vector3Int, int> _virtualLightMap;
        
        // Constructor
        public ChunkLightMapper() {
            // Initialize BFS queues
            _blockAddQueue = new Queue<LightNode>();
            
            // Initialize buffers
            _virtualLightMap = new Dictionary<Vector3Int, int>();
        }
        
        /// <summary>
        /// Updates the light map for a given chunk belonging to the specified world.
        /// </summary>
        public void UpdateBlockLightMap(VoxelWorld world, Chunk chunk) {
            // Clear light map buffer
            _virtualLightMap.Clear();
            
            // Clear existing block light data
            chunk.ClearBlockLight();
            
            // Fetch all block light sources in this chunk
            lock (chunk.LightSources) {
                foreach (var lightSource in chunk.LightSources) {
                    chunk.SetBlockLight(lightSource.Key, lightSource.Value);
                    _blockAddQueue.Enqueue(new LightNode(chunk.LocalToWorld(lightSource.Key), chunk));
                }
            }
            
            // Fetch light sources from neighboring 8 chunks
            foreach (var neighborPos in ChunkNeighbors) {
                // Skip neighbors which are not yet loaded
                if (!world.IsChunkLoaded(chunk.Position + neighborPos)) continue;
                
                var neighbor = world.GetOrLoadChunk(chunk.Position + neighborPos);
                lock (neighbor.LightSources) {
                    foreach (var lightSource in neighbor.LightSources) {
                        _virtualLightMap.Add(neighbor.LocalToWorld(lightSource.Key), lightSource.Value);
                        _blockAddQueue.Enqueue(new LightNode(neighbor.LocalToWorld(lightSource.Key), neighbor));
                        DebugSystem.Instance.LogDirect($"Added light source at {neighbor.LocalToWorld(lightSource.Key)} from chunk {neighbor.Position}");
                    }
                }
            }
            
            // Propagate light sources
            PropagateLightSources(world, chunk);
        }
        
        /*public void CalculateSunlight() {
            for (var z = 0; z < CHUNK_LENGTH; z++) {
                for (var y = CHUNK_HEIGHT - 1; y > 0; y--) {
                    for (var x = 0; x < CHUNK_LENGTH; x++) {
                        var seq = x + CHUNK_LENGTH * (y + CHUNK_HEIGHT * z);
                        
                        // Fill according to height map
                        var height = _heightMap[x, z];

                        // Skip voxels above the height map and sea level
                        if (y > Mathf.Max(height, SEA_LEVEL) + 1) continue;
                        if (y < height) continue;
                        
                        // Reference voxel data
                        var voxelData = MaterialDictionary.DataById(_voxels[seq].Id);

                        if (voxelData.IsTransparent) {
                            SetSunlight(new Vector3Int(x, y, z), 15 - voxelData.LightAttenuation);
                            SunlightQueue.Enqueue(new LightNode(new Vector3Int(x, y, z), this));
                        }
                    }
                }
            }
        }*/
        
        /// <summary>
        /// Propagates any light nodes in the addition queue.
        /// </summary>
        private void PropagateLightSources(VoxelWorld world, Chunk chunk) {   
            // Process all light nodes in the queue
            while (_blockAddQueue.Count > 0) {
                // Reference the node at the front of the queue
                var node = _blockAddQueue.Dequeue();
                
                // If the node is in a neighboring chunk, cache the data to the buffer
                int lightLevel;
                if (node.Chunk != chunk) {
                    // Update the buffer and fetch the light value
                    if (!_virtualLightMap.ContainsKey(node.Position)) {
                        lightLevel = 0;//world.GetBlockLight(node.Position);
                        _virtualLightMap.Add(node.Position, 0);
                    }
                    else {
                        lightLevel = _virtualLightMap[node.Position];
                    }
                }
                else {
                    // Fetch lighting data from the local chunk
                    lightLevel = chunk.GetBlockLight(chunk.WorldToLocal(node.Position));
                }
                
                // Check all 6 neighbors
                for (var i = 0; i < 6; i++) {
                    // Calculate neighbor position
                    var neighborPos = node.Position + VoxelNeighbors[i];
                    
                    // Try to reference the containing chunk and skip if unloaded
                    var neighborChunk = world.TryGetChunk(neighborPos);
                    if (neighborChunk == null) continue;
                
                    // Skip neighbor if the neighbor Y is out of bounds
                    if (neighborPos.y < 0 || neighborPos.y > Chunk.CHUNK_HEIGHT - 1) continue;
                    
                    // Determine if the neighbor is in a neighboring chunk
                    var neighborVoxel = neighborChunk.GetVoxel(neighborChunk.WorldToLocal(neighborPos));
                    var neighborLight = 0;
                    
                    // Handle neighbor in adjacent case
                    if (neighborChunk != chunk) {
                        if (!_virtualLightMap.ContainsKey(neighborPos)) {
                            _virtualLightMap.Add(neighborPos, 0);
                        }
                        else {
                            neighborLight = _virtualLightMap[neighborPos];
                        }
                        
                        // Avoid propagating light into opaque blocks
                        if (neighborVoxel.IsTransparent && neighborLight + 2 <= lightLevel) {

                            // Set new light level in buffer
                            if (!_virtualLightMap.ContainsKey(neighborPos)) {
                                _virtualLightMap.Add(neighborPos, 0);
                            }
                            else {
                                _virtualLightMap[neighborPos] = lightLevel - 1;
                            }

                            // Queue up a new light node
                            _blockAddQueue.Enqueue(new LightNode(neighborPos, neighborChunk));
                        }
                    }
                    
                    // Handle neighbor local case
                    else {
                        neighborLight = chunk.GetBlockLight(chunk.WorldToLocal(neighborPos));
                        
                        // Avoid propagating light into opaque blocks
                        if (neighborVoxel.IsTransparent && neighborLight + 2 <= lightLevel) {

                            // Set new light level
                            chunk.SetBlockLight(chunk.WorldToLocal(neighborPos), lightLevel - 1);

                            // Queue up a new light node
                            _blockAddQueue.Enqueue(new LightNode(neighborPos, chunk));
                        }
                    }
                }
            }
        }
    }
}
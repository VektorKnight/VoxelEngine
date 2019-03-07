using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Threading;
using VoxelEngine.Voxels;

namespace VoxelEngine.Chunks.LightMapping {
    /// <summary>
    /// Handles lighting calculations for chunks.
    /// </summary>
    public class ChunkLightMapper {
        // Light Map Buffer Constants
        private const int LIGHT_BUFFER_LENGTH = 48;
        private const int LIGHT_BUFFER_HEIGHT = 256;
        
        // Voxel Neighbor Positions
        private static readonly Vector3Int[] VoxelNeighbors = {
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 0), 
            new Vector3Int(0, 0, -1), 
            new Vector3Int(-1, 0, 0), 
            new Vector3Int(0, 1, 0), 
            new Vector3Int(0, -1, 0),
        };
        
        // Height Map Neighbors
        private static readonly Vector2Int[] HeightMapNeighbors = {
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1), 
        };

        private Vector2Int _mapWorldOrigin;
        private Vector2Int _mapChunkOrigin;
        
        // BFS Queues for Propagation
        private readonly Queue<LightNode> _blockAddQueue;
        private readonly Queue<LightNode> _sunAddQueue;
        
        // Temporary Data
        private readonly byte[] _lightMapBuffer;
        private readonly Chunk[] _chunkBuffer;
        
        // Constructor
        public ChunkLightMapper() {
            // Initialize BFS queues
            _blockAddQueue = new Queue<LightNode>();
            _sunAddQueue = new Queue<LightNode>();
            _chunkBuffer = new Chunk[9];
            
            // Initialize light map buffer
            _lightMapBuffer = new byte[LIGHT_BUFFER_LENGTH * LIGHT_BUFFER_HEIGHT * LIGHT_BUFFER_LENGTH];
        }
        
        /// <summary>
        /// Updates the light map for a given chunk belonging to the specified world.
        /// </summary>
        public void UpdateBlockLightMap(VoxelWorld world, Chunk chunk) {
            // Calculate light map buffer world origin
            _mapWorldOrigin = new Vector2Int(chunk.WorldPosition.x - Chunk.CHUNK_LENGTH, chunk.WorldPosition.y - Chunk.CHUNK_LENGTH);
            _mapChunkOrigin = new Vector2Int(_mapWorldOrigin.x >> Chunk.CHUNK_LENGTH_LOG, _mapWorldOrigin.y >> Chunk.CHUNK_LENGTH_LOG);
            
            // Add this chunk to the chunk reference buffer
            _chunkBuffer[1 + 3] = chunk;
            
            // Fetch all block light sources in this chunk
            foreach (var lightSource in chunk.LightSources) {
                SetBlockLight(WorldToMapLocal(chunk.LocalToWorld(lightSource.Key)), lightSource.Value);
                _blockAddQueue.Enqueue(new LightNode(WorldToMapLocal(chunk.LocalToWorld(lightSource.Key)), chunk));
            }
            
            // Fetch light sources from neighboring 8 chunks
            foreach (var neighborPos in Chunk.ChunkNeighbors) {
                // Skip neighbors which are not yet loaded
                if (!world.IsChunkLoaded(chunk.Position + neighborPos)) continue;
                
                // Reference the neighbor
                var neighbor = world.TryGetChunk(chunk.Position + neighborPos);
                
                // Add neighbor to chunk reference buffer
                var bufferPos = new Vector2Int(neighbor.Position.x - _mapChunkOrigin.x, neighbor.Position.y - _mapChunkOrigin.y);
                _chunkBuffer[bufferPos.x + bufferPos.y * 3] = neighbor;
                
                // Fetch light sources if the neighbor is loaded
                foreach (var lightSource in neighbor.LightSources) {
                    SetBlockLight(WorldToMapLocal(neighbor.LocalToWorld(lightSource.Key)), lightSource.Value);
                    _blockAddQueue.Enqueue(new LightNode(WorldToMapLocal(neighbor.LocalToWorld(lightSource.Key)), neighbor));
                }
            }
            
            // Nothing to do if there are no light sources
            if (_blockAddQueue.Count == 0) {
                chunk.ClearBlockLight();
                return;
            }
            
            // Propagate light nodes
            PropagateBlockLightNodes(chunk);

            // Write light map data to the chunk
            lock (chunk) {
                for (var x = 16; x < 32; x++) {
                    for (var y = 0; y < 256; y++) {
                        for (var z = 16; z < 32; z++) {
                            chunk.SetBlockLight(new Vector3Int(x - 16, y, z - 16), GetBlockLight(new Vector3Int(x, y, z)));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates the light map for a given chunk belonging to the specified world.
        /// </summary>
        public void UpdateSunLightMap(VoxelWorld world, Chunk chunk) {
            // Calculate light map buffer world origin
            _mapWorldOrigin = new Vector2Int(chunk.WorldPosition.x - Chunk.CHUNK_LENGTH, chunk.WorldPosition.y - Chunk.CHUNK_LENGTH);
            _mapChunkOrigin = new Vector2Int(_mapWorldOrigin.x >> Chunk.CHUNK_LENGTH_LOG, _mapWorldOrigin.y >> Chunk.CHUNK_LENGTH_LOG);
            
            // Add this chunk to the chunk reference buffer
            _chunkBuffer[1 + 3] = chunk;
            
            // Fetch neighboring chunks which are loaded and add them to the buffer
            foreach (var neighborPos in Chunk.ChunkNeighbors) {
                // Skip neighbors which are not yet loaded
                if (!world.IsChunkLoaded(chunk.Position + neighborPos)) continue;

                // Reference the neighbor
                var neighbor = world.TryGetChunk(chunk.Position + neighborPos);

                // Add neighbor to chunk reference buffer
                var bufferPos = new Vector2Int(neighbor.Position.x - _mapChunkOrigin.x, neighbor.Position.y - _mapChunkOrigin.y);
                _chunkBuffer[bufferPos.x + bufferPos.y * 3] = neighbor;
            }
            
            // Populate nodes along the surface by the maximum of 4 adjacent neighbors
            for (var lx = 0; lx < 48; lx++) {
                for (var lz = 0; lz < 48; lz++) {
                    // Determine the chunk we are iterating over
                    var currentChunk = _chunkBuffer[(lx >> Chunk.CHUNK_LENGTH_LOG) + (lz >> Chunk.CHUNK_LENGTH_LOG) * 3];
                    
                    // Skip if the chunk is null (not loaded)
                    if (currentChunk == null) continue;
                    
                    // Get height of the current position
                    var currentHeight = currentChunk.GetVoxelHeight(new Vector2Int(lx, lz) + _mapWorldOrigin - currentChunk.WorldPosition);
                    
                    // Get voxel type at current position
                    var voxel = currentChunk.GetVoxel(new Vector3Int(lx + _mapWorldOrigin.x - currentChunk.WorldPosition.x, currentHeight, lz + _mapWorldOrigin.y - currentChunk.WorldPosition.y));
                    if (voxel.Id == 7) {
                        // Calculate node position and enqueue a node
                        var nodePos = new Vector3Int(lx, currentHeight + 1, lz);
                        SetSunlight(nodePos, 15);
                        _sunAddQueue.Enqueue(new LightNode(nodePos, null));
                        continue;
                    }
                    
                    // Check 4 height map neighbors
                    var hMax = currentHeight;
                    for (var i = 0; i < 4; i++) {
                        // Calculate neighbor position
                        var nP = new Vector2Int(lx, lz) + HeightMapNeighbors[i];
                        
                        // If the neighbor is out of bounds, skip it
                        if (nP.x < 0 || nP.x > LIGHT_BUFFER_LENGTH - 1 || nP.y < 0 || nP.y > LIGHT_BUFFER_LENGTH - 1) continue;
                        
                        // Try to sample the height map at the given position
                        var neighborChunk = _chunkBuffer[(nP.x >> Chunk.CHUNK_LENGTH_LOG) + (nP.y >> Chunk.CHUNK_LENGTH_LOG) * 3];
                        
                        // If the neighbor lies in a null chunk, skip it
                        if (neighborChunk == null) continue;
                        
                        // Update the height max value
                        hMax = Mathf.Max(hMax, neighborChunk.GetVoxelHeight(_mapWorldOrigin + nP - neighborChunk.WorldPosition));
                    }
                    
                    // No node needed if hMax = current height or zero
                    
                    if (hMax == currentHeight || hMax == 0) {
                        var nodePos = new Vector3Int(lx, hMax + 1, lz);
                        SetSunlight(nodePos, 15);
                    }
                    else {
                        // Calculate node position and enqueue a node
                        var nodePos = new Vector3Int(lx, hMax + 1, lz);
                        SetSunlight(nodePos, 15);
                        _sunAddQueue.Enqueue(new LightNode(nodePos, null));
                    }
                }
            }
            
            // Propagate light nodes
            PropagateSunlightNodes(chunk);

            // Write light map data to the chunk
            lock (chunk) {
                for (var x = 16; x < 32; x++) {
                    for (var y = 0; y < 256; y++) {
                        for (var z = 16; z < 32; z++) {
                            chunk.SetSunlight(new Vector3Int(x - 16, y, z - 16), GetSunlight(new Vector3Int(x, y, z)));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Propagates any light nodes in the addition queue.
        /// </summary>
        private void PropagateBlockLightNodes(Chunk chunk) {   
            // Process all light nodes in the queue
            while (_blockAddQueue.Count > 0) {            
                // Reference the node at the front of the queue
                var node = _blockAddQueue.Dequeue();
                
                // If the node is in a neighboring chunk, cache the data to the buffer
                var lightLevel = GetBlockLight(node.Position);
                
                // Check all 6 neighbors
                for (var i = 0; i < 6; i++) {
                    // Calculate neighbor position
                    var neighborPos = node.Position + VoxelNeighbors[i];
                    
                    // Skip out of bounds neighbors
                    if (neighborPos.x < 0 || neighborPos.x > LIGHT_BUFFER_LENGTH - 1 || 
                        neighborPos.y < 0 || neighborPos.y > LIGHT_BUFFER_HEIGHT - 1 || 
                        neighborPos.z < 0 || neighborPos.z > LIGHT_BUFFER_LENGTH - 1) continue;
                    
                    // Try to reference the containing chunk and skip if unloaded
                    var neighborChunk = _chunkBuffer[(neighborPos.x >> Chunk.CHUNK_LENGTH_LOG) + (neighborPos.z >> Chunk.CHUNK_LENGTH_LOG) * 3];
                    if (neighborChunk == null) continue;
                    
                    // Calculate voxel position relative to the neighbor chunk
                    var neighborLocalPos = new Vector3Int(neighborPos.x + _mapWorldOrigin.x - neighborChunk.WorldPosition.x,
                        neighborPos.y,
                        neighborPos.z + _mapWorldOrigin.y - neighborChunk.WorldPosition.y);
                    
                    // Fetch voxel data
                    var neighborVoxel = neighborChunk.GetVoxel(neighborLocalPos);
                    
                    // Fetch light level for the current neighbor
                    var neighborLight = GetBlockLight(neighborPos);

                    // Avoid propagating light into opaque blocks
                    if (neighborVoxel.RenderType != RenderType.Opaque && neighborLight + 2 <= lightLevel) {
                        // Set new light level
                        SetBlockLight(neighborPos, lightLevel - 1 - neighborVoxel.Attenuation);

                        // Queue up a new light node
                        _blockAddQueue.Enqueue(new LightNode(neighborPos, chunk));
                    }
                }
            }
        }
        
        /// <summary>
        /// Propagates any light nodes in the addition queue.
        /// </summary>
        private void PropagateSunlightNodes(Chunk chunk) {   
            // Process all light nodes in the queue
            while (_sunAddQueue.Count > 0) {
                // Reference the node at the front of the queue
                var node = _sunAddQueue.Dequeue();
                
                // If the node is in a neighboring chunk, cache the data to the buffer
                var lightLevel = GetSunlight(node.Position);
                
                // Check all 6 neighbors
                for (var i = 0; i < 6; i++) {
                    // Calculate neighbor position
                    var neighborPos = node.Position + VoxelNeighbors[i];
                    
                    // Skip out of bounds neighbors
                    if (neighborPos.x < 0 || neighborPos.x > LIGHT_BUFFER_LENGTH - 1 || 
                        neighborPos.y < 0 || neighborPos.y > LIGHT_BUFFER_HEIGHT - 1 || 
                        neighborPos.z < 0 || neighborPos.z > LIGHT_BUFFER_LENGTH - 1) continue;
                    
                    // Fetch neighbor light value
                    var neighborLight = GetSunlight(neighborPos);
                    if (neighborLight == 15) continue;
                    
                    // Try to reference the containing chunk and skip if unloaded
                    var neighborChunk = _chunkBuffer[(neighborPos.x >> Chunk.CHUNK_LENGTH_LOG) + (neighborPos.z >> Chunk.CHUNK_LENGTH_LOG) * 3];
                    if (neighborChunk == null) continue;
                    
                    // Calculate voxel position relative to the neighbor chunk
                    var neighborLocalPos = new Vector3Int(neighborPos.x + _mapWorldOrigin.x - neighborChunk.WorldPosition.x,
                                                          neighborPos.y,
                                                          neighborPos.z + _mapWorldOrigin.y - neighborChunk.WorldPosition.y);
                    
                    // Fetch voxel data
                    var neighborVoxel = neighborChunk.GetVoxel(neighborLocalPos);
                    
                    // Avoid propagating light into opaque blocks
                    if (neighborVoxel.RenderType != RenderType.Opaque && neighborLight + 2 <= lightLevel) {
                        // Sunlight suffers no attenuation when propagating downward
                        if (neighborPos.y < node.Position.y) {
                            SetSunlight(neighborPos, lightLevel - neighborVoxel.Attenuation);
                        }
                        else {
                            // Set new light level
                            SetSunlight(neighborPos, lightLevel - 1 - neighborVoxel.Attenuation);
                        }

                        // Queue up a new light node
                        _sunAddQueue.Enqueue(new LightNode(neighborPos, chunk));
                    }
                }
            }
        }
        
        #region Lightmap Functions
        private Vector3Int WorldToMapLocal(Vector3Int world) {
            return new Vector3Int(world.x - _mapWorldOrigin.x, world.y, world.z - _mapWorldOrigin.y);
        }

        /// <summary>
        /// Gets sunlight value for a particular voxel.
        /// </summary>
        public int GetSunlight(Vector3Int pos) {
            return (_lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)] >> 4) & 0xF;
        }
        
        /// <summary>
        /// Sets sunlight value for a particular voxel.
        /// </summary>
        public void SetSunlight(Vector3Int pos, int val) {
            _lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)]
                = (byte) ((_lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)] & 0xF) | (val << 4));
        }
        
        /// <summary>
        /// Gets block light value for a particular voxel.
        /// </summary>
        private int GetBlockLight(Vector3Int pos) {
            return _lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)] & 0xF;
        }
        
        /// <summary>
        /// Sets block light value for a particular voxel.
        /// </summary>
        private void SetBlockLight(Vector3Int pos, int val, bool addNode = true) {
            // Set light value
            _lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)]
                = (byte) ((_lightMapBuffer[pos.x + LIGHT_BUFFER_LENGTH * (pos.y + LIGHT_BUFFER_HEIGHT * pos.z)] & 0xF0) | (byte) val);
        }
        
        /// <summary>
        /// Clears all block light data from the light map.
        /// </summary>
        public void ClearBuffers() {
            for (var i = 0; i < _lightMapBuffer.Length; i++) {
                _lightMapBuffer[i] = 0;
            }
            
            for (int i = 0; i < _chunkBuffer.Length; i++) {
                _chunkBuffer[i] = null;
            }
        }
        #endregion
    }
}
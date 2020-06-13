using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RTSEngine;
using UnityEngine;
using VoxelEngine.Chunks.LightMapping;
using VoxelEngine.Voxels;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Chunks.MeshGeneration {
    /// <summary>
    /// Generates a cubic mesh from a given chunk using naive meshing.
    /// Greedy meshing should be implemented eventually.
    /// </summary>
    public class ChunkUpdateWorker {
        // Eight vertices of a cube
        private static readonly Vector3[] Vertices = {
            new Vector3(1, 1, 1), 
            new Vector3(-1, 1, 1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, -1, -1),
        };
        
        // Triangles of a cube
        private static readonly int[] Triangles = {
            0, 1, 2, 
            3, 5, 0, 
            3, 6, 4, 
            5, 6, 7,
            1, 4, 7, 
            2, 5, 4, 
            1, 0, 3, 
            2, 7, 6
        };
        
        // Offsets of neighboring voxels
        private static readonly Vector3Int[] Neighbors = {
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 0), 
            new Vector3Int(0, 0, -1), 
            new Vector3Int(-1, 0, 0), 
            new Vector3Int(0, 1, 0), 
            new Vector3Int(0, -1, 0),
        };
        
        // Default voxel (air)
        private static readonly Voxel DefaultVoxel = new Voxel(0);
        
        // Vertex Data
        private readonly List<Vector3> _vertices = new List<Vector3>(16384);
        
        // Triangle Data
        private readonly List<int>[] _triangles = {
            new List<int>(8192),    // Opaque
            new List<int>(8192),    // Cutout
            new List<int>(8192),    // Alpha
            new List<int>(8192),    // Custom
        };
        
        // UV Data
        private readonly List<Vector2> _uv0 = new List<Vector2>(16384);
        private readonly List<Vector2> _uv1 = new List<Vector2>(16384);
        private readonly List<Vector2> _uv2 = new List<Vector2>(16384);
        private readonly List<Vector2> _uv3 = new List<Vector2>(16384);
        private readonly List<Color> _vertexColors = new List<Color>(16384);
        private readonly List<int> _rowsToUpdate = new List<int>(256);
        
        // Chunk Buffer & Positional Data
        private readonly Chunk[] _chunkBuffer = new Chunk[9];
        private Vector2Int _mapWorldOrigin;
        private Vector2Int _mapChunkOrigin;
        
        // Job/Result Queues
        private readonly BlockingCollection<ChunkUpdateJob> _updateJobs;
        private readonly IProducerConsumerCollection<ChunkMeshResult> _meshResults;
        
        // Worker Thread
        private Thread _workerThread;
        private ManualResetEvent _threadControl;
        private bool _terminateFlag = false;
        
        // Chunk Light Mapper
        private readonly ChunkLightMapper _lightMapper;
        
        // Debug
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lightMapTime, _chunkMeshTime;
        
        // Best / Worst Times
        public long LightTimeBest { get; private set; }
        public long LightTimeWorst { get; private set; }
        public long MeshTimeBest { get; private set; }
        public long MeshTimeWorst { get; private set; }
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Creates a new simple cubes mesh extraction worker.
        /// </summary>
        /// <param name="jobQueue">The job queue to pull jobs from.</param>
        /// <param name="meshResults">The result queue to push results to.</param>
        public ChunkUpdateWorker(BlockingCollection<ChunkUpdateJob> jobQueue, IProducerConsumerCollection<ChunkMeshResult> meshResults) {
            _updateJobs = jobQueue;
            _meshResults = meshResults;
            
            // Initialize job/result queues
            _updateJobs = jobQueue;
            _meshResults = meshResults;
            
            // Initialize light mapper
            _lightMapper = new ChunkLightMapper();
            
            // Initialize worker thread
            var workerStart = new ThreadStart(WorkLoop);
            _workerThread = new Thread(workerStart) { IsBackground = true };
            _workerThread.Start();
        }
        
        /// <summary>
        /// Worker thread loop.
        /// </summary>
        private void WorkLoop() {
            foreach (var updateJob in _updateJobs.GetConsumingEnumerable()) {
                IsBusy = true;
                try {
                    _stopwatch.Start();
                    // Populate the chunk buffer
                    PopulateChunkBuffer(updateJob.World, updateJob.Chunk);
                    
                    // Generate mesh data for solid voxels
                    GenerateMeshData(updateJob.Chunk, VoxelType.Solid);
                    
                    // Store generated data and clear work buffers
                    var solid = new MeshData (
                        _vertices.ToArray(),
                        new [] {_triangles[0].ToArray(), _triangles[1].ToArray(), _triangles[2].ToArray(), _triangles[3].ToArray()},
                        _uv0.ToArray(),
                        _uv1.ToArray(),
                        _uv2.ToArray(),
                        _uv3.ToArray(),
                        _vertexColors.ToArray()
                    );
                    ClearWorkBuffers();
                    
                    // Generate mesh data for non-solid voxels
                    GenerateMeshData(updateJob.Chunk, VoxelType.Liquid);
                    
                    // Store generated data and clear work buffers
                    var nonSolid = new MeshData (
                        _vertices.ToArray(),
                        new [] {_triangles[0].ToArray(), _triangles[1].ToArray(), _triangles[2].ToArray(), _triangles[3].ToArray()},
                        _uv0.ToArray(),
                        _uv1.ToArray(),
                        _uv2.ToArray(),
                        _uv3.ToArray(),
                        _vertexColors.ToArray()
                    );
                    
                    ClearWorkBuffers();
                    
                    // Compile results and enqueue to main
                    var result = new ChunkMeshResult (
                        updateJob.Id,
                        solid,
                        nonSolid,
                        updateJob.MeshCallback
                    );
                    
                    // Clear chunk buffer
                    ClearChunkBuffer();
                    
                    // Record mesh timing stats
                    _stopwatch.Stop();
                    _chunkMeshTime = _stopwatch.ElapsedMilliseconds;
                    _stopwatch.Reset();
                    _meshResults.TryAdd(result);
                    
                    // Update lighting for the current chunk
                    _stopwatch.Start();
                    _lightMapper.UpdateBlockLightMap(updateJob.World, updateJob.Chunk);
                    _lightMapper.UpdateSunLightMap(updateJob.World, updateJob.Chunk);
                    _lightMapper.ClearBuffers();
                    
                    // Record light timing stats
                    _stopwatch.Stop();
                    _lightMapTime = _stopwatch.ElapsedMilliseconds;
                    _stopwatch.Reset();
                    
                    // Flag chunk light as dirty once a light job completes
                    updateJob.Chunk.IsLightDirty = true;
                    
                    // Log statistics to console
                    DebugSystem.Instance.LogDirect($"Chunk Worker: Update task for chunk {updateJob.Chunk.Position} complete! " +
                                                   $"\nMesh: {_chunkMeshTime}ms ({_rowsToUpdate.Count} Rows) Lighting: {_lightMapTime}ms");
                    _stopwatch.Reset();
                    
                    // Update best/worst times
                    LightTimeBest = Math.Min(LightTimeBest, _lightMapTime);
                    LightTimeWorst = Math.Max(LightTimeWorst, _lightMapTime);
                    MeshTimeBest = Math.Min(MeshTimeBest, _chunkMeshTime);
                    MeshTimeWorst = Math.Max(MeshTimeWorst, _chunkMeshTime);
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Populates chunk buffer with the current chunk and any loaded neighbors.
        /// </summary>
        private void PopulateChunkBuffer(VoxelWorld world, Chunk chunk) {
            // Calculate chunk buffer world origin
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
        }

        private void ClearChunkBuffer() {
            for (var i = 0; i < _chunkBuffer.Length; i++) {
                _chunkBuffer[i] = null;
            }
        }
        
        /// <summary>
        /// Generates a cube mesh from a given chunk and world.
        /// </summary>
        /// <returns></returns>
        private void GenerateMeshData(Chunk chunk, VoxelType voxelType) {   
            // Process each row in the chunk and determine if the row needs to be considered in mesh generation
            for (var ry = 0; ry < Mathf.Max(chunk.HeightMapMax, Chunk.SEA_LEVEL) + 1; ry++) {
                for (var rx = -1; rx < Chunk.CHUNK_LENGTH + 1; rx++) {
                    for (var rz = -1; rz < Chunk.CHUNK_LENGTH + 1; rz++) {
                        // Declare addRow flag
                        bool addRow;
                        
                        // We can skip checks above or below if the voxel is in an adjacent chunk and is transparent              
                        var voxelInAdjacent = rx < 0 || rx > Chunk.CHUNK_LENGTH - 1 || rz < 0 || rz > Chunk.CHUNK_LENGTH - 1;
                        if (voxelInAdjacent) {
                            var bufferPos = WorldToMapLocal(chunk.LocalToWorld(new Vector3Int(rx, ry, rz)));
                            var neighborChunk = _chunkBuffer[(bufferPos.x >> Chunk.CHUNK_LENGTH_LOG) + (bufferPos.z >> Chunk.CHUNK_LENGTH_LOG) * 3];
                            
                            // Branch based on chunk existence
                            if (neighborChunk != null) {
                                var voxel = neighborChunk.GetVoxel(neighborChunk.WorldToLocal(MapLocalToWorld(bufferPos)));
                                
                                // We need to process the row if the row contains any non-opaque voxels
                                addRow = voxel.RenderType != RenderType.Opaque;
                            }
                            else {
                                addRow = true;
                            }
                        }
                        else {
                            // Fetch voxel 
                            var voxel = chunk.GetVoxel(new Vector3Int(rx, ry, rz));
                            
                            // Add the row to the list if we find any transparent voxels
                            var transparentAbove = ry >= Chunk.CHUNK_HEIGHT || chunk.GetVoxel(new Vector3Int(rx, ry + 1, rz)).RenderType != RenderType.Opaque;
                            var transparentBelow = ry - 1 < 0 || chunk.GetVoxel(new Vector3Int(rx, ry - 1, rz)).RenderType != RenderType.Opaque;
                            addRow = transparentAbove || transparentBelow || voxel.RenderType != RenderType.Opaque;
                        }
                        
                        // Skip if we don't have a reason to add the row yet
                        if (!addRow) continue;
                        
                        // Add the row to the list and skip to the next row
                        _rowsToUpdate.Add(ry);
                        rx = Chunk.CHUNK_LENGTH;
                        rz = Chunk.CHUNK_LENGTH;
                    }
                }
            }
            
            // Process relevant rows
            for (var y = 0; y < _rowsToUpdate.Count; y++) {
                for (var x = 0; x < Chunk.CHUNK_LENGTH; x++) {
                    for (var z = 0; z < Chunk.CHUNK_LENGTH; z++) {
                        var voxelPos = new Vector3Int(x, _rowsToUpdate[y], z);
                        ProcessVoxel(chunk, voxelPos, voxelType);
                    }
                }
            }
        }
       
        /// <summary>
        /// Adds relevant faces for a single voxel.
        /// </summary>
        private void ProcessVoxel(Chunk chunk, in Vector3Int voxelPos, VoxelType voxelType) {
            // Reference the current voxel
            var voxel = chunk.GetVoxel(voxelPos);
            
            // Skip if the voxel is not of the current type
            if (voxel.VoxelType != voxelType) return;
            
            // Skip if the current voxel is empty (air)
            var voxelData = VoxelDictionary.VoxelData[voxel.Id];
            if (voxel.Id == 0) return;
            
            // Populate vertices for each face
            for (var i = 0; i < 6; i++) {
                // Calculate neighbor position
                var neighborPos = voxelPos + Neighbors[i];
                
                // Skip face if the neighbor Y is out of bounds
                if (neighborPos.y < 0) continue;
                
                // Determine if the neighbor lies in an adjacent chunk
                var neighborInAdjacent = neighborPos.x < 0 || neighborPos.x > Chunk.CHUNK_LENGTH - 1 || neighborPos.z < 0 || neighborPos.z > Chunk.CHUNK_LENGTH - 1;
                
                // Declare variables to be fetched from chunks
                var neighbor = DefaultVoxel;
                var blockLight = 0;
                var sunlight = 0;
                
                // Fetch data from the chunk or world depending on locality
                if (neighborInAdjacent) {
                    //var bufferPos = WorldToMapLocal(chunk.LocalToWorld(voxelPos)) + VoxelNeighbors[i];
                    var bufferPos = new Vector3Int(voxelPos.x + chunk.WorldPosition.x - _mapWorldOrigin.x,
                                                   voxelPos.y,
                                                   voxelPos.z + chunk.WorldPosition.y - _mapWorldOrigin.y) + Neighbors[i];
                    
                    var neighborChunk = _chunkBuffer[(bufferPos.x >> Chunk.CHUNK_LENGTH_LOG) + ((bufferPos.z >> Chunk.CHUNK_LENGTH_LOG) * 3)];
                    
                    // Determine if the neighbor is in a neighboring chunk
                    if (neighborChunk != null) {
                        // Calculate voxel position relative to the neighbor chunk
                        var neighborLocalPos = new Vector3Int(bufferPos.x + _mapWorldOrigin.x - neighborChunk.WorldPosition.x,
                                                              bufferPos.y,
                                                              bufferPos.z + _mapWorldOrigin.y - neighborChunk.WorldPosition.y);
                        // Fetch voxel and lighting data
                        neighbor = neighborChunk.GetVoxel(neighborLocalPos);
                        blockLight = neighborChunk.GetBlockLight(neighborLocalPos);
                        sunlight = neighborChunk.GetSunlight(neighborLocalPos);
                    }
                }
                else {
                    neighbor = chunk.GetVoxel(neighborPos);
                    blockLight = chunk.GetBlockLight(neighborPos);
                    sunlight = chunk.GetSunlight(neighborPos);
                }
                
                // Skip this face if the voxel and its neighbor are opaque
                if (voxel.RenderType == RenderType.Opaque && neighbor.RenderType == RenderType.Opaque && neighbor.Id != 0) continue;
                
                // Skip if this voxel is transparent but the neighbor is opaque
                if (voxel.RenderType != RenderType.Opaque && neighbor.RenderType == RenderType.Opaque && neighbor.Id != 0) continue;
                
                // Skip if this voxel is transparent and the neighbor is of the same type
                if (voxel.RenderType != RenderType.Opaque && neighbor.Id == voxel.Id) continue;
                
                // Add UVs if necessary
                if (voxelData.UsesTextureAtlas) {
                    // Branch for multi-texture voxels
                    Rect rectA, rectE, rectM, rectS;
                    if (voxelData.PerSideTextures) {
                        // Declare default rect reference
                        rectA = voxelData.AlbedoRects[i];
                        rectE = voxelData.EmissionRects[i];
                        rectM = voxelData.MetallicRects[i];
                        rectS = voxelData.SmoothnessRects[i];
                    }
                    else {
                        rectA = voxelData.AlbedoRects[0];
                        rectE = voxelData.EmissionRects[0];
                        rectM = voxelData.MetallicRects[0];
                        rectS = voxelData.SmoothnessRects[0];
                    }

                    // Add UV0 coords
                    _uv0.Add(new Vector2(rectA.xMin, rectA.yMax));
                    _uv0.Add(rectA.max);
                    _uv0.Add(new Vector2(rectA.xMax, rectA.yMin));
                    _uv0.Add(rectA.min);

                    // Add UV0 coords
                    _uv1.Add(new Vector2(rectE.xMin, rectE.yMax));
                    _uv1.Add(rectE.max);
                    _uv1.Add(new Vector2(rectE.xMax, rectE.yMin));
                    _uv1.Add(rectE.min);

                    // Add UV0 coords
                    _uv2.Add(new Vector2(rectM.xMin, rectM.yMax));
                    _uv2.Add(rectM.max);
                    _uv2.Add(new Vector2(rectM.xMax, rectM.yMin));
                    _uv2.Add(rectM.min);

                    // Add UV0 coords
                    _uv3.Add(new Vector2(rectS.xMin, rectS.yMax));
                    _uv3.Add(rectS.max);
                    _uv3.Add(new Vector2(rectS.xMax, rectS.yMin));
                    _uv3.Add(rectS.min);
                }
                else {
                    // Add UV0 coords
                    _uv0.Add(new Vector2(0, 1));
                    _uv0.Add(new Vector2(1, 1));
                    _uv0.Add(new Vector2(1, 0));
                    _uv0.Add(new Vector2(0, 0));
                }

                // Get face vertices for each direction
                for (var j = 0; j < 4; j++) {
                    var offset = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);
                    var vertex = offset + Vertices[Triangles[j + (i * 4)]] * 0.5f;

                    if (i == 4 && voxel.Id == VoxelDictionary.IdByName("water")) {
                        vertex.y -= 0.125f;
                    }
                    
                    _vertices.Add(vertex);
                    
                    // Calculate light values
                    _vertexColors.Add(new Color(sunlight / 15f, blockLight / 15f, 0f));
                }
                
                // Add triangles
                var subIndex = (int)voxel.RenderType - 1;
                var vCount = _vertices.Count;
                _triangles[subIndex].Add(vCount - 4);
                _triangles[subIndex].Add(vCount - 3);
                _triangles[subIndex].Add(vCount - 2);
                _triangles[subIndex].Add(vCount - 4);
                _triangles[subIndex].Add(vCount - 2);
                _triangles[subIndex].Add(vCount - 1);
            }
        }

        private void ClearWorkBuffers() {
            _vertices.Clear();
            _vertexColors.Clear();

            // Clear triangle buffers
            foreach (var buffer in _triangles) {
                buffer.Clear();
            }

            // Clear UV buffers
            _uv0.Clear();
            _uv1.Clear();
            _uv2.Clear();
            _uv3.Clear();
            _rowsToUpdate.Clear();
        }
        
        private Vector3Int WorldToMapLocal(in Vector3Int world) {
            return new Vector3Int(world.x - _mapWorldOrigin.x, world.y, world.z - _mapWorldOrigin.y);
        }
        
        private Vector3Int MapLocalToWorld(in Vector3Int world) {
            return new Vector3Int(world.x + _mapWorldOrigin.x, world.y, world.z + _mapWorldOrigin.y);
        }
    }
}
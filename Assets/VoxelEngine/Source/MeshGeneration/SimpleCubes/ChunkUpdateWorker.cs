using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RTSEngine;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Chunks;
using VoxelEngine.MarchingCubes;
using VoxelEngine.Voxels;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.MeshGeneration.SimpleCubes {
    /// <summary>
    /// Generates a cubic mesh from a given chunk using naive meshing.
    /// Greedy meshing should be implemented eventually.
    /// </summary>
    public class ChunkUpdateWorker {
        private static readonly Vector3[] FaceVertices = {
            new Vector3(1, 1, 1), 
            new Vector3(-1, 1, 1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, -1, -1),
        };

        private const float UV_ATLAS_FRAC = 1f / 8f;
        private static readonly Vector2[] FaceUVs = {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0), 
        };

        private static readonly int[] FaceTriangles = {
            0, 1, 2, 
            3, 5, 0, 
            3, 6, 4, 
            5, 6, 7,
            1, 4, 7, 
            2, 5, 4, 
            1, 0, 3, 
            2, 7, 6
        };

        private static readonly Vector3Int[] VoxelNeighbors = {
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 0), 
            new Vector3Int(0, 0, -1), 
            new Vector3Int(-1, 0, 0), 
            new Vector3Int(0, 1, 0), 
            new Vector3Int(0, -1, 0),
        };
        
        // Data Buffers
        private readonly List<Vector3> _vertices = new List<Vector3>(65535);
        private readonly List<TriangleIndex> _triangles = new List<TriangleIndex>(65535);
        private readonly List<Vector2> _uvs = new List<Vector2>(65535);
        private readonly List<Color> _vertexColors = new List<Color>(65535);
        private int _submeshIndex;
        
        // Job/Result Queues
        private readonly BlockingCollection<MeshJob> _jobQueue;
        private readonly ConcurrentQueue<MeshResult> _resultQueue;
        
        // Worker Thread
        private Thread _workerThread;
        private ManualResetEvent _threadControl;
        private bool _terminateFlag = false;
        
        // Chunk Light Mapper
        private readonly ChunkLightMapper _lightMapper;
        
        // Debug
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lightMapTime, _chunkMeshTime;
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Creates a new simple cubes mesh extraction worker.
        /// </summary>
        /// <param name="jobQueue">The job queue to pull jobs from.</param>
        /// <param name="resultQueue">The result queue to push results to.</param>
        public ChunkUpdateWorker(BlockingCollection<MeshJob> jobQueue, ConcurrentQueue<MeshResult> resultQueue) {
            _jobQueue = jobQueue;
            _resultQueue = resultQueue;
            
            // Initialize job/result queues
            _jobQueue = jobQueue;
            _resultQueue = resultQueue;
            
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
            foreach (var meshJob in _jobQueue.GetConsumingEnumerable()) {
                IsBusy = true;
                try {
                    GenerateMeshData(meshJob.World, meshJob.Chunk);
                    _resultQueue.Enqueue(new MeshResult(meshJob.Id, _vertices.ToArray(), _vertexColors.ToArray(), _triangles.ToArray(), _uvs.ToArray(), meshJob.Callback));
                    DebugSystem.Instance.LogDirect($"Chunk Worker: Update task for chunk {meshJob.Chunk.Position} complete! Lighting: {_lightMapTime}ms Mesh: {_chunkMeshTime}ms");
                    _stopwatch.Reset();
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Generates a cube mesh from a given chunk and world.
        /// </summary>
        /// <returns></returns>
        private void GenerateMeshData(VoxelWorld world, Chunk chunk) {
            // Clear vertex and triangle arrays
            _vertices.Clear();
            _vertexColors.Clear();
            _triangles.Clear();
            _uvs.Clear();
            
            // We have to update lighting before meshing
            _stopwatch.Start();
            _lightMapper.UpdateBlockLightMap(world, chunk);

            _stopwatch.Stop();
            _lightMapTime = _stopwatch.ElapsedMilliseconds;
            
            _stopwatch.Reset();
            _stopwatch.Start();
            // Process each cube
            for (var i = 0; i < Chunk.VoxelCount; i++) {
                var z = i & (Chunk.CHUNK_LENGTH - 1);
                var y = (i >> 4) & (Chunk.CHUNK_HEIGHT - 1);
                var x = i >> 12;
                
                // We can skip voxels above the height map as they will always be air
                var skipHeight = chunk.GetVoxelHeight(x, z);
                //if (y > skipHeight) continue;
                        
                // Calculate voxel world position and run the process voxel function
                var voxelPos = new Vector3Int(x, y, z);
                ProcessVoxel(world, chunk, voxelPos);
            }
            _stopwatch.Stop();
            _chunkMeshTime = _stopwatch.ElapsedMilliseconds;
            _stopwatch.Reset();
        }
       
        /// <summary>
        /// Adds relevant faces for a single voxel.
        /// </summary>
        private void ProcessVoxel(VoxelWorld world, Chunk chunk, Vector3Int voxelPos) {
            // Skip if the current voxel is empty (air)
            var voxel = chunk.GetVoxel(voxelPos);
            _submeshIndex = voxel.Id == MaterialDictionary.DataByName("water").Id ? 1 : 0;
            if (voxel.Id == 0) return;
            
            // Populate vertices for each face
            for (var i = 0; i < 6; i++) {
                // Calculate neighbor position
                var neighborPos = voxelPos + VoxelNeighbors[i];
                
                // Skip face if the neighbor Y is out of bounds
                if (neighborPos.y < 0) continue;
                
                // Determine if the neighbor lies in an adjacent chunk
                var neighborInAdjacent = neighborPos.x < 0 || neighborPos.x > Chunk.CHUNK_LENGTH - 1 || neighborPos.z < 0 || neighborPos.z > Chunk.CHUNK_LENGTH - 1;
                
                // Fetch the neighbor data from the chunk or world depending on locality
                var neighbor = neighborInAdjacent ? world.GetVoxel(new Vector3Int(neighborPos.x + chunk.WorldPosition.x, neighborPos.y, neighborPos.z + chunk.WorldPosition.y)) 
                                                  : chunk.GetVoxel(neighborPos);
                
                // Skip this face if the voxel and its neighbor are opaque
                if (!voxel.IsTransparent && !neighbor.IsTransparent && neighbor.Id != 0) continue;
                
                // Skip if this voxel is transparent but the neighbor is opaque
                if (voxel.IsTransparent && !neighbor.IsTransparent && neighbor.Id != 0) continue;
                
                // Skip if this voxel is transparent and the neighbor is of the same type
                if (voxel.IsTransparent && neighbor.Id == voxel.Id) continue;
                
                // Add UVs
                for (var k = 0; k < 4; k++) {
                    var coord = FaceUVs[k] * UV_ATLAS_FRAC;
                    coord.x += (voxel.Id - 1) % 8 * UV_ATLAS_FRAC;
                    coord.y += (voxel.Id - 1) / 8 * UV_ATLAS_FRAC;
                    
                    if (voxel.Id == MaterialDictionary.DataByName("grass").Id && i == 4)
                        coord.y += UV_ATLAS_FRAC;
                    _uvs.Add(coord);
                }
                
                // Get face vertices for each direction
                var neighborBlockLight = world.GetBlockLight(new Vector3Int(neighborPos.x + chunk.WorldPosition.x, neighborPos.y, neighborPos.z + chunk.WorldPosition.y)) / 15f;
                var neighborSunlight = world.GetSunlight(new Vector3Int(neighborPos.x + chunk.WorldPosition.x, neighborPos.y, neighborPos.z + chunk.WorldPosition.y)) / 15f;
                for (var j = 0; j < 4; j++) {
                    var offset = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);
                    var vertex = offset + FaceVertices[FaceTriangles[j + (i * 4)]] * 0.5f;
                    _vertices.Add(vertex);
                    
                    // Calculate light values
                    
                    _vertexColors.Add(new Color(Mathf.Max(neighborSunlight, neighborBlockLight), 0f, 0f));
                }
                
                // Add triangles
                var vCount = _vertices.Count;
                _triangles.Add(new TriangleIndex(vCount - 4, _submeshIndex));
                _triangles.Add(new TriangleIndex(vCount - 3, _submeshIndex));
                _triangles.Add(new TriangleIndex(vCount - 2, _submeshIndex));
                _triangles.Add(new TriangleIndex(vCount - 4, _submeshIndex));
                _triangles.Add(new TriangleIndex(vCount - 2, _submeshIndex));
                _triangles.Add(new TriangleIndex(vCount - 1, _submeshIndex));
            }
        }
    }
}
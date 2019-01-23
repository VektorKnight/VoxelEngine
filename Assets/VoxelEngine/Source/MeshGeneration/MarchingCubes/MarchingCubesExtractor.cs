using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RTSEngine;
using UnityEngine;
using VoxelEngine.MeshGeneration;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.MarchingCubes {
    /// <summary>
    /// Copyright (c) 2017 Justin Hawkins
    ///
    /// Permission is hereby granted, free of charge, to any person obtaining a copy
    /// of this software and associated documentation files (the "Software"), to deal
    /// in the Software without restriction, including without limitation the rights
    /// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    /// copies of the Software, and to permit persons to whom the Software is
    /// furnished to do so, subject to the following conditions:
    ///
    /// The above copyright notice and this permission notice shall be included in all
    /// copies or substantial portions of the Software.
    ///
    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    /// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    /// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    /// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    /// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    /// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    /// SOFTWARE.
    /// </summary>
    /*public class MarchingCubesExtractor {
        /// <summary>
        /// Vertex offsets from the current voxel defining a cube.
        /// </summary>
        private static readonly int[,] VertexOffset = {
            {0, 0, 0}, {1, 0, 0}, {1, 1, 0}, {0, 1, 0},
            {0, 0, 1}, {1, 0, 1}, {1, 1, 1}, {0, 1, 1}
        };

        private static readonly int[] WindingOrder= { 2, 1, 0 };
       
        /// <summary>
        /// edgeDirection lists the direction vector (vertex1-vertex0) for each edge in the cube.
        /// edgeDirection[12][3]
        /// </summary>
        private static readonly float[,] EdgeDirection = {
            {1.0f, 0.0f, 0.0f}, {0.0f, 1.0f, 0.0f}, {-1.0f, 0.0f, 0.0f}, {0.0f, -1.0f, 0.0f},
            {1.0f, 0.0f, 0.0f}, {0.0f, 1.0f, 0.0f}, {-1.0f, 0.0f, 0.0f}, {0.0f, -1.0f, 0.0f},
            {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f, 1.0f}
        };
        
        /// <summary>
        /// EdgeConnection lists the index of the endpoint vertices for each 
        /// of the 12 edges of the cube.
        /// edgeConnection[12][2]
        /// </summary>
        private static readonly int[,] EdgeConnection = {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
        };
        
        // Data Buffers
        private readonly List<Vector3> _vertices = new List<Vector3>(65536);
        private readonly List<TriangleIndex> _triangles = new List<TriangleIndex>(65536);
        private readonly List<Vector2> _uvs = new List<Vector2>(65536);
        private readonly Vector3[] _edgeVertex = new Vector3[12];
        private int _submeshIndex;
        
        // Job/Result Queues
        private readonly BlockingCollection<MeshJob> _jobQueue;
        private readonly ConcurrentQueue<MeshResult> _resultQueue;
        
        // Worker Thread
        private Thread _workerThread;
        private ManualResetEvent _threadControl;
        private bool _terminateFlag = false;
        
        // Debug
        private Stopwatch _stopwatch = new Stopwatch();
        
        /// <summary>
        /// Create a new marching cubes mesh extractor.
        /// </summary>
        public MarchingCubesExtractor(BlockingCollection<MeshJob> jobQueue, ConcurrentQueue<MeshResult> resultQueue) {
            // Initialize job/result queues
            _jobQueue = jobQueue;
            _resultQueue = resultQueue;
            
            // Initialize worker thread
            var workerStart = new ThreadStart(WorkLoop);
            _workerThread = new Thread(workerStart) { IsBackground = true };
            _workerThread.Start();
        }

        private void WorkLoop() {
            foreach (var meshJob in _jobQueue.GetConsumingEnumerable()) {
                _stopwatch.Start();
                GenerateMeshData(meshJob.World, meshJob.Chunk);
                //_resultQueue.Enqueue(new MeshResult(_vertices.ToArray(), _triangles.ToArray(), _uvs.ToArray(), meshJob.Callback));
                _stopwatch.Stop();
                DebugSystem.Instance.LogDirect($"Mesh generation took {_stopwatch.ElapsedMilliseconds}ms");
                _stopwatch.Reset();
            }
        }

        /// <summary>
        /// Extracts a marching cubes mesh from a given chunk.
        /// </summary>
        public void GenerateMeshData(VoxelWorld world, Chunk chunk) {
            // Initialize vertex and triangle lists
            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            
            // Run marching cubes on each voxel (sequential)
            for (var i = 0; i < Chunk.VoxelCount; i++) {
                var z = i & (Chunk.CHUNK_LENGTH - 1);
                var y = (i >> 4) & (Chunk.CHUNK_HEIGHT - 1);
                var x = i >> 12;
                
                // We can skip voxels above the height map as they will always be air
                var skipHeight = chunk.GetVoxelHeight(x, z);
                if (y > skipHeight + 4) continue;
                        
                // Calculate voxel world position and run the marching cubes algorithm
                var voxelPos = new Vector3Int(chunk.WorldPosition.x + x, y, chunk.WorldPosition.y + z);
                March(world, voxelPos, chunk.WorldPosition);
            }
        }

        /// <summary>
        /// Performs the marching cubes algorithm on a single voxel.
        /// </summary>
        private void March(VoxelWorld world, Vector3Int voxelPos, Vector2Int chunkPos) {
            // Define the flag index for the LUT
            var flagIndex = 0;
            
            // Determine which vertices are inside/outside the surface
            for (var i = 0; i < 8; i++) {
                
                // Reference voxel at current offset
                var neighborPos = new Vector3Int(
                    voxelPos.x + VertexOffset[i, 0],
                    voxelPos.y + VertexOffset[i, 1],
                    voxelPos.z + VertexOffset[i, 2]
                );

                var voxel = world.GetVoxel(neighborPos);
                _submeshIndex = voxel.ID <= 1 ? 0 : voxel.ID - 1;
                
                // Set flag if voxel is solid (Material ID > 0 and Solid)
                if (voxel.ID != 0 && voxel.IsSolid) flagIndex |= 1 << i;
            }
            
            // Determine edges intersection the surface
            var edgeFlag = MarchingCubesTables.CubeEdgeFlags[flagIndex];
            
            // Return if the cube is entirely inside or outside the surface
            if (edgeFlag == 0) return;
            
            // Find point of surface intersection with each edge
            for (var i = 0; i < 12; i++) {
                // Skip if no intersection on this edge
                if ((edgeFlag & (1 << i)) == 0) continue;
                
                // Calculate surface offset
                var offset = 0.5f; //GetOffset(densityData[EdgeConnection[i, 0]], densityData[EdgeConnection[i, 1]]);
                var vertex = _edgeVertex[i];
                vertex.x = (voxelPos.x - chunkPos.x) + (VertexOffset[EdgeConnection[i, 0], 0] + offset * EdgeDirection[i, 0]);
                vertex.y =                voxelPos.y + (VertexOffset[EdgeConnection[i, 0], 1] + offset * EdgeDirection[i, 1]);
                vertex.z = (voxelPos.z - chunkPos.y) + (VertexOffset[EdgeConnection[i, 0], 2] + offset * EdgeDirection[i, 2]);
                _edgeVertex[i] = vertex;
            }
            
            // Write vertices and triangles to their respective lists
            for (var i = 0; i < 5; i++) {
                // Not sure yet
                if (MarchingCubesTables.TriangleConnectionTable[flagIndex, 3 * i] < 0) break;

                var vertexCount = _vertices.Count;

                for (var j = 0; j < 3; j++) {
                    var vert = MarchingCubesTables.TriangleConnectionTable[flagIndex, 3 * i + j];
                    _triangles.Add(new TriangleIndex(vertexCount + WindingOrder[j], _submeshIndex));
                    _vertices.Add(_edgeVertex[vert]);
                }
            }
        }
    }*/
}
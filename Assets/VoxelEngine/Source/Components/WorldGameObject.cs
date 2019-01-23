using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VektorLibrary.Utility;
using VoxelEngine.Chunks;
using VoxelEngine.MarchingCubes;
using VoxelEngine.MeshGeneration;
using VoxelEngine.MeshGeneration.SimpleCubes;
using VoxelEngine.Utility;

namespace VoxelEngine {
    /// <summary>
    /// 
    /// </summary>
    public class WorldGameObject : MonoBehaviour {
        public static VoxelWorld World { get; private set; }

        public ChunkGameObject ChunkPrefab;

        private int _loadDistance = 32;

        private int _xCount, _yCount;

        private ChunkUpdateWorker[] _meshExtractors;
        public BlockingCollection<MeshJob> MeshJobs;
        private ConcurrentQueue<MeshResult> _meshResults;
        
        // Initialization
        private void Start() {
            World = new VoxelWorld(4582);
            
            MeshJobs = new BlockingCollection<MeshJob>(new ConcurrentStack<MeshJob>());
            _meshResults = new ConcurrentQueue<MeshResult>();
            
            // Instantiate mesh workers proportional to system thread count
            var workerCount = Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : 2;
            _meshExtractors = new ChunkUpdateWorker[workerCount];
            for (var i = 0; i < _meshExtractors.Length; i++) {
                _meshExtractors[i] = new ChunkUpdateWorker(MeshJobs, _meshResults);
            }
            
            // Start the chunk generation routine
            StartCoroutine(nameof(GenerateChunks));
        }
        
        // Cleanup
        private void OnDestroy() {
            MeshJobs.CompleteAdding();
        }
        
        // Fixed Update
        private void FixedUpdate() {
            // Process mesh result queues and dispatch to relevant objects
            while (_meshResults.Count > 0) {
                MeshResult result;
                if (!_meshResults.TryDequeue(out result)) continue;
                result.Callback?.Invoke(result);
            }
            
            // 
            if (DevReadout.Instance != null) {
                for (var i = 0; i < _meshExtractors.Length; i++) {
                    DevReadout.UpdateField($"Chunk Thread {i}", _meshExtractors[i].IsBusy ? "Busy" : "Idle");
                }
            }
        }
        
        // Chunk loading routine
        private IEnumerator GenerateChunks() {
            int x=0, y=0, dx = 0, dy = -1;
            int t = _loadDistance;
            int maxI = t*t;

            for (int i=0; i < maxI; i++){
                if ((-_loadDistance/2 <= x) && (x <= _loadDistance/2) && (-_loadDistance/2 <= y) && (y <= _loadDistance/2)) {
                    var chunk = World.GetOrLoadChunk(new Vector2Int(x, y));

                    var chunkObject = Instantiate(ChunkPrefab, new Vector3(x * Chunk.CHUNK_LENGTH, 0f, y * Chunk.CHUNK_LENGTH), Quaternion.identity);
                    chunkObject.Initialize(this, chunk);
                    yield return null;
                }

                if( (x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1-y))) {
                    t=dx; dx=-dy; dy=t;
                }   
                x+=dx; y+=dy;
            }

            //yield return null;
        }
    }
}
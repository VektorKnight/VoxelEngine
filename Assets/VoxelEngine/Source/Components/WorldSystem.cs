using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;
using VoxelEngine.Chunks;
using VoxelEngine.Chunks.LightMapping;
using VoxelEngine.Chunks.MeshGeneration;
using VoxelEngine.Textures;
using VoxelEngine.Utility;
using VoxelEngine.Voxels;

namespace VoxelEngine.Components {
    /// <summary>
    /// 
    /// </summary>
    public class WorldSystem : MonoBehaviour {
        // Singleton Instance Accessor
        public static WorldSystem Instance { get; private set; }
        
        // Voxel World Reference
        public VoxelWorld VoxelWorld { get; private set; }
        
        // Runtime Texture Atlas Instance
        public RuntimeTextureAtlas AtlasGenerator { get; private set; }

        // Chunk Object Prefab
        private ChunkGameObject _chunkPrefab;

        private int _loadDistance = 24;

        private int _xCount, _yCount;

        private ChunkUpdateWorker[] _meshExtractors;
        public BlockingCollection<ChunkUpdateJob> UpdateJobs;
        private ConcurrentStack<ChunkMeshResult> _meshResults;
        private ConcurrentStack<ChunkLightResult> _lightResults;

        // Runtime Singleton Initialization
        [RuntimeInitializeOnLoadMethod]
        private static void InitializeSingleton() {
            // Create a new game object and attach this system
            var worldObject = new GameObject("Voxel World System");
            Instance = worldObject.AddComponent<WorldSystem>();
            
            // Load the chunk prefab from the resources folder
            Instance._chunkPrefab = Resources.Load<ChunkGameObject>("Common/ChunkObject");
            
            // Initialize the instance
            Instance.Initialize();
        }
        
        // Initialization
        private void Initialize() {
            VoxelWorld = new VoxelWorld(4582);
            
            UpdateJobs = new BlockingCollection<ChunkUpdateJob>(new ConcurrentStack<ChunkUpdateJob>());
            _meshResults = new ConcurrentStack<ChunkMeshResult>();
            _lightResults = new ConcurrentStack<ChunkLightResult>();
            
            // Initialize the voxel dictionary
            VoxelDictionary.InitializeVoxelDictionary();
            
            // Instantiate a new runtime texture atlas
            AtlasGenerator = new RuntimeTextureAtlas();
            
            // Instantiate mesh workers proportional to system thread count
            var workerCount =  Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : 2;
            _meshExtractors = new ChunkUpdateWorker[workerCount];
            for (var i = 0; i < _meshExtractors.Length; i++) {
                _meshExtractors[i] = new ChunkUpdateWorker(UpdateJobs, _meshResults);
            }
            
            // Start the chunk generation routine
            StartCoroutine(nameof(GenerateChunks));
        }
        
        // Cleanup
        private void OnDestroy() {
            UpdateJobs.CompleteAdding();
        }
        
        // Fixed Update
        private void Update() {
            // Process mesh result queue and dispatch to relevant objects
            ChunkMeshResult meshResult;
            if (_meshResults.TryPop(out meshResult)) {
                meshResult.Callback?.Invoke(meshResult);
            }
            
            // Process light result queue and dispatch to relevant objects
            ChunkLightResult lightResult;
            if (_lightResults.TryPop(out lightResult)) {
                lightResult.LightCallback?.Invoke(lightResult);
            }
            
            // 
            if (DevReadout.Instance != null) {
                for (var i = 0; i < _meshExtractors.Length; i++) {
                    DevReadout.UpdateField($"Worker Thread {i}", _meshExtractors[i].IsBusy ? "Busy" : "Idle");
                }
            }
        }
        
        // Chunk loading routine
        private IEnumerator GenerateChunks() {
                int x=0, y=0, dx = 0, dy = -1;
                int t = _loadDistance;
                int maxI = t*t;

                for (var i=0; i < maxI; i++){
                    if ((-_loadDistance/2 <= x) && (x <= _loadDistance/2) && (-_loadDistance/2 <= y) && (y <= _loadDistance/2)) {

                        var chunkObject = Instantiate(_chunkPrefab, new Vector3(x * Chunk.CHUNK_LENGTH, 0f, y * Chunk.CHUNK_LENGTH), Quaternion.identity);
                        chunkObject.Initialize(new Vector2Int(x, y));
                        yield return null;
                    }

                    if( (x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1-y))) {
                        t=dx; dx=-dy; dy=t;
                    }   
                    x+=dx; y+=dy;
                } 
            yield return null;
        }
    }
}
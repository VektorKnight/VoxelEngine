using UnityEngine;
using VoxelEngine.Chunks;
using VoxelEngine.Voxels;

namespace VoxelEngine.Biomes {
    public class RollingHills : IBiome {
        // Noise Layers & Settings
        private FastNoise[] _noiseLayers;
        private FastNoise.Interp _interpolation;
        
        // Constructor
        public RollingHills(int seed) {
            
            // Initialize noise layer array
            _noiseLayers = new FastNoise[2];
            
            // Configure noise layer 0
            _noiseLayers[0] = new FastNoise(seed);
            _noiseLayers[0].SetNoiseType(FastNoise.NoiseType.SimplexFractal);
            _noiseLayers[0].SetFractalOctaves(4);
            _noiseLayers[0].SetFractalLacunarity(1.0f);
            _noiseLayers[0].SetFractalGain(0.5f);
            _noiseLayers[0].SetFrequency(0.001f);
            
            // Configure noise layer 1
            _noiseLayers[1] = new FastNoise(seed);
            _noiseLayers[1].SetNoiseType(FastNoise.NoiseType.PerlinFractal);
            _noiseLayers[1].SetFractalOctaves(4);
            _noiseLayers[1].SetFractalLacunarity(2.0f);
            _noiseLayers[1].SetFractalGain(0.3f);
            _noiseLayers[1].SetFrequency(0.01f);
        }

        public void SetSeed(int seed) {
            throw new System.NotImplementedException();
        }

        public float GetNoiseValue(int x, int y) {
            var noiseValue = 0f;
            foreach (var layer in _noiseLayers) {
                noiseValue += layer.GetNoise(x, y);
            }

            return noiseValue / _noiseLayers.Length;
        }

        public Voxel GetVoxelAtHeight(int height, int y) {              
            // Top layer should be grass
            var id = VoxelDictionary.IdByName("grass");

            if (y < height) id = VoxelDictionary.IdByName("dirt");

            if (y < height - 2) id = VoxelDictionary.IdByName("stone");
            
            if (y > height && y <= Chunk.SEA_LEVEL) {
                id = VoxelDictionary.IdByName("water");
            }

            if (y == height && y <= Chunk.SEA_LEVEL) {
                id = VoxelDictionary.IdByName("sand");
            }

            if (y > height && y > Chunk.SEA_LEVEL) {
                id = 0;
            }

            return new Voxel(id);
        }
    }
}
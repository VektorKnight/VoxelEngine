using VoxelEngine.Chunks;
using VoxelEngine.Voxels;

namespace VoxelEngine.Biomes {
    public class Ocean : IBiome {
        // Noise Layers & Settings
        private FastNoise[] _noiseLayers;
        private FastNoise.Interp _interpolation;

        // Constructor
        public Ocean(int seed) {
            // Initialize noise layer array
            _noiseLayers = new FastNoise[1];

            // Configure noise layer 0
            _noiseLayers[0] = new FastNoise(seed);
            _noiseLayers[0].SetNoiseType(FastNoise.NoiseType.CubicFractal);
            _noiseLayers[0].SetFractalOctaves(2);
            _noiseLayers[0].SetFractalLacunarity(2.0f);
            _noiseLayers[0].SetFractalGain(1f);
            _noiseLayers[0].SetFrequency(0.005f);
        }

        public void SetSeed(int seed) {
            throw new System.NotImplementedException();
        }

        public float GetNoiseValue(int x, int y) {
            return _noiseLayers[0].GetNoise(x, y) - 0.1f;
        }

        public Voxel GetVoxelAtHeight(int height, int y) {
            // Top layer should be grass
            var id = MaterialDictionary.DataByName("sand").Id;

            if (y < height - 1) id = MaterialDictionary.DataByName("dirt").Id;

            if (y < height - 2) id = MaterialDictionary.DataByName("stone").Id;

            if (y > height && y <= Chunk.SEA_LEVEL) {
                id = MaterialDictionary.DataByName("water").Id;
            }

            if (y > height && y > Chunk.SEA_LEVEL) {
                id = 0;
            }
            
            return new Voxel(id);
        }
    }
}
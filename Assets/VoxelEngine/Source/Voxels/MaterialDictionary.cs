using System.Collections.Generic;

namespace VoxelEngine.Voxels {
    public static class MaterialDictionary {
        private static readonly Dictionary<string, int> IdMapping = new Dictionary<string, int> {
            {"air",         0},
            {"stone",       1},
            {"dirt",        2},
            {"grass",       3},
            {"sand",        4},
            {"gravel",      5},
            {"cobblestone", 6},
            {"water",       7},
            {"flowing-water", 8},
            {"bedrock", 9},
            {"glowstone", 10}
        };
        
        private static readonly Dictionary<int, MaterialData> MaterialData = new Dictionary<int, MaterialData> {
            {0, new MaterialData(0, MaterialType.None, 0, true, false, false)},
            {1, new MaterialData(1, MaterialType.Solid, 7, false, true, false)},
            {2, new MaterialData(2, MaterialType.Solid, 1, false, true, false)},
            {3, new MaterialData(3, MaterialType.Solid, 2, false, true, true)},
            {4, new MaterialData(4, MaterialType.Solid, 1, false, true, false)},
            {5, new MaterialData(5, MaterialType.Solid, 1, false, true, false)},
            {6, new MaterialData(6, MaterialType.Solid, 6, false, true, false)},
            {7, new MaterialData(7, MaterialType.Liquid, 0, true, false, false)},
            {8, new MaterialData(8, MaterialType.Liquid, 0, true, false, false)},
            {9, new MaterialData(9, MaterialType.Solid, 255, false, true, false)},
            {10, new MaterialData(10, MaterialType.Solid, 1, true, true, false, true, 15)}
        };

        public static MaterialData DataById(ushort id) {
            return MaterialData[id];
        }

        public static MaterialData DataByName(string name) {
            return MaterialData[IdMapping[name]];
        }
    }
}
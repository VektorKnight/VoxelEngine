using System;

namespace VoxelEngine {
    public class ChunkLoadException : Exception {
        public ChunkLoadException() { }
        public ChunkLoadException(string message) : base(message) { }
        public ChunkLoadException(string message, Exception innerException) : base(message, innerException) { }
    }
}
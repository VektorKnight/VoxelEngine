using System.Runtime.InteropServices;
using VoxelEngine.Voxels;

namespace VoxelEngine {
	/// <summary>
	/// Represents a single voxel in a chunk.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct Voxel {
		// Material ID of the voxel (0-65535)
		public readonly ushort Id;
		
		// Voxel material type and transparency flag
		public readonly MaterialType Type;
		public readonly bool IsTransparent;
		
		/// <summary>
		/// Creates a new voxel with the specified properties.
		/// </summary>
		/// <param name="id">Material ID of the voxel.</param>
		public Voxel(ushort id) {
			Id = id;
			Type = MaterialDictionary.DataById(id).Type;
			IsTransparent = MaterialDictionary.DataById(id).IsTransparent;
		}
	}
}

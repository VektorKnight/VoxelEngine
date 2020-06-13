using System.Runtime.InteropServices;

namespace VoxelEngine.Voxels {
	/// <summary>
	/// Represents a single voxel in a chunk.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Voxel {
		// Material ID of the voxel (0-65535)
		public readonly ushort Id;
		
		// Voxel material type and transparency flag
		public readonly VoxelType VoxelType;
		public readonly RenderType RenderType;
		
		// Voxel light source and attenuation
		public readonly int LightValue;
		public readonly int Attenuation;
		
		/// <summary>
		/// Creates a new voxel with the specified properties.
		/// </summary>
		/// <param name="id">Material ID of the voxel.</param>
		public Voxel(ushort id) {
			Id = id;
			VoxelType = VoxelDictionary.VoxelData[id].VoxelType;
			RenderType = VoxelDictionary.VoxelData[id].RenderType;
			LightValue = VoxelDictionary.VoxelData[id].LightValue;
			Attenuation = VoxelDictionary.VoxelData[id].Attenuation;
		}
	}
}

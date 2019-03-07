using UnityEngine;

namespace VoxelEngine.Components {
    public class RenderDepthTexture : MonoBehaviour {
        private void OnEnable() {
            GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
        }
    }
}
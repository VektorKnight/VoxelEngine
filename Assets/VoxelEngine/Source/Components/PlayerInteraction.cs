using UnityEngine;
using VoxelEngine.Voxels;

namespace VoxelEngine.Components {
    public class PlayerInteraction : MonoBehaviour {
        public LayerMask Mask;
        
        private void Update() {
            if (Input.GetKeyDown(KeyCode.Mouse0)) {
                RaycastHit hit;
                var rayHit = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 12f, Mask);
                
                if (rayHit) {
                    var hitPoint = hit.point + (Camera.main.transform.forward * 0.25f);
                    var voxelPos = new Vector3Int(Mathf.RoundToInt(hitPoint.x), Mathf.RoundToInt(hitPoint.y), Mathf.RoundToInt(hitPoint.z));
                    
                    WorldSystem.Instance.VoxelWorld.TrySetVoxel(voxelPos, new Voxel(0));
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Mouse1)) {
                RaycastHit hit;
                var rayHit = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 12f, Mask);
                
                if (rayHit) {
                    var hitPoint = hit.point;
                    var voxelPos = new Vector3Int(Mathf.RoundToInt(hitPoint.x + hit.normal.x * 0.6f), Mathf.RoundToInt(hitPoint.y + hit.normal.y * 0.5f), Mathf.RoundToInt(hitPoint.z + hit.normal.z * 0.5f));
                    
                    WorldSystem.Instance.VoxelWorld.TrySetVoxel(voxelPos, new Voxel(VoxelDictionary.IdByName("light_stone_lamp")));
                }
            }
        }
    }
}
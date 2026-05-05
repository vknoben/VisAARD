using MixedReality.Toolkit.UX;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class SpatialMesh : MonoBehaviour
{
    #region Class properties

    [Tooltip("Toggle collection for spatial mesh (occlusion, wireframe, transparent)")]
    public ToggleCollection spatialMeshToggles;
    [Tooltip("Spatial mesh prefabs")]
    public MeshFilter[] spatialMeshes;

    [Tooltip("Occlusion, wireframe & transparent material")]
    public Material[] materials;

    #endregion

    #region Class methods

    public void SpatialMeshToggled()
    {
//#if !UNITY_EDITOR
        // Get ARMeshManager component on this GO
        ARMeshManager arMeshManager = GetComponent<ARMeshManager>();

        // Get current index of active toggle (0: occlusion, 1: wireframe, 2: transparent)
        switch (spatialMeshToggles.CurrentIndex)
        {
            case 0:
                // Occlusion selected
                // Change prefab (for future mesh objects)
                arMeshManager.meshPrefab = spatialMeshes[0];
                
                // Change material of every currently existing rendered mesh to target material
                foreach (var mesh in arMeshManager.meshes)
                {
                    mesh.GetComponent<MeshRenderer>().material = materials[0];
                }

                //Log.Msg("Occlusion mesh selected");
                break;
            case 1:
                // Wireframe selected
                // Change prefab (for future mesh objects)
                arMeshManager.meshPrefab = spatialMeshes[1];

                // Change material of every currently existing rendered mesh to target material
                foreach (var mesh in arMeshManager.meshes)
                {
                    mesh.GetComponent<MeshRenderer>().material = materials[1];
                }

                //Log.Msg("Wireframe mesh selected");
                break;
            case 2:
                // Transparent selected
                // Change prefab (for future mesh objects)
                arMeshManager.meshPrefab = spatialMeshes[2];

                // Change material of every currently existing rendered mesh to target material
                foreach (var mesh in arMeshManager.meshes)
                {
                    mesh.GetComponent<MeshRenderer>().material = materials[2];
                }

                //Log.Msg("Transparent mesh selected");
                break;
            default:
                break;
        }
//#endif
    }

#endregion
}

/// <summary>
/// This script makes a given object face the user on Start but on the height of the object itself
/// </summary>

using UnityEngine;

public class MakeObjFaceUser : MonoBehaviour
{
    #region Class properties

    [SerializeField, Tooltip("Rotate object only around local y-axis. This means the object faces along the shortest path from object to user plane")]
    private bool yRotOnly = false;
    [SerializeField, Tooltip("Update continuously to face user all the time")]
    private bool continuous = false;
    [Tooltip("Time for how long to update continuously. Disabled if continuous is false")]
    public float timeToUpdate;
    [Tooltip("Timer measuring elapsed time since start of continuous update. Disabled if continuous is false")]
    private float elapsedUpdateTime = 0f;


    [Tooltip("Cached main camera")]
    private Camera mainCam = null;
    #endregion


    #region Unity lifecycle

    // Face user on enabling
    private void OnEnable()
    {
        // Cache main camera
        mainCam = Camera.main;

        FaceUser();

        // Disable component immediately if not continuous
        if (!continuous)
        {
            enabled = false;
        }
    }

    // Face user constantly
    private void Update()
    {
        if (continuous && mainCam != null)
        {
            // Update timer
            elapsedUpdateTime += Time.deltaTime;

            // Check if specified update time reached
            if (elapsedUpdateTime > timeToUpdate)
            {
                // Reset variables and disable component
                elapsedUpdateTime = 0f;
                enabled = false;
            }

            // Make object face user for now
            FaceUser();
        }
    }

    #endregion


    #region Class methods

    // Method to make this object face user
    public void FaceUser()
    {
        // Differentiate
        if (yRotOnly)
        {
            // Make object face user along shortest path to user plane
            Vector3 targetPos = mainCam.transform.position;
            targetPos = new Vector3(targetPos.x, transform.position.y, targetPos.z);
            //targetTransform.rotation = Quaternion.LookRotation(2 * transform.position - targetTransform.position);

            transform.LookAt(2 * transform.position - targetPos);
        }
        else
        {
            // Make object face user directly
            transform.LookAt(mainCam.transform);
            transform.Rotate(Vector3.up, 180f);
        }
    }

    #endregion
}

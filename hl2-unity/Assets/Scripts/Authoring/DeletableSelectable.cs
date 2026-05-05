/// <summary> Added to 3d objects in both manual and assisted authoring modes that can be selected for deletion. Handles user input to select object for deletion and shows appropriate confirm deletion dialog. </summary>
using UnityEngine;

public class DeletableSelectable : MonoBehaviour
{
    [Tooltip("Owner class instance handling references to 3d instructions in manual authoring (Author3d). Passed on instantiation of object")]
    public Author3d author3d;

    [Tooltip("Owner class instance handling references to 3d instructions in assisted authoring (InstructionManager). Passed on display of in-situ object")]
    public InstructionManager instructionManager;

    // User wants to delete object
    public void UserRequestsDelete()
    {
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Assisted authoring

            // Check it this is an animated instruction step (e.g. open/close)
            if (instructionManager.currentInstructionSet.isAnimated)
            {
                // Animated instruction set

                // Show confirm deletion dialog (animation specific)
                instructionManager.deleteAnimated3dUI.SetActive(true);

                //Log.Msg($"Selected animation to delete");
            }
            else
            {
                // Regular instruction set (non-animated)

                // Show confirm deletion dialog
                instructionManager.delete3dUI.SetActive(true);

                // Cache name/index of object to be deleted
                instructionManager.objToDeleteIndex = int.Parse(gameObject.name);

                //Log.Msg($"Selected object {instructionManager.objToDeleteIndex} to delete");
            }
        }
        else
        {
            // Manual authoring

            // Show confirm deletion dialog
            author3d.deleteObjUI.SetActive(true);

            // Cache name/index of object to be deleted
            author3d.selectedObjIndex = int.Parse(gameObject.name);

            //Log.Msg($"Selected object {author3d.selectedObjIndex} to delete");
        }
    }
}
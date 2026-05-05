using MixedReality.Toolkit.UX;
using System.Collections.Generic;
using UnityEngine;

public class VerifyIfVideoExists : MonoBehaviour
{
    // Relevant objects to enable/disable if video exists or not
    public List<GameObject> objs = new List<GameObject>();
    // Relevant button components to enable/disable if video exists or not
    public List<PressableButton> buttons = new List<PressableButton>();
    
    // Check if video exists
    public void VerifyVideoExistence()
    {
        try
        {
            string vidPath = AuthorVideo.instance.vidPath;

            if (vidPath != null && vidPath != string.Empty)
            {
                // Video exists
                //Log.Msg($"Video already exists. Enabling buttons {name}");

                // Enable objs
                foreach (var obj in objs)
                {
                    obj.SetActive(true);
                }

                // Enable buttons
                foreach (var button in buttons)
                {
                    button.enabled = true;
                }
            }
            else
            {
                // Video does not exist yet
                //Log.Msg($"Video does not exist yet. Disabling buttons{name}");

                // Disable objs
                foreach (var obj in objs)
                {
                    obj.SetActive(false);
                }

                // Disable buttons
                foreach (var button in buttons)
                {
                    button.enabled = false;
                }
            }
        }
        catch (System.Exception e)
        {
            //Log.Msg($"Error verifying if video exists: {e.Message}");
        }
    }

    private void OnEnable()
    {
        VerifyVideoExistence();
    }
}
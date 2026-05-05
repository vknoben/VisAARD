using MixedReality.Toolkit.UX;
using UnityEngine;

public class VerifyInputfieldButton : MonoBehaviour
{
    // Button which willl be enabled/disabled depending on whether inputfield is empty or not
    public PressableButton associatedButton;
    // Inputfield of interest
    public MRTKTMPInputField inputField;

    private void OnEnable()
    {
        if (inputField != null)
        {
            // Verify inputfield for emptyness on enable and toggle associated button accordingly
            if (inputField.text != string.Empty)
            {
                // Enable done button
                associatedButton.enabled = true;
            }
            else
            {
                // Disable button
                associatedButton.enabled = false;
            }
        }
    }
}

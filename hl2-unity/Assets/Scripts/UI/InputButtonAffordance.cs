///<summary>
/// Attach this script to an (TMP) input field. Assign button which should only be enabled if this input field is non-empty.
/// </summary>
using MixedReality.Toolkit.UX;
using TMPro;
using UnityEngine;

public class InputButtonAffordance : MonoBehaviour
{
    private TMP_InputField inputField;
    public PressableButton controlledButton;

    private void Start()
    {
        // Cache input field
        inputField = GetComponent<TMP_InputField>();
    }

    // Callback for OnInputFieldValueChanged
    public void CheckInputFieldNonEmpty()
    {
        if (inputField != null)
        {
            if (inputField.text != string.Empty)
            {
                controlledButton.enabled = true;
            }
            else
            {
                controlledButton.enabled = false;
            }
        }
    }
}

/// <summary>
/// On enabling of workflow name dialog: Proposes a workflow name based on existing number of workflows (playerprefs)
/// </summary>

using MixedReality.Toolkit.UX;
using UnityEngine;

public class ProposeWorkflowName : MonoBehaviour
{
    [Tooltip("Inputfield for naming workflow")]
    public MRTKTMPInputField inputField;

    [Tooltip("Number of existing workflows in PlayerPrefs")]
    private int numOfExistingWorkflows;

    // Propose workflow name on enabling of workflow naming dialog
    private void OnEnable()
    {
        // Check how many workflows exist in PlayerPrefs
        if (PlayerPrefs.HasKey("wfCount"))
        {
            numOfExistingWorkflows = PlayerPrefs.GetInt("wfCount");

            // Update number of existing workflows
            numOfExistingWorkflows++;
            PlayerPrefs.SetInt("wfCount", numOfExistingWorkflows);
        }
        else
        {
            // This is very first workflow
            //Log.Msg("This is the very first authored workflow.");

            numOfExistingWorkflows = 1;
            PlayerPrefs.SetInt("wfCount", numOfExistingWorkflows);
        }

        // Propose a workflow name based on existing number of workflows
        string proposedName = "workflow_" + (WebSocketClient.instance.assistedAuthMode ? "assisted" : "manual") + "_" + numOfExistingWorkflows.ToString("D2");

        // Fill input field with proposed name
        inputField.text = proposedName;
    }
}

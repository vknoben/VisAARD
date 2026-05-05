using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using Msg;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIGuidance : MonoBehaviour
{
    // Singleton
    public static UIGuidance instance;

    #region UI elements

    // Handmenu
    [Tooltip("Handmenu providing control features on occasion")]
    public GameObject handmenu;
    [Tooltip("Handmenu canvas which is toggled by handtracking detection")]
    public GameObject handmenuCanvas;
    [Tooltip("Toggle debug panel")]
    public GameObject debugToggle;
    [Tooltip("Button on handmenu to reinitialize qr-code tracking")]
    public GameObject retrackQrButton;
    [Tooltip("Button on handmenu to advance to next step")]
    public GameObject hmNextStepButton;
    [Tooltip("Button on handmenu to return to previous step")]
    public GameObject hmPrevStepButton;
    [Tooltip("Button on handmenu to terminate guide")]
    public GameObject finishGuideButton;

    // Fov
    [Tooltip("Instruction text displayed to user when checking fov and correct adjustment of HL2")]
    public GameObject fovCheck;
    [Tooltip("Button to complete fov check")]
    public GameObject fovConfirmButton;

    // Debug panels
    [Tooltip("Superordinate debug object")]
    public GameObject debugObj;

    // System pop ups
    [Tooltip("List of available workflows")]
    public GameObject workflowList;
    [Tooltip("Dialog for confirming to leave guide")]
    public GameObject confirmQuitDialog;
    [Tooltip("QRCode panel")]
    public GameObject qrcodePanel;
    [Tooltip("Instruction view parent")]
    public GameObject instructionView;
    [Tooltip("Dialog to start guide")]
    public GameObject startGuideDialog;
    [Tooltip("Dialog asking user whether to stream to PC or not (only optional in manual authoring)")]
    public GameObject streamDialog;
    [Tooltip("Websocket dialog for entering server ip")]
    public GameObject wsDialog;
    [Tooltip("Input field for websocket ip")]
    public MRTKTMPInputField websocketIpInput;

    #endregion


    #region Class properties

    [Tooltip("List of currently enabled toggles on handmenu")]
    private List<GameObject> enabledTogglesHandmenu;

    #endregion


    #region UI callbacks

    // User aborted guidance 
    public void ReturnToMain()
    {
        SceneManager.LoadScene(0);
    }

    // User selected workflow
    public async void WorkflowSelected()
    {
        // Get currently selected workflow name
        int workflowIndex = GuidanceManager.instance.workflowToggles.CurrentIndex;
        //Log.Msg("Selected Workflow " + GuidanceManager.instance.workflowNames[workflowIndex]);

        // Load workflow based on selection
        await GuidanceManager.instance.LoadWorkflow(workflowIndex);

        // Update UI
        workflowList.SetActive(false);
        streamDialog.SetActive(true);
    }

    // User chose to stream to PC
    public void StreamToPc()
    {
        // Move to connection establishing step
        streamDialog.SetActive(false);
        wsDialog.SetActive(true);
    }

    // User chose not to stream to PC
    public void DontStreamToPc()
    {
        // Skip websocket connection dialog and scan qr
        streamDialog.SetActive(false);
        qrcodePanel.SetActive(true);
        //QRGuidance.instance.InitializeQRCodeTracking();
    }

    // User confirmed websocket connection ip
    public void ConfirmWebsocketAddress()
    {
        // Attempt to establish websocket connection
        WebSocketClient.instance.EstablishConnection(websocketIpInput.text);
    }

    // User aborted websocket connection attempt
    public void AbortWebsocketAddress()
    {
        // Go back to streaming choice
        wsDialog.SetActive(false);
        streamDialog.SetActive(true);
    }

    // Started guide
    public async void StartGuide()
    {
        // Update UI
        startGuideDialog.SetActive(false);
        hmNextStepButton.SetActive(true);
        hmPrevStepButton.SetActive(true);
        finishGuideButton.SetActive(true);
        handmenu.SetActive(true);

        // Load first step
        await GuidanceManager.instance.LoadStep();

        // Show first instruction
        instructionView.SetActive(true);

        // Adjust navigation buttons
        GuidanceManager.instance.ConfigureNavButtons();
    }

    // User wants to proceed to next step
    public async void ProceedToNextStep()
    {
        // Load data for next work step
        GuidanceManager.instance.currentStepNumber++;
        await GuidanceManager.instance.LoadStep();

        // Adjust navigation buttons
        GuidanceManager.instance.ConfigureNavButtons();
    }

    // User wants to return to previous step
    public async void ReturnToPrevStep()
    {
        // Load data for previous work step
        GuidanceManager.instance.currentStepNumber--;
        await GuidanceManager.instance.LoadStep();

        // Adjust navigation buttons
        GuidanceManager.instance.ConfigureNavButtons();
    }

    // User wants to quit guide
    public void QuitGuide()
    {
        confirmQuitDialog.SetActive(true);
    }

    // User confirms quitting guide
    public void QuitGuideConfirm()
    {
        // Hide dialog
        confirmQuitDialog.SetActive(false);

        // Inform pc about guidance termination
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.GUIDEFINISH, "");
        }

        // Go back to main menu (selection guidance, authoring)
        SceneManager.LoadScene("Main");
    }

    // User cancels quitting guide
    public void QuitGuideCancel()
    {
        // Hide dialog
        confirmQuitDialog.SetActive(false);
    }

    #endregion


    #region Handmenu UI callbacks

    // User toggled settings menu
    public void ToggleSettings()
    {
        // Make sure to enable handmenu
        handmenu.SetActive(true);

        // Cache other currently enabled toggles on handmenu and disable them
        enabledTogglesHandmenu = new();
        foreach (var toggle in handmenu.GetComponentsInChildren<PressableButton>())
        {
            enabledTogglesHandmenu.Add(toggle.gameObject);
            toggle.gameObject.SetActive(false);
        }

        // Enable default settings options
        debugToggle.SetActive(true);
        retrackQrButton.SetActive(QRGuidance.instance.arMarkerManager.didStart);
    }

    // User untoggled settings menu
    public void UntoggleSettings()
    {
        // Disable default settings options
        debugToggle.SetActive(false);
        retrackQrButton.SetActive(false);

        // Disable handmenu if no other toggles currently active
        if (enabledTogglesHandmenu.Count == 0)
        {
            handmenu.SetActive(false);

            return;
        }

        // Enable all previously enabled toggles
        foreach (var toggle in enabledTogglesHandmenu)
        {
            toggle.SetActive(true);
        }
    }

    // User finished field of view check
    public void FinishFovCheck()
    {
        // Disable cheking UI
        fovCheck.SetActive(false);
        fovConfirmButton.SetActive(false);
        startGuideDialog.SetActive(true);
        handmenu.SetActive(false);
    }

    #endregion


    #region Debug stuff

    public void DebugWindowToggled()
    {
        //Log.Msg("Showing debug panel now");

        // Position debug panel right in front of user
        Transform mainCamTransform = Camera.main.transform;
        Vector3 userHeadPos = mainCamTransform.position;
        Vector3 start = userHeadPos + mainCamTransform.transform.forward;
        Vector3 end = new Vector3(start.x, userHeadPos.y, start.z);
        Vector3 parallelToGroundInGazeDirection = (end - userHeadPos).normalized;
        debugObj.transform.position = userHeadPos + parallelToGroundInGazeDirection;
        debugObj.transform.LookAt(2 * debugObj.transform.position - mainCamTransform.transform.position);

        // Show debug panel
        debugObj.SetActive(true);

        // Enable untoggle for debug panel
        debugToggle.SetActive(false);
    }

    public void DebugWindowUntoggled()
    {
        //Log.Msg("Debug panel hidden");

        // Hide debug panel
        debugObj.SetActive(false);

        // Enable toggle for debug panel
        debugToggle.SetActive(true);
    }

    #endregion


    #region Unity lifecycle

    // Awake is called even if object not active
    void Awake()
    {
        // Make sure only one instance of this class exists
        if (instance != null && instance != this)
        {
            Destroy(this);
        }
        else
        {
            instance = this;
        }
    }

    // Start is called before first Update
    void Start()
    {
        // Enable workflow selector
        workflowList.SetActive(true);

        // Disable the rest by default
        qrcodePanel.SetActive(false);
        instructionView.SetActive(false);
        confirmQuitDialog.SetActive(false);
        startGuideDialog.SetActive(false);
        handmenuCanvas.SetActive(false);
        streamDialog.SetActive(false);
        wsDialog.SetActive(false);
        handmenu.SetActive(false);

        //Log.Msg($"Current locale set to: {(LocaleManager.currentLocale == LocaleManager.LocaleTypes.German ? "German" : "English")}");
        //Log.Msg($"Current handedness set to: {(HandednessManager.rightHanded ? "Right handed" : "Left handed")}");

        // Set handmenu attached side based on settings
        if (HandednessManager.rightHanded)
        {
            handmenu.GetComponent<SolverHandler>().TrackedHandedness = MixedReality.Toolkit.Handedness.Left;
        }
        else
        {
            handmenu.GetComponent<SolverHandler>().TrackedHandedness = MixedReality.Toolkit.Handedness.Right;
        }
    }

    #endregion
}

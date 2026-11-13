using GuidanceData;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using Msg;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Current step display on handmenu")]
    public GameObject hmCurrentStep;

    // Study mode
    [Tooltip("Handmenu on right hand used for switching between workflows (only in study mode)")]
    public GameObject handmenuRight;
    [Tooltip("Switch menu on instruction panel during second phase of study mode (direct comparison)")]
    public GameObject switchMenu;
    [Tooltip("Backplate materials of workflows in study mode, in presentation order (A, B, C)")]
    public Material[] setMaterials;
    [Tooltip("UI elements (backplates) colored with material of currently shown workflow in study mode")]
    public List<Graphic> setColorTargets;
    [Tooltip("Backplates of transition dialogs in study mode, colored with material of workflow shown after confirmation")]
    public List<Graphic> dialogColorTargets;

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
    [Tooltip("List of oders for study mode")]
    public GameObject orderList;
    [Tooltip("Dialog for confirming to leave guide")]
    public GameObject confirmQuitDialog;
    [Tooltip("QRCode panel")]
    public GameObject qrcodePanel;
    [Tooltip("Instruction view parent")]
    public GameObject instructionView;
    [Tooltip("Dialog to start guide")]
    public GameObject startGuideDialog;
    [Tooltip("Button to start guide in study mode")]
    public GameObject startGuideButton;
    [Tooltip("Dialog asking user whether to stream to PC or not (only optional in manual authoring)")]
    public GameObject streamDialog;
    [Tooltip("Websocket dialog for entering server ip")]
    public GameObject wsDialog;
    [Tooltip("Input field for websocket ip")]
    public MRTKTMPInputField websocketIpInput;
    [Tooltip("Dialog inbetween workflows in study mode")]
    public GameObject nextSetDialog;
    [Tooltip("Dialog after seeing all workflows in study mode. Moving on to direct comparison")]
    public GameObject nextPhaseDialog;

    [Tooltip("Directional indicator used to visually guide user towards currently most relevant UI element. Is automatically disabled after specified time")]
    public CustomDirectionalIndicator visualCue;

    #endregion


    #region Class properties

    [Tooltip("List of currently enabled toggles on handmenu")]
    private List<GameObject> enabledTogglesHandmenu;
    [Tooltip("Whether current step display on handmenu was shown before settings menu was toggled")]
    private bool hmCurrentStepWasActive;

    #endregion


    #region UI callbacks

    // User aborted guidance 
    public void ReturnToMain()
    {
        SceneManager.LoadScene(0);
    }

    // User selected workflow
    public void WorkflowSelected()
    {
        // Get currently selected workflow name
        int workflowIndex = GuidanceManager.instance.workflowToggles.CurrentIndex;
        //Log.Msg("Selected Workflow " + GuidanceManager.instance.workflowNames[workflowIndex]);

        // Use selected workflow for this session (data is loaded once qr-code is tracked)
        GuidanceManager.instance.SetSingleWorkflow(workflowIndex);

        // Update UI (skip streaming)
        workflowList.SetActive(false);
        // streamDialog.SetActive(true);
        qrcodePanel.SetActive(true);
    }

    // Researcher confirmed workflows to be evaluated in study mode
    public void StudyWorkflowsSelected()
    {
        //Log.Msg($"Selected study workflows {string.Join(", ", GuidanceManager.instance.selectedStudyWorkflowIndices)}");

        // Label orders with names of selected workflows
        GuidanceManager.instance.LabelStudyOrders();

        // Update UI
        workflowList.SetActive(false);
        orderList.SetActive(true);
    }

    // Researcher confirmed presentation order of study workflows
    public void StudyOrderSelected()
    {
        // Get currently selected order
        int orderIndex = GuidanceManager.instance.studyWorkflowOrderCollection.CurrentIndex;
        if (orderIndex < 0)
        {
            return;
        }

        // Arrange workflows in chosen order
        GuidanceManager.instance.SetStudyOrder(orderIndex);

        // Skip streaming dialog (no streaming in study mode) and scan qr
        orderList.SetActive(false);
        qrcodePanel.SetActive(true);
    }

    // Researcher started tutorial (study mode with fixed placeholder workflows for familiarization)
    public void StartTutorial()
    {
        if (!GuidanceManager.instance.SetTutorialSession())
        {
            return;
        }

        // Skip workflow and order selection and scan qr
        workflowList.SetActive(false);
        orderList.SetActive(false);
        qrcodePanel.SetActive(true);
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
    public void StartGuide()
    {
        // Update UI
        startGuideDialog.SetActive(false);
        hmNextStepButton.SetActive(true);
        hmPrevStepButton.SetActive(true);
        hmCurrentStep.SetActive(true);
        handmenu.SetActive(true);

        // Guide can only be finished once all workflows have been seen in study mode. Switching only in comparison phase
        finishGuideButton.SetActive(GuidanceManager.instance.phase != GuidancePhase.Sequential);
        switchMenu.SetActive(false);
        handmenuRight.SetActive(false);

        // Show first step
        ShowCurrentStep();
    }

    // User wants to proceed to next step
    public void ProceedToNextStep()
    {
        GuidanceManager gm = GuidanceManager.instance;

        // End of workflow in first study phase: pause instructions and show transition dialog
        if (gm.phase == GuidancePhase.Sequential && gm.IsLastStep())
        {
            gm.ClearStep();
            instructionView.SetActive(false);
            handmenu.SetActive(false);

            // Dialog takes color of workflow shown after confirmation (next workflow, or last one when comparison phase starts)
            if (gm.HasNextSet())
            {
                ApplySetMaterial(dialogColorTargets, gm.activeSet + 1);
                nextSetDialog.SetActive(true);
            }
            else
            {
                ApplySetMaterial(dialogColorTargets, gm.activeSet);
                nextPhaseDialog.SetActive(true);
            }

            return;
        }

        // Show next work step
        gm.currentStepNumber++;
        ShowCurrentStep();
    }

    // User wants to return to previous step
    public void ReturnToPrevStep()
    {
        // Show previous work step
        GuidanceManager.instance.currentStepNumber--;
        ShowCurrentStep();
    }

    // User confirmed transition to next workflow (study mode, sequential phase)
    public void ConfirmNextSet()
    {
        nextSetDialog.SetActive(false);
        handmenu.SetActive(true);

        // Start next workflow at first step
        GuidanceManager.instance.activeSet++;
        GuidanceManager.instance.currentStepNumber = 0;
        ShowCurrentStep();
    }

    // User confirmed transition to comparison phase after seeing all workflows (study mode)
    public void ConfirmNextPhase()
    {
        nextPhaseDialog.SetActive(false);
        handmenu.SetActive(true);

        // Unlock switching between workflows and finishing guide
        GuidanceManager.instance.phase = GuidancePhase.Comparison;
        switchMenu.SetActive(true);
        handmenuRight.SetActive(true);
        finishGuideButton.SetActive(true);

        // Stay at current step of last workflow and mark it on switch menus
        GuidanceManager.instance.SyncSwitches();
        ShowCurrentStep();
    }

    // User selected workflow on one of the switch menus (study mode, comparison phase). Linked to OnToggleSelected of both toggle collections
    public void SwitchSet(int setIndex)
    {
        GuidanceManager gm = GuidanceManager.instance;

        // Ignore selection of already active workflow (also triggered when syncing both switch menus)
        if (gm.phase != GuidancePhase.Comparison || setIndex == gm.activeSet)
        {
            return;
        }

        // Show same step of selected workflow
        gm.SelectSet(setIndex);
        ShowCurrentStep();
    }

    // Show current step of active workflow and guide user towards it
    private void ShowCurrentStep()
    {
        GuidanceManager.instance.ShowStep();
        instructionView.SetActive(true);

        // Color UI according to currently shown workflow (study mode only)
        if (GuidanceManager.instance.phase != GuidancePhase.Regular)
        {
            ApplySetMaterial(setColorTargets, GuidanceManager.instance.activeSet);
        }

        // Adjust navigation buttons
        GuidanceManager.instance.ConfigureNavButtons();

        // Enable directional indicator to visually guide user towards instruction
        visualCue.gameObject.SetActive(true);
        visualCue.DirectionalTarget = instructionView.transform;
    }

    // Assign backplate material of given workflow to UI elements (study mode)
    private void ApplySetMaterial(List<Graphic> targets, int setIndex)
    {
        Material setMaterial = setMaterials[setIndex];
        foreach (Graphic target in targets)
        {
            target.material = setMaterial;
        }
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

        // Also hide current step display (no button, therefore not covered above)
        hmCurrentStepWasActive = hmCurrentStep.activeSelf;
        hmCurrentStep.SetActive(false);

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

        // Restore current step display
        hmCurrentStep.SetActive(hmCurrentStepWasActive);

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

    // User finished field of view check (currently skipped, see QRGuidance)
    public void FinishFovCheck()
    {
        // Disable cheking UI
        fovCheck.SetActive(false);
        fovConfirmButton.SetActive(false);
        ShowStartGuideDialog();
    }

    // Show start dialog and load workflow data
    public async void ShowStartGuideDialog()
    {
        startGuideDialog.SetActive(true);
        handmenu.SetActive(false);

        // Load all workflow data while start dialog is shown. Guide can only be started once loading is complete
        PressableButton startButton = startGuideButton.GetComponent<PressableButton>();
        startButton.enabled = false;
        bool loaded = await GuidanceManager.instance.LoadSession();
        if (loaded)
        {
            startButton.enabled = true;
        }
        else
        {
            Log.Msg("Workflow data incomplete, guide cannot be started");
        }
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
        orderList.SetActive(false);
        qrcodePanel.SetActive(false);
        instructionView.SetActive(false);
        confirmQuitDialog.SetActive(false);
        startGuideDialog.SetActive(false);
        handmenuCanvas.SetActive(false);
        streamDialog.SetActive(false);
        wsDialog.SetActive(false);
        handmenu.SetActive(false);
        nextSetDialog.SetActive(false);
        nextPhaseDialog.SetActive(false);

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

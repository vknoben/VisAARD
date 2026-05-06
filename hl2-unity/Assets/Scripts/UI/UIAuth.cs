using MixedReality.Toolkit.SpatialManipulation;
/// <summary>
/// This class manages most stuff regarding UI and its events
/// </summary>
using MixedReality.Toolkit.UX;
using Msg;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;
using MixedReality.Toolkit;
using UnityEngine.Video;
using Unity.VisualScripting;




#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
using System;
using SerializableInstructions;
#endif

public class UIAuth : MonoBehaviour
{
    // Singleton
    public static UIAuth instance;

    #region UI elements

    // Debug panels
    [Tooltip("Superordinate debug object")]
    public GameObject debugObj;
    [Tooltip("Debug logs panel")]
    public GameObject debugLogsPanel;

    // World Space Stuff
    [Tooltip("Directional indicator used to visually guide user towards currently most relevant UI element. Is automatically disabled after specified time")]
    public CustomDirectionalIndicator visualCue;

    // Handmenu
    [Tooltip("Handmenu (either left or right)")]
    public GameObject handmenu;
    [Tooltip("Handmenu canvas which is toggled by handtracking detection")]
    public GameObject handmenuCanvas;
    [Tooltip("Toggle debug panel")]
    public GameObject debugToggle;
    [Tooltip("Button on handmenu to reinitialize qr-code tracking")]
    public GameObject retrackQrButton;
    [Tooltip("Start capturing video button on handmenu (assisted mode)")]
    public GameObject aStartCaptureButtonHandmenu;
    [Tooltip("Start capturing video button on handmenu (manual mode)")]
    public GameObject mStartCaptureButtonHandmenu;
    [Tooltip("Button to complete fov check")]
    public GameObject fovConfirmButton;

    // General UI elements, dialogs, and pop ups
    [Tooltip("Dialog for selecting workflow editing (create or edit)")]
    public GameObject workflowDialog;
    [Tooltip("Dialog for user to input name of new workflow")]
    public GameObject workflowNameDialog;
    [Tooltip("Input field for naming new workflow")]
    public MRTKTMPInputField workflowNameInput;
    [Tooltip("Input field for websocket ip")]
    public MRTKTMPInputField websocketIpInput;
    [Tooltip("Dialog asking user whether to stream to PC or not (only optional in manual authoring)")]
    public GameObject streamDialog;
    [Tooltip("Dialog asking user whether this is a user study (with preview demonstration videos) or regular authoring (no demo videos)")]
    public GameObject userstudyDialog;
    [Tooltip("Start button on start control loop dialog")]
    public PressableButton startControlLoopButton;
    [Tooltip("Dialog shown to user before authoring process starts")]
    public GameObject startAuthoringDialog;
    [Tooltip("QRCode panel instructing to scan code")]
    public GameObject qrcodePanel;
    [Tooltip("Websocket dialog for entering server ip")]
    public GameObject wsDialog;
    [Tooltip("Far ray interaction (left)")]
    public GameObject farRayLeft;
    [Tooltip("Far ray interaction (right)")]
    public GameObject farRayRight;
    [Tooltip("Dialog to select specific step to start (default 1)")]
    public GameObject startStepDialog;

    // Countdown visualizer for video capture (assisted and manual)
    [Tooltip("Countdown panel shown at static position in front of user after pressing start demo recording")]
    public GameObject countdownPanel;
    [Tooltip("Count down text")]
    public TMP_Text countdownText;
    [Tooltip("Fov Indicator parent object")]
    public GameObject fovIndicators;
    [Tooltip("List of capture center visualizer elements")]
    public List<RawImage> captureVisualizers;
    [Tooltip("Center visualizer when capturing")]
    public GameObject centerVisualizer;
    [Tooltip("Instruction for capturing to keep scene within fov bounds")]
    public GameObject captureInstruction;
    // Fov check instruction
    [Tooltip("Instruction text displayed to user when checking fov and correct adjustment of HL2")]
    public GameObject fovCheck;

    // Assisted authoring (a)
    // In-situ review
    [Tooltip("Preview of captured video in assisted authoring mode")]
    public GameObject aStepPreview;
    [Tooltip("Label for displaying currently authored step on step preview")]
    public TMP_Text aStepNumberOnPreview;
    [Tooltip("Confirmation dialog for finishing authoring of in-situ instructions in assisted authoring mode")]
    public GameObject aConfirmInsituStepDialog;
    [Tooltip("Dialog inbetween finishing text and video instructions and human-in-the-loop in-situ authoring")]
    public GameObject aStartInsituDialog;
    [Tooltip("Nearmenu for editing text and in-situ instructions")]
    public GameObject aAuthNearMenu;
    [Tooltip("Button to move on to next step during in-situ review authoring")]
    public PressableButton aNextStepInsituButton;
    [Tooltip("Button to regenerate instructions")]
    public PressableButton aRegenInstructsButton;
    [Tooltip("Button to recapture demonstration (during review)")]
    public PressableButton aRecaptureDemoButton;
    [Tooltip("Confirmation dialog for recapturing demonstration during in-situ review authoring")]
    public GameObject aConfirmRedoDialog;

    // Video
    [Tooltip("Main video authoring panel used in assisted authoring mode to author video for each step")]
    public GameObject aVideoAuthPanel;
    [Tooltip("Button to move on to authoring next step in assisted authoring mode")]
    public GameObject aNextStepButton;
    [Tooltip("Button to complete manual authoring part (videos) in assisted authoring mode. This was the last step of the workflow")]
    public GameObject aLastStepButton;
    [Tooltip("Button to (re-)capture video in assisted authoring mode")]
    public GameObject aCaptureButton;
    [Tooltip("Button to confirm redo of demonstration video in assisted authoring mode during review")]
    public GameObject aConfirmRedoneVideoButton;
    [Tooltip("Play button for authored video in assisted authoring mode")]
    public GameObject aPlayVideoButton;
    [Tooltip("Label for currently authored step number on assisted video authoring panel")]
    public TMP_Text aStepNumberLabel;
    [Tooltip("Confirm dialog for moving on to authoring next step in assisted authoring mode")]
    public GameObject aConfirmNextStepDialog;
    [Tooltip("Confirm dialog for finishing manual authoring part (videos) in assisted authoring mode")]
    public GameObject aConfirmLastStepDialog;
    [Tooltip("Confirm recapture dialog in assisted authoring mode")]
    public GameObject aConfirmRecaptureDialog;
    [Tooltip("Dialog for confirming redone demonstration video in assisted authoring mode during review")]
    public GameObject aConfirmRedoneVideoDialog;

    // Manual authoring (m)
    [Tooltip("Preview of captured video in manual authoring mode")]
    public GameObject mStepPreview;
    [Tooltip("Label for displaying currently authored step on step preview")]
    public TMP_Text mStepNumberOnPreview;
    [Tooltip("Confirmation dialog for finishing authoring of single step")]
    public GameObject mConfirmFinishStepDialog;
    [Tooltip("Confirmation dialog for finishing authoring of whole workflow")]
    public GameObject mConfirmFinishWorkflowDialog;
    [Tooltip("Nearmenu where user selects which modality to author next (text, video, in-situ)")]
    public GameObject mAuthNearMenu;
    [Tooltip("Button to finish authoring step (enabled when all modalities authored)")]
    public PressableButton mFinishStepButton;
    [Tooltip("Button to finish authoring workflow (enabled when all modalities authored)")]
    public PressableButton mFinishWorkflowButton;
    // Text
    [Tooltip("Authoring panel for text in manual authoring mode")]
    public GameObject mTextAuthPanel;
    [Tooltip("Dialog for confirming authored text in manual authoring mode")]
    public GameObject mConfirmTextDialog;
    // Video
    [Tooltip("Window shown to user when authoring video instruction")]
    public GameObject mVideoAuthPanel;
    [Tooltip("Dialog for confirming recapture of video")]
    public GameObject mConfirmRecaptureVideoDialog;
    [Tooltip("Dialog for confirming finish of authoring video instruction")]
    public GameObject mConfirmVideoDialog;
    [Tooltip("Play button for authored video in manual authoring mode")]
    public GameObject mPlayVideoButton;
    [Tooltip("Button to finish authoring video in manual authoring mode")]
    public PressableButton mFinishAuthVideoButton;
    // 3D
    [Tooltip("Main panel for manual 3D authoring")]
    public GameObject mAuth3dPanel;
    [Tooltip("Dialog for confirming finish of authoring 3D instruction")]
    public GameObject mConfirm3dDialog;
    [Tooltip("Dialog for confirming deletion of authored 3D instruction")]
    public GameObject mConfirmDelete3dDialog;

    #endregion


    #region Class properties

    [Tooltip("Countdown for video capture")]
    public float countdownVideo = 5;

    [Tooltip("List of currently enabled toggles on handmenu")]
    private List<GameObject> enabledTogglesHandmenu;

    [Tooltip("List of demonstration videos used to instruct study participants during authoring")]
    public List<VideoClip> demoVideos;
    [Tooltip("Specified step to start at")]
    public int startStep = 1;

    [Tooltip("Whether currently waiting for result of redo or not")]
    public bool waitingForRedoResult = false;

    [Tooltip("Flag whether this is a user study with preview demonstrations or free authoring")]
    public bool isUserStudy = false;

    #endregion


    #region Handmenu callbacks

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
        retrackQrButton.SetActive(QRAuth.instance.arMarkerManager.didStart);
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

    // User finished setting up authoring environment (placement of instructions for authoring)
    public void FinishAuthSetup()
    {
        // Save current authoring panel poses relative to qr-code for use during authoring
        QRAuth.instance.SaveAuthoringPanelPoses();

        // Distinguish between assisted and manual authoring mode
        if (WebSocketClient.instance.assistedAuthMode)
        {
            aVideoAuthPanel.SetActive(false);

            // Move to spatial setup of review panel
            aStepPreview.SetActive(true);
        }
        else
        {
            mTextAuthPanel.SetActive(false);
            mVideoAuthPanel.SetActive(false);
            mAuth3dPanel.SetActive(false);

            // Move to spatial setup of review panel
            mStepPreview.SetActive(true);
        }
    }

    // User finished setting up guidance environment (placement of instructions for guidance)
    public void FinishReviewSetup()
    {
        // Save current previa panel poses relative to qr-code for use during guidance
        QRAuth.instance.SaveReviewPanelPoses();

        if (WebSocketClient.instance.assistedAuthMode)
        {
            aStepPreview.SetActive(false);
        }
        else
        {
            mStepPreview.SetActive(false);
        }

        // Update UI (in any case)
        userstudyDialog.SetActive(true);
    }

    // User finished field of view check
    public void FinishFovCheck()
    {
        // Update UI
        fovCheck.SetActive(false);
        fovIndicators.SetActive(false);
        fovConfirmButton.SetActive(false);
        startAuthoringDialog.SetActive(true);
    }

    #endregion


    #region UI callbacks before actual authoring begins (independent of authoring mode)

    // User chose manual authoring mode
    public void SelectedManualAuthoring()
    {
        // Set flag
        WebSocketClient.instance.assistedAuthMode = false;

        // Inform websocket if already connected
        if (WebSocketClient.instance.connected)
        {
            // With this corrected line:
            WebSocketClient.instance.SendMsg(MsgType.MODE, "manual");
        }

        // Update UI
        workflowDialog.SetActive(false);
        streamDialog.SetActive(true);
    }

    // User chose assisted authoring mode
    public void SelectedAssistedAuthoring()
    {
        // Set flag
        WebSocketClient.instance.assistedAuthMode = true;

        // Inform websocket if already connected
        if (WebSocketClient.instance.connected)
        {
            // With this corrected line:
            WebSocketClient.instance.SendMsg(MsgType.MODE, "assisted");
        }
        // Update UI
        workflowDialog.SetActive(false);
        wsDialog.SetActive(true);
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
        // Skip websocket connection dialog and go to workflow naming
        streamDialog.SetActive(false);
        workflowNameDialog.SetActive(true);
    }

    // This is a user study (preview demonstration videos)
    public void UserStudyModeOn()
    {
        isUserStudy = true;

        // Configure relevant UI elements
        mFinishWorkflowButton.transform.parent.gameObject.SetActive(false);
        aLastStepButton.transform.parent.gameObject.SetActive(false);

        // Update UI
        userstudyDialog.SetActive(false);
        startStepDialog.SetActive(true);
    }

    // This is not a user study (regular authoring, no preview demonstration videos)
    public void UserStudyModeOff()
    {
        isUserStudy = false;

        // Configure relevant UI elements
        mFinishWorkflowButton.transform.parent.gameObject.SetActive(true);
        aLastStepButton.transform.parent.gameObject.SetActive(true);

        // Update UI
        userstudyDialog.SetActive(false);
        fovCheck.SetActive(true);
        fovIndicators.SetActive(true);
        fovConfirmButton.SetActive(true);
    }

    // User chose to go back to main menu (authoring, guidance selection)
    public void ReturnToMain()
    {
        SceneManager.LoadScene("Main");
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

    // User confirmed name for new workflow
    public void ConfirmWorkflowName()
    {
        // Read user input
        string workflowName = workflowNameInput.text;

        // Create folder based on input name for workflow to be stored
        WorkflowManager.instance.CreateWorkflowFolder(workflowName);

        // Update UI
        workflowNameDialog.SetActive(false);
        qrcodePanel.SetActive(true);
        //QRAuth.instance.InitializeQRCodeTracking();
    }

    // Set specific step to start at
    public void SelectedStartStep(int selectedToggleIndex)
    {
        // Set start step
        startStep = selectedToggleIndex + 1;
    }

    // Confirm step to start at
    public void ConfirmStartStep()
    {
        // Adjust list of demo videos to start at specified step
        demoVideos.RemoveRange(0, startStep - 1);
        startStep = 1; // Reset

        // If only 1 step left, show last step button in assisted authoring mode right away
        if (demoVideos.Count == 1)
        {
            // Assisted
            aNextStepButton.SetActive(false);
            aLastStepButton.transform.parent.gameObject.SetActive(true);

            // Manual
            mFinishStepButton.transform.parent.gameObject.SetActive(false);
            mFinishWorkflowButton.transform.parent.gameObject.SetActive(true);
        }

        // Update UI
        startStepDialog.SetActive(false);
        fovCheck.SetActive(true);
        fovIndicators.SetActive(true);
        fovConfirmButton.SetActive(true);
    }

    // User started authoring workflow
    public void StartAuthoring()
    {
        // Update UI
        startAuthoringDialog.SetActive(false);

        // Inform client about start of authoring process
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHSTART, "");
        }

        // Differentiate between assisted and manual authoring mode
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // We're in assisted mode

            // Populate assisted video capture panel with demo video
            if (isUserStudy)
            {
                AuthorVideo.instance.PopulateDemoVideo(demoVideos[0], AuthorVideo.instance.aVideoPlayer);
            }

            // Show video authoring panel
            aVideoAuthPanel.SetActive(true);
            aPlayVideoButton.SetActive(false);
            handmenu.SetActive(true);
            //aStartCaptureButtonHandmenu.SetActive(true);

            // Update step number on main authoring panel
            aStepNumberLabel.text = $"{WorkflowManager.instance.stepNumber}";

            // Enable face user constraint for video authoring panel
            //var faceUserComp = aVideoAuthPanel.GetComponent<MakeObjFaceUser>();
            //faceUserComp.enabled = true;
            //faceUserComp.FaceUser();

            // Enable panel arrangement events
            var comp = aStepPreview.GetComponentInChildren<DidManipulate>();
            comp.enabled = true;
            comp.didArrange = false;

            // Link directional indicator to next element
            visualCue.DirectionalTarget = aVideoAuthPanel.transform;
        }
        else
        {
            // We're in manual authoring mode

            // Populate manual video capture panel with demo video
            if (isUserStudy)
            {
                AuthorVideo.instance.PopulateDemoVideo(demoVideos[0], AuthorVideo.instance.mVideoPlayerReview);
            }

            // Update UI
            mAuthNearMenu.SetActive(true);
            mStepPreview.SetActive(true);

            // Update step number on main authoring panel
            mStepNumberOnPreview.text = $"{WorkflowManager.instance.stepNumber}";

            // Enable panel arrangement events
            var comp = mStepPreview.GetComponentInChildren<DidManipulate>();
            comp.enabled = true;
            comp.didArrange = false;

            // Link directional indicator to next element
            visualCue.DirectionalTarget = mStepPreview.transform;
        }
    }

    #endregion


    #region Callbacks for both assisted and manual authoring mode

    // User requested stop of video capture
    public async void StopVideoCaptureRequested()
    {
        // Stop capture
        if (AuthorVideo.instance.isCapturing)
        {
            // Stop capturing
            HandtrackingManager.instance.StopCapturingHands();
            await AuthorVideo.instance.StopCapturingVideo();
        }
    }

    // Reestablish UI for video capturing. Is called after video trimmed and data loaded into preview
    public void ResetCaptureUI()
    {
        // Differentiate between assisted and manual authoring mode
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Enable visual indicator towards authoring panel again
            visualCue.DirectionalTarget = aVideoAuthPanel.transform;

            // Update UI
            aVideoAuthPanel.SetActive(true);
            //aStartCaptureButtonHandmenu.SetActive(true);
            //aPlayVideoButton.SetActive(true);
        }
        else
        {
            // Enable visual indicator towards authoring panel/authored instruction
            visualCue.DirectionalTarget = mVideoAuthPanel.transform;

            // Update UI
            mVideoAuthPanel.SetActive(true);
            //mStartCaptureButtonHandmenu.SetActive(true);
            //mPlayVideoButton.SetActive(true);
        }

        // Relevant in both cases
        // Increase opacity of capture visualizers again
        foreach (var vis in captureVisualizers)
        {
            vis.color = new Color(1f, 1f, 1f, 1f);
        }

        fovIndicators.SetActive(false);
        centerVisualizer.SetActive(false);
        handmenu.SetActive(true);

        // Enable far interaction again
        farRayRight.SetActive(true);
        farRayLeft.SetActive(true);
    }

    // Check if all modalities have been authored
    public void CheckAuthoredModalities()
    {
        if (AuthorText.instance.textAuthored && AuthorVideo.instance.videoAuthored && Author3d.instance._3dAuthored)
        {
            // Enable buttons
            mFinishStepButton.enabled = true;
            mFinishWorkflowButton.enabled = true;
        }
        else
        {
            // Disable buttons
            mFinishStepButton.enabled = false;
            mFinishWorkflowButton.enabled = false;
        }
    }

    #endregion


    #region Callbacks in manual authoring mode

    #region General

    // User requests to finish authoring this step
    public void ManualFinishStep()
    {
        mConfirmFinishStepDialog.SetActive(true);
        mConfirmFinishWorkflowDialog.SetActive(false);
    }

    // User cancels step authoring finished
    public void ManualCancelFinishStep()
    {
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(false);
    }

    // User confirms step authoring finished (only relevant for manual authoring mode)
    public async void ManualConfirmFinishStep()
    {
        // Update UI
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(false);
        mAuthNearMenu.SetActive(true);
        mStepPreview.SetActive(false);
        mFinishStepButton.enabled = false;
        mFinishWorkflowButton.enabled = false;

        // Save instruction panel pose relative to qr-code
        await SaveInstructionPanelPose();

#if ENABLE_WINMD_SUPPORT
        // Prepare folder structure for next step
        await WorkflowManager.instance.PrepareFolderForNextStep();
#endif

        // Update step number on main authoring panel
        mStepNumberOnPreview.text = $"{WorkflowManager.instance.stepNumber}";

        // Reset text, video, handtracking, 3d for next step
        AuthorText.instance.ResetTextAuth();
        AuthorVideo.instance.ResetVideoAuthManual();
        Author3d.instance.Reset3dAuth();

        // Stop any ongoing playback
        AuthorVideo.instance.ResetOngoingPlaybackManual(AuthorVideo.instance.mVideoPlayerReview);

        // Populate manual video capture panel with demo video
        if (isUserStudy)
        {
            AuthorVideo.instance.PopulateDemoVideo(demoVideos[WorkflowManager.instance.stepNumber - 1], AuthorVideo.instance.mVideoPlayerReview);

            // Enable complete done button if this is last step to author
            if (WorkflowManager.instance.stepNumber == demoVideos.Count)
            {
                mFinishStepButton.transform.parent.gameObject.SetActive(false);
                mFinishWorkflowButton.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                mFinishStepButton.transform.parent.gameObject.SetActive(true);
                mFinishWorkflowButton.transform.parent.gameObject.SetActive(false);
            }
        }

        // Enable panel arrangement events
        var comp = mStepPreview.GetComponentInChildren<DidManipulate>();
        comp.enabled = true;
        comp.didArrange = false;

        // Reenable step preview again
        mStepPreview.SetActive(true);
    }

    // User chose to complete authoring of workflow
    public void ManualFinishWorkflow()
    {
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(true);
    }

    // User does not want to complete authoring
    public void ManualCancelFinishWorkflow()
    {
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(false);
    }

    // User wants to complete authoring
    public async void ManualConfirmFinishWorkflow()
    {
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(false);

        // Save instruction panel pose to disk
        await SaveInstructionPanelPose();

        // Inform pc about authoring complete
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHFINISH, "");
        }

        // Finish authoring of this workflow
        WorkflowManager.instance.FinishAuthoringWorkflow();

        // Switch back to main scene
        SceneManager.LoadScene("Main");
    }

    // Save instruction panel pose for current step relative to qr-code (manual authoring)
    public async Task SaveInstructionPanelPose()
    {
#if ENABLE_WINMD_SUPPORT
        // Save instruction panel pose to disk
        SerializableInstructionPanelPose serializableInstructionPanelPose = new SerializableInstructionPanelPose(mStepPreview.transform);

        // Create json from serializable instruction panel pose
        string json = JsonUtility.ToJson(serializableInstructionPanelPose, true);
        StorageFile file = await WorkflowManager.instance.currentStepFolder.CreateFileAsync("panel.json", CreationCollisionOption.ReplaceExisting);

        // Write data to file
        await FileIO.WriteTextAsync(file, json);

        //Log.Msg($"Saved instruction panel pose for step {WorkflowManager.instance.stepNumber}");
#endif
    }

    #endregion

    #region Text

    // User wants to author textual instruction
    public void ManualAuthorTextSelected()
    {
        // Stop any ongoing playback and reset view
        AuthorVideo.instance.ResetOngoingPlaybackManual(AuthorVideo.instance.mVideoPlayerReview);

        // Update UI
        mAuthNearMenu.SetActive(false);
        mTextAuthPanel.SetActive(true);
        mStepPreview.SetActive(false);

        // Disable far rays
        //farRayLeft.SetActive(false);
        //farRayRight.SetActive(false);

        // Hide potentially authored 3D instructions
        Author3d.instance.authoredObjs.ForEach(obj => obj.SetActive(false));

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Set visual cue to textual authoring panel
        visualCue.DirectionalTarget = mTextAuthPanel.transform;

        // Inform client that user chose to author text instruction
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODAL, "text");
        }

        // Start checking for text input by physical keyboard
        AuthorText.instance.readKeyboardRoutine = StartCoroutine(AuthorText.instance.ManualCheckForUserInput());
    }

    // User requests finish of authoring textual instruction
    public void FinishAuthText()
    {
        mConfirmTextDialog.SetActive(true);
    }

    // User cancels finish of authoring textual instruction
    public void CancelFinishAuthText()
    {
        mConfirmTextDialog.SetActive(false);
    }

    // User confirms finish of authoring textual instruction
    public async void ConfirmFinishAuthText()
    {
        // Update UI
        mConfirmTextDialog.SetActive(false);
        mTextAuthPanel.SetActive(false);
        mStepPreview.SetActive(true);
        mAuthNearMenu.SetActive(true);
        visualCue.DirectionalTarget = mStepPreview.transform;

        // Enable far rays
        //farRayLeft.SetActive(true);
        //farRayRight.SetActive(true);

        // Show potentially authored 3D instructions again
        Author3d.instance.authoredObjs.ForEach(obj => obj.SetActive(true));

        // Show QR-Code visuals
        QRAuth.instance.markerContent.SetActive(true);

        // Transfer authored text to instruction preview
        AuthorText.instance.ManualPopulatePreview();

        // Save textual instruction
        await AuthorText.instance.SaveInstructionText();

        // Check if all modalities have been authored
        CheckAuthoredModalities();

        // Inform client that user exited authoring modality
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODALFINISH, string.Empty);
        }

        // Stop monitoring physical keyboard input again
        StopCoroutine(AuthorText.instance.readKeyboardRoutine);
    }

    #endregion

    #region Video

    // User wants to author video instruction
    public void ManualAuthorVideoSelected()
    {
        // Stop any ongoing playback and reset view
        AuthorVideo.instance.ResetOngoingPlaybackManual(AuthorVideo.instance.mVideoPlayerReview);

        // Update UI
        mAuthNearMenu.SetActive(false);
        //mStartCaptureButtonHandmenu.SetActive(true);
        mVideoAuthPanel.SetActive(true);
        mStepPreview.SetActive(false);

        // Hide potentially authored 3D instructions
        Author3d.instance.authoredObjs.ForEach(obj => obj.SetActive(false));

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Set visual cue to video authoring panel
        visualCue.DirectionalTarget = mVideoAuthPanel.transform;

        // Inform client that user chose to author video instruction
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODAL, "video");
        }
    }

    // User requested start of video capture
    public async void ManualCaptureVideoPressed()
    {
        // Confirm with user if recording for this step already exists
        if (AuthorVideo.instance.vidPath != null && AuthorVideo.instance.vidPath != string.Empty)
        {
            mConfirmRecaptureVideoDialog.SetActive(true);
            mConfirmVideoDialog.SetActive(false);

            return;
        }

        // Update UI
        mVideoAuthPanel.SetActive(false);
        handmenu.SetActive(false);
        visualCue.DirectionalTarget = null;

        // Initialize capturing procedure
        await AuthorVideo.instance.InitializeMediaCaptureVideo();
        HandtrackingManager.instance.InitializeHandRecording();

        // Start countdown
        StartCoroutine(CountDown(countdownVideo));
    }

    // User did not want to overwrite existing video instruction (Reject)
    public void ManualCancelNewVideo()
    {
        mConfirmRecaptureVideoDialog.SetActive(false);
        mConfirmVideoDialog.SetActive(false);
    }

    // User wants to overwrite existing video instruction (Accept)
    public async void ManualConfirmNewVideo()
    {
        // Update UI
        mConfirmRecaptureVideoDialog.SetActive(false);
        mConfirmVideoDialog.SetActive(false);
        //mStartCaptureButtonHandmenu.SetActive(false);
        visualCue.DirectionalTarget = null;
        mVideoAuthPanel.SetActive(false);
        handmenu.SetActive(false);

        // Stop any ongoing video playback
        AuthorVideo.instance.ResetOngoingPlaybackManual(AuthorVideo.instance.mVideoPlayer);

        // Initialize capturing procedure
        await AuthorVideo.instance.InitializeMediaCaptureVideo();
        HandtrackingManager.instance.InitializeHandRecording();

        // Start countdown
        StartCoroutine(CountDown(countdownVideo));
    }

    // User requests finish of authoring video instruction
    public void ManualFinishAuthVideo()
    {
        // Confirm with user
        mConfirmVideoDialog.SetActive(true);
        mConfirmRecaptureVideoDialog.SetActive(false);
    }

    // User cancels finish of authoring video instruction
    public void ManualCancelFinishAuthVideo()
    {
        mConfirmVideoDialog.SetActive(false);
        mConfirmRecaptureVideoDialog.SetActive(false);
    }

    // User confirms finish of authoring video instruction
    public async void ManualConfirmFinishAuthVideo()
    {
        // Update UI
        mConfirmVideoDialog.SetActive(false);
        mConfirmRecaptureVideoDialog.SetActive(false);
        mVideoAuthPanel.SetActive(false);
        //mStartCaptureButtonHandmenu.SetActive(false);
        mStepPreview.SetActive(true);
        mAuthNearMenu.SetActive(true);

        // Show potentially authored 3D instructions again
        Author3d.instance.authoredObjs.ForEach(obj => obj.SetActive(true));

        // Enable QR-Code visuals
        QRAuth.instance.markerContent.SetActive(true);

        // Display authored video to user in preview
        AuthorVideo.instance.PopulateVideoPreview(AuthorVideo.instance.trimmedVidPath ?? AuthorVideo.instance.vidPath, AuthorVideo.instance.mVideoPlayerReview);

        // Store handtracking for this step
        await HandtrackingManager.instance.WriteHandtrackingToDisk();

        // Link directional indicator to none
        visualCue.DirectionalTarget = mStepPreview.transform;

        // Check if all modalities have been authored
        CheckAuthoredModalities();

        // Inform client that user exited authoring modality
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODALFINISH, string.Empty);
        }
    }

    #endregion

    #region 3D

    // User wants to author 3D instruction
    public void ManualAuthor3dSelected()
    {
        // Stop any ongoing playback and reset view
        AuthorVideo.instance.ResetOngoingPlaybackManual(AuthorVideo.instance.mVideoPlayerReview);

        // Update UI
        mAuthNearMenu.SetActive(false);
        mAuth3dPanel.SetActive(true);

        // Hide preview of authored steps
        mStepPreview.SetActive(false);

        // Set visual cue to 3D authoring panel
        visualCue.DirectionalTarget = mAuth3dPanel.transform;

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Initialize 3D authoring
        Author3d.instance.Initialize3dAuthoring();

        // Inform client that user chose to author 3D instruction
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODAL, "3d");
        }
    }

    // User requests finish of authoring 3D instruction
    public void ManualFinishAuth3d()
    {
        mConfirm3dDialog.SetActive(true);
    }

    // User cancels finish of authoring 3D instruction
    public void ManualCancelFinishAuth3d()
    {
        mConfirm3dDialog.SetActive(false);
    }

    // User confirms finish of authoring 3D instruction
    public async void ManualConfirmFinishAuth3d()
    {
        // Update UI
        mConfirm3dDialog.SetActive(false);
        mAuth3dPanel.SetActive(false);
        mAuthNearMenu.SetActive(true);
        mStepPreview.SetActive(true);

        // Enable QR-Code visuals
        QRAuth.instance.markerContent.SetActive(true);
        //Log.Msg("Reenabled qr-code visuals");

        // Make all previously authored 3D non-manipulable at all for now
        foreach (var obj in Author3d.instance.authoredObjs)
        {
            var interactableComps = obj.GetComponents<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = false;
                }
            }
        }
        //Log.Msg("Made authored 3D objects non-manipulable");

        // Save 3D instructions
        await Author3d.instance.Save3dInstructions();

        // Point to step preview again
        visualCue.DirectionalTarget = mStepPreview.transform;

        // Check if all modalities have been authored
        CheckAuthoredModalities();

        // Inform client that user exited authoring modality
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.AUTHMODALFINISH, string.Empty);
        }
    }

    // User confirms deletion of selected 3D instruction
    public void ManualConfirmDelete3d()
    {
        // Call deletion logic
        Author3d.instance.Delete3dInstruction();

        mConfirmDelete3dDialog.SetActive(false);
    }

    // User wants to manipulate not delete
    public void ManualConfirmManipulate3d()
    {
        // Call manipulation logic
        Author3d.instance.Make3dManipulable();

        mConfirmDelete3dDialog.SetActive(false);
    }

    // User cancels deletion of selected 3D instruction
    public void ManualCancelDelete3d()
    {
        mConfirmDelete3dDialog.SetActive(false);
    }

    #endregion

    #endregion


    #region Callbacks in assisted authoring mode

    #region General Workflow

    // User requests to move on to authoring next step
    public void AssistedAuthNextStep()
    {
        aConfirmNextStepDialog.SetActive(true);
        aConfirmLastStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
    }

    // User canceled moving on to authoring next step
    public void AssistedCancelAuthNextStep()
    {
        aConfirmNextStepDialog.SetActive(false);
        aConfirmLastStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
    }

    // User confirms moving on to authoring next step
    public async void AssistedConfirmAuthNextStep()
    {
        // Update UI
        aConfirmNextStepDialog.SetActive(false);
        aConfirmLastStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
        aVideoAuthPanel.SetActive(false);

        // Enable QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Inform client that video captured was finalized. In assisted mode: Processing of frames by VLM can begin
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.VIDEOCONFIRMED, "");

            //Log.Msg("Informed client that video was confirmed");
        }

        // Cache video path for this step (needed for loading in same session during in-situ authoring)
        InstructionManager.Instance.VideoPath = AuthorVideo.instance.trimmedVidPath ?? AuthorVideo.instance.vidPath;
        // Cache handtracking data for this step
        InstructionManager.Instance.HandTrackingData = HandtrackingManager.instance.handtrackingDataset;

        // Store handtracking for this step (needed for loading in separate session during guidance)
        await HandtrackingManager.instance.WriteHandtrackingToDisk();
        //Log.Msg("Stored handtracking to disk");

        // Prepare authoring for next step
        await WorkflowManager.instance.PrepareFolderForNextStep();

        // Update step number on main authoring panel
        aStepNumberLabel.text = $"{WorkflowManager.instance.stepNumber}";

        // Reset video and handtracking data
        AuthorVideo.instance.ResetVideoAuthAssisted();

        // Populate assisted video capture panel with demo video
        if (isUserStudy)
        {
            AuthorVideo.instance.PopulateDemoVideo(demoVideos[WorkflowManager.instance.stepNumber - 1], AuthorVideo.instance.aVideoPlayer);

            // Enable complete done button onlyy if this is last step to author
            if (WorkflowManager.instance.stepNumber == demoVideos.Count)
            {
                aNextStepButton.SetActive(false);
                aLastStepButton.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                aNextStepButton.SetActive(true);
                aLastStepButton.transform.parent.gameObject.SetActive(false);
            }
        }

        // Reenable video authoring panel
        aVideoAuthPanel.SetActive(true);
    }

    // User requests to finish authoring last step
    public void AssistedAuthLastStep()
    {
        // Update UI
        aConfirmLastStepDialog.SetActive(true);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
    }

    // User canceled finishing authoring last step
    public void AssistedCancelAuthLastStep()
    {
        // Update UI
        aConfirmLastStepDialog.SetActive(false);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
    }

    // User confirms finishing authoring last step
    public async void AssistedConfirmAuthLastStep()
    {
        // Update UI
        aConfirmLastStepDialog.SetActive(false);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);

        aVideoAuthPanel.SetActive(false);
        //aStartCaptureButtonHandmenu.SetActive(false);
        aStartInsituDialog.SetActive(true);

        // Inform client that video captured was finalized. Processing of frames by VLM can begin
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.VIDEOCONFIRMED, "");

            //Log.Msg("Informed client that video was confirmed");
        }

        // Cache video path for this step
        InstructionManager.Instance.VideoPath = AuthorVideo.instance.trimmedVidPath ?? AuthorVideo.instance.vidPath;
        // Cache handtracking data for this step
        InstructionManager.Instance.HandTrackingData = HandtrackingManager.instance.handtrackingDataset;

        // Store handtracking for this step
        await HandtrackingManager.instance.WriteHandtrackingToDisk();
        //Log.Msg("Stored handtracking to disk");

        // Save current step number as final total steps count
        WorkflowManager.instance.SaveStepCount();
    }

    #endregion

    #region Video

    // User requested start of video capture
    public async void AssistedCaptureVideoPressed()
    {
        // Confirm with user if recording for this step already exists
        if (AuthorVideo.instance.vidPath != null && AuthorVideo.instance.vidPath != string.Empty)
        {
            // Recapture requested
            aConfirmRecaptureDialog.SetActive(true);
            aConfirmNextStepDialog.SetActive(false);
            aConfirmLastStepDialog.SetActive(false);

            return;
        }

        // Initial capture requested
        // Initialize capturing procedure
        await AuthorVideo.instance.InitializeMediaCaptureVideo();
        HandtrackingManager.instance.InitializeHandRecording();

        // Update UI
        aVideoAuthPanel.SetActive(false);
        handmenu.SetActive(false);
        visualCue.DirectionalTarget = null;

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Start countdown
        StartCoroutine(CountDown(countdownVideo));
    }

    // User wants to overwrite existing video instruction (Accept)
    public async void AssistedConfirmNewVideo()
    {
        // Update UI
        aConfirmRecaptureDialog.SetActive(false);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmLastStepDialog.SetActive(false);
        aVideoAuthPanel.SetActive(false);
        visualCue.DirectionalTarget = null;

        // Stop any ongoing video playback
        AuthorVideo.instance.ResetOngoingPlaybackAssisted(AuthorVideo.instance.aVideoPlayer);

        // Initialize capturing procedure
        await AuthorVideo.instance.InitializeMediaCaptureVideo();
        HandtrackingManager.instance.InitializeHandRecording();

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Start countdown
        StartCoroutine(CountDown(countdownVideo));
    }

    // User did not want to overwrite existing video instruction (Reject)
    public void AssistedCancelNewVideo()
    {
        // Update UI
        aConfirmRecaptureDialog.SetActive(false);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmLastStepDialog.SetActive(false);
    }

    // User wants to submit redone video for regeneration
    public void AssistedRedoneVideo()
    {
        aConfirmRedoneVideoDialog.SetActive(true);
    }

    // User confirms redo of video for regeneration
    public async void AssistedConfirmRedoneVideo()
    {
        // Inform client that video captured was finalized. Processing of frames by VLM can begin
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.VIDEOCONFIRMED, "");

            //Log.Msg("Informed client that video was confirmed");
        }

        // Update UI
        aConfirmRedoneVideoDialog.SetActive(false);
        aVideoAuthPanel.SetActive(false);
        aNextStepButton.SetActive(true);
        aLastStepButton.transform.parent.gameObject.SetActive(true);
        aConfirmRedoneVideoButton.SetActive(false);
        aStepPreview.SetActive(true);

        // Cache video path for this step
        InstructionManager.Instance.currentInstructionSet.videoInstructionPath = AuthorVideo.instance.trimmedVidPath ?? AuthorVideo.instance.vidPath;
        // Cache handtracking data for this step
        InstructionManager.Instance.currentInstructionSet.handtrackingData = HandtrackingManager.instance.handtrackingDataset;

        // Load newly captured video into preview
        AuthorVideo.instance.PopulateVideoPreview(InstructionManager.Instance.currentInstructionSet.videoInstructionPath, AuthorVideo.instance.aVideoPlayerReview);

        // Store handtracking for this step
        await HandtrackingManager.instance.WriteHandtrackingToDisk(InstructionManager.Instance.currentInstructionSet.stepNumber);
        //Log.Msg("Stored handtracking to disk");

        // Flag as waiting for redo result
        waitingForRedoResult = true;
    }

    // User cancels redo of video for regeneration
    public void AssistedCancelRedoneVideo()
    {
        aConfirmRedoneVideoDialog.SetActive(false);
    }

    #endregion

    #region 3D

    // User started human-in-the-loop in-situ authoring
    public void AssistedStartInsituAuthoring()
    {
        // Update UI
        aStartInsituDialog.SetActive(false);
        aStepPreview.SetActive(true);
        //aAuthNearMenu.SetActive(true);

        // Enable face user constraint for step preview panel
        var faceUserComp = aStepPreview.GetComponent<MakeObjFaceUser>();
        faceUserComp.enabled = true;

        // Enable QR-Code visuals
        QRAuth.instance.markerContent.SetActive(true);

        // Start control loop by loading instructions for first step
        AssistedLoadFirstStep();
    }

    // User requests to finish in-situ authoring
    public void AssistedFinishAuthInsitu()
    {
        // Confirm with user
        aConfirmInsituStepDialog.SetActive(true);
    }

    // User confirms finishing in-situ authoring for single step
    public async void AssistedConfirmFinishAuthInsitu()
    {
        aConfirmInsituStepDialog.SetActive(false);

        // Save in-situ instruction(s) for this step to disk (also saves instruction panel pose)
        await InstructionManager.Instance.SaveInsituInstructions();

        // Check if this is last step or if there are steps left to author
        if (InstructionManager.Instance.instructionSets.Count + InstructionManager.Instance.incompleteInstructionSets.Count == 0)
        {
            // Inform pc about authoring complete
            if (WebSocketClient.instance.connected)
            {
                WebSocketClient.instance.SendMsg(MsgType.AUTHFINISH, "");
            }

            // Finish authoring of this workflow
            WorkflowManager.instance.FinishAuthoringWorkflow();

            // Switch back to main scene
            SceneManager.LoadScene("Main");
        }
        else
        {
            // Show instructions for next step (automatically selects relevant step due to queue)
            InstructionManager.Instance.ShowInstructionSet();
        }
    }

    // User cancels finishing in-situ authoring for one step
    public void AssistedCancelFinishAuthInsitu()
    {
        aConfirmInsituStepDialog.SetActive(false);
    }

    #endregion

    #region Review

    // Populates step preview with instruction data for first step
    public void AssistedLoadFirstStep()
    {
        // Show instructions for next step (automatically selects relevant step due to queue)
        InstructionManager.Instance.ShowInstructionSet();
    }

    // User wants to edit generated 3D instruction (manipulate location)
    public void AssistedEdit3d()
    {
        InstructionManager.Instance.Make3dEditable();
    }

    // User wants to delete generated 3D instruction
    public void AssistedDelete3d()
    {
        InstructionManager.Instance.Make3dDeletable();
    }

    // User wants to add 3D instruction
    public void AssistedAdd3d()
    {

    }

    // User wants to edit generated text
    public void AssistedEditText()
    {
        // Allow text editing in assisted authoring mode
        AuthorText.instance.AssistedEditText();
    }

    // User wants to regenerate instructions
    public void AssistedRegenerateInstructions()
    {
        // Get relevant information of step to be regenerated
        int stepNumber = InstructionManager.Instance.currentInstructionSet.stepNumber;
        string false_action = InstructionManager.Instance.currentInstructionSet.instructionType;

        RegenInstructRequestObj riObj = new RegenInstructRequestObj(stepNumber.ToString(), false_action);
        string riObjStr = JsonUtility.ToJson(riObj);

        // Inform server that capture is terminated
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.REGENERATE, riObjStr);

            // Update UI
            aRegenInstructsButton.enabled = false;
            aRecaptureDemoButton.enabled = false;
            aNextStepInsituButton.enabled = false;

            // Hide QR-Code visuals
            QRAuth.instance.markerContent.SetActive(false);

            // Remove existing
            InstructionManager.Instance.RemoveCurrentInsituandText();
        }
    }

    // User wants to redo video for regeneration
    public void AssistedRedoInstruction()
    {
        aConfirmRedoDialog.SetActive(true);
    }

    // User confirms redo
    public void AssistedConfirmRedoInstruction()
    {
        // Update UI
        aConfirmRedoDialog.SetActive(false);
        aStepPreview.SetActive(false);
        aVideoAuthPanel.SetActive(true);
        aRegenInstructsButton.enabled = false;
        aRecaptureDemoButton.enabled = false;
        aNextStepInsituButton.enabled = false;
        AuthorText.instance.aEditTextButton.GetComponent<PressableButton>().enabled = false;

        // Remove existing 3D instructions and indicate redo in textual instruction
        InstructionManager.Instance.RemoveCurrentInsituandText();

        // Update step counter on video authoring panel
        aStepNumberLabel.text = $"{InstructionManager.Instance.currentInstructionSet.stepNumber}";

        // Prepare capture video panel for redo (populate with current video, disable/enable irrelevant buttons)
        AuthorVideo.instance.PopulateVideoPreview(InstructionManager.Instance.currentInstructionSet.videoInstructionPath, AuthorVideo.instance.aVideoPlayer);
        aNextStepButton.SetActive(false);
        aLastStepButton.transform.parent.gameObject.SetActive(false);
        aCaptureButton.SetActive(true);
        aConfirmRedoneVideoButton.SetActive(true);
    }

    // User cancels redo
    public void AssistedCancelRedoInstruction()
    {
        aConfirmRedoDialog.SetActive(false);
    }

    #endregion 

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


    #region Coroutines

    // Coroutine for counting down before video or image capture
    private IEnumerator CountDown(float durationInSecs)
    {
        // Disable handmenu
        handmenu.SetActive(false);
        // Show countdown panel
        countdownPanel.SetActive(true);
        // Show visualizer for camera center
        fovIndicators.SetActive(true);
        // Show capture instruction
        captureInstruction.SetActive(true);
        // Show center visualizer
        centerVisualizer.SetActive(true);

        // Disable far interaction
        farRayLeft.SetActive(false);
        farRayRight.SetActive(false);

        // Hide QR-Code visuals
        QRAuth.instance.markerContent.SetActive(false);

        // Counter
        float counter = durationInSecs;
        while (counter > 0)
        {
            // Change count down display
            countdownText.text = counter.ToString();

            // Decrease counter
            counter--;

            // Wait for 1 second between countdowns
            yield return new WaitForSeconds(1f);
        }

        // Start capture procedure
        HandtrackingManager.instance.StartCapturingHands();
        AuthorVideo.instance.StartCapturingVideo();

        // Inform server that video capture just started
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.CAPTURESTART, durationInSecs.ToString());
        }

        // Hide countdown panel and capture instruction
        captureInstruction.SetActive(false);
        countdownPanel.SetActive(false);

        // Reduce opacity of camera center visualizers
        foreach (var vis in captureVisualizers)
        {
            vis.color = new Color(1f, 1f, 1f, 0.7f);
        }
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
        // Set up UI in general (which elements (in-)active at the beginning)

        // Active elements
        workflowDialog.SetActive(true);

        // Inactive elements
        workflowNameDialog.SetActive(false);
        mConfirmRecaptureVideoDialog.SetActive(false);
        mConfirmTextDialog.SetActive(false);
        mConfirmVideoDialog.SetActive(false);
        mVideoAuthPanel.SetActive(false);
        mAuth3dPanel.SetActive(false);
        mConfirmDelete3dDialog.SetActive(false);
        startAuthoringDialog.SetActive(false);
        aStartInsituDialog.SetActive(false);
        qrcodePanel.SetActive(false);
        wsDialog.SetActive(false);
        streamDialog.SetActive(false);
        userstudyDialog.SetActive(false);
        handmenuCanvas.SetActive(false);
        startStepDialog.SetActive(false);

        aAuthNearMenu.SetActive(false);
        aVideoAuthPanel.SetActive(false);
        aConfirmRecaptureDialog.SetActive(false);
        aConfirmNextStepDialog.SetActive(false);
        aConfirmLastStepDialog.SetActive(false);
        aConfirmInsituStepDialog.SetActive(false);
        aStepPreview.SetActive(false);

        mAuthNearMenu.SetActive(false);
        mStepPreview.SetActive(false);
        mConfirmFinishStepDialog.SetActive(false);
        mConfirmFinishWorkflowDialog.SetActive(false);
        mTextAuthPanel.SetActive(false);


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
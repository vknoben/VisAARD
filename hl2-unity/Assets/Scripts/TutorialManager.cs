/// <summary>
/// This class manages most stuff regarding UI and its events
/// </summary>

using MixedReality.Toolkit.UX;
using Msg;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    #region UI elements

    [Tooltip("AR-Playground containing all stations to learn")]
    public GameObject arPlayground;

    // Handmenu 
    [Tooltip("General handmenu")]
    public GameObject handmenu;
    [Tooltip("List of all handmenu buttons not part of tutorial")]
    public List<GameObject> nonTutHandmenuButtons;
    [Tooltip("Button on handmenu used for tutorial")]
    public GameObject tutButtonHandmenu;

    // System pop ups
    [Tooltip("Dialog for selecting mode (authoring or guidance)")]
    public GameObject modeDialog;
    [Tooltip("Panel for starting tutorial")]
    public GameObject startTutorialDialog;

    // Tutorial stations
    [Tooltip("Panel explaining difference between near and far interaction")]
    public GameObject interactionPanel;
    [Tooltip("Panel explaining window organization")]
    public GameObject windowOrganizationPanel;

    // Other UI elements
    [Tooltip("Toggle for spatial behavior of window organization station")]
    public PressableButton toggleSpatialBehaviorButton;
    [Tooltip("Video capture orientation markers")]
    public GameObject videoCaptureVisualizers;
    [Tooltip("Countdown panel for video capture")]
    public GameObject countdownPanel;
    [Tooltip("Text element for countdown display")]
    public TMP_Text countdownText;
    [Tooltip("Instruction displayed before video capture")]
    public GameObject captureInstruction;
    [Tooltip("Input field for writing text")]
    public MRTKTMPInputField textInputField;

    #endregion


    #region Class properties

    [Tooltip("Cached position of 'organize windows' station for reset purpose")]
    private Vector3 organizeWindowsPanelInitPos;
    [Tooltip("Cached rotation of 'organize windows' station for reset purpose")]
    private Quaternion organizeWindowsPanelInitRot;
    [Tooltip("Cached scale of 'organize windows' station for reset purpose")]
    private Vector3 organizeWindowsPanelInitScale;

    [Tooltip("Countdown coroutine for video capture")]
    private Coroutine countDownCoroutine;
    [Tooltip("Check for user input coroutine for text writing station")]
    private Coroutine checkForUserInputCoroutine;

    [Tooltip("Exemplary 3D object used for manipulation tutorial station")]
    public GameObject exampleObj;
    [Tooltip("Cached position of example object for reset purpose")]
    private Vector3 exampleObjInitPos;
    [Tooltip("Cached rotation of example object for reset purpose")]
    private Quaternion exampleObjInitRot;
    [Tooltip("Cached scale of example object for reset purpose")]
    private Vector3 exampleObjInitScale;

    #endregion


    #region UI callbacks

    // User started tutorial
    public void StartTutorial()
    {
        // Update UI
        startTutorialDialog.SetActive(false);
        arPlayground.SetActive(true);

        // Spawn AR-Playground in front of user
        SpawnArPlayground();

        // Start listening for user input for text writing station
        checkForUserInputCoroutine = StartCoroutine(CheckForUserInput());

        // Make sure to disregard native input events
        textInputField.onValidateInput += (string text, int charIndex, char addedChar) =>
        {
            // By returning null, we "swallow" the native event.s
            return '\0';
        };
    }

    // User reset organize windows panel
    public void ResetOrganizeWindowsPanel()
    {
        // Reset back to cached pose
        windowOrganizationPanel.transform.SetPositionAndRotation(organizeWindowsPanelInitPos, organizeWindowsPanelInitRot);
        windowOrganizationPanel.transform.localScale = organizeWindowsPanelInitScale;

        // Make sure anchored for now
        toggleSpatialBehaviorButton.ForceSetToggled(false);
    }

    // Start emulated video capture
    public void StartVideoCapture()
    {
        // Update UI
        arPlayground.SetActive(false);

        // Start countdown for 5 seconds before capture
        countDownCoroutine = StartCoroutine(CountDown(5f));
    }

    // User reset example object for manipulation station
    public void ResetExampleObj()
    {
        // Reset back to cached pose
        exampleObj.transform.SetPositionAndRotation(exampleObjInitPos, exampleObjInitRot);
        exampleObj.transform.localScale = exampleObjInitScale;
    }

    // User finished tutorial
    public void FinishTutorial()
    {
        // Update UI
        arPlayground.SetActive(false);
        modeDialog.SetActive(true);

        // Stop checking for user input for text writing station
        StopCoroutine(checkForUserInputCoroutine);

        // Reset everything that is resetable
        ResetOrganizeWindowsPanel();
        ResetExampleObj();
    }

    // Reposition tutorial in front of user
    public void RepositionTutorial()
    {
        // Reset everything
        ResetOrganizeWindowsPanel();
        ResetExampleObj();

        // Respawn tutorial in front of user
        SpawnArPlayground();
    }

    #endregion


    #region Methods

    // Position AR-Playground in front of user
    public void SpawnArPlayground()
    {
        // Get user head (main camera)
        Transform userHead = Camera.main.transform;

        // Parent playground to main camera
        arPlayground.transform.SetParent(userHead, false);

        // Put playground in front of user (gaze direction)
        arPlayground.transform.SetLocalPositionAndRotation(Vector3.forward, Quaternion.identity);

        // Put playground on same height as user head
        Vector3 modifiedPos = arPlayground.transform.position;
        arPlayground.transform.SetParent(userHead, false);
        modifiedPos.y = userHead.position.y;
        arPlayground.transform.position = modifiedPos;

        // Place playground at given distance from user
        arPlayground.transform.localPosition = arPlayground.transform.localPosition.normalized;

        // Unparent playground
        arPlayground.transform.SetParent(null);

        // Make playground face user
        arPlayground.transform.LookAt(userHead);
        // Rotate playground about y-axis for 180� otherwise inverted orientation
        arPlayground.transform.Rotate(Vector3.up, 180);

        // Apply height offset
        arPlayground.transform.position += Vector3.up * -0.25f;

        // Enable playground
        arPlayground.SetActive(true);

        // Cache current pose of "organize windows" station
        organizeWindowsPanelInitPos = windowOrganizationPanel.transform.position;
        organizeWindowsPanelInitRot = windowOrganizationPanel.transform.rotation;
        organizeWindowsPanelInitScale = windowOrganizationPanel.transform.localScale;

        // Cache current pose of example object for manipulation station
        exampleObjInitPos = exampleObj.transform.position;
        exampleObjInitRot = exampleObj.transform.rotation;
        exampleObjInitScale = exampleObj.transform.localScale;
    }

    // Coroutine for counting down before video or image capture
    private IEnumerator CountDown(float durationInSecs)
    {
        // Update UI
        captureInstruction.SetActive(true);
        countdownPanel.SetActive(true);
        videoCaptureVisualizers.SetActive(true);

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

        // Hide countdown panel and capture instruction
        captureInstruction.SetActive(false);
        countdownPanel.SetActive(false);
    }

    // Stop emulated capture
    public void StopCapture()
    {
        // Update UI
        videoCaptureVisualizers.SetActive(false);
        countdownPanel.SetActive(false);
        captureInstruction.SetActive(false);
        arPlayground.SetActive(true);

        // Stop countdown if still running
        StopCoroutine(countDownCoroutine);
    }

    // Check for user input for text writing
    public IEnumerator CheckForUserInput()
    {
        Debug.Log("Checking for user input now");

        // Select input field
        textInputField.Select();

        // Set caret position to end of text
        textInputField.caretPosition = textInputField.text.Length;

        while (true)
        {
            // Select input field to show caret (also combats loose focus)
            textInputField.Select();

            // Read keyboard input
            if (Input.anyKey)
            {
                // Get caret position
                int caretPos = textInputField.caretPosition;
                //mInstructionInputField.Select();

                // Handle return
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    // Insert newline at caret
                    textInputField.text = textInputField.text.Insert(caretPos, "\n");
                    // Move caret forward
                    textInputField.caretPosition = caretPos + 1;
                }
                // Handle backspace
                else if (Input.GetKey(KeyCode.Backspace))
                {
                    yield return null;
                    continue;
                }
                // Arrow key (left)
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    yield return null;
                    continue;
                }
                // Arrow key (right)
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    yield return null;
                    continue;
                }
                // Handle alphanumeric, space input
                else if (Input.inputString != string.Empty)
                {
                    textInputField.text = textInputField.text.Insert(caretPos, Input.inputString);
                    // Move caret forward
                    textInputField.caretPosition = caretPos + 1;
                }
            }


            yield return null;
        }
    }

    #endregion


    #region Unity lifecycle

    private void Start()
    {
        // Disable tutorial stuff by default
        startTutorialDialog.SetActive(false);
        arPlayground.SetActive(false);
    }

    #endregion
}
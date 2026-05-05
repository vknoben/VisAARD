/// <summary>
/// This script manages the logic behind authoring of textual instructions
/// </summary>
using MixedReality.Toolkit.UX;
using TMPro;
using UnityEngine;
using Msg;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.Localization.Components;



#if ENABLE_WINMD_SUPPORT
using System;
using Windows.Storage;
#endif

public class AuthorText : MonoBehaviour
{
    public static AuthorText instance;

    #region Properties

    [Tooltip("Path to instruction text file")]
    public string textPath;
    //[Tooltip("List of paths to text instructions in the order they were authored")]
    //public List<string> textPaths = new List<string>();
    [Tooltip("Text file name (used in WINMD)")]
    private string fileName;
#if ENABLE_WINMD_SUPPORT
    private StorageFile textFile;
#endif

    [Tooltip("Flag indicating that text is currently authored. During that time keyboard input is read")]
    public bool authoringText = false;
    [Tooltip("Delay between repeats in seconds")]
    public float repeatDelay = 0.1f;

    [Tooltip("Flag indicating if text instruction was authored")]
    public bool textAuthored = false;

    [Tooltip("Courotine used for checking physical keyboard input. Assigned and dessigned")]
    public Coroutine readKeyboardRoutine = null;

    #endregion


    #region UI elements

    // Manual authoring
    [Tooltip("Instruction input field (manual authoring)")]
    public MRTKTMPInputField mInstructionInputField;
    [Tooltip("Button for finishing authoring of textual instruction (manual authoring)")]
    public PressableButton mDoneButton;
    [Tooltip("Textual instruction preview (manual authoring)")]
    public TMP_Text mTextPreview;

    // Assisted authoring
    [Tooltip("Main text on textual instruction preview (assisted authoring)")]
    public TMP_Text aTextPreview;
    [Tooltip("Preview text instruction panel (assisted authoring)")]
    public GameObject aTextPreviewPanel;
    [Tooltip("Edit text panel (assisted authoring)")]
    public GameObject aEditTextPanel;
    [Tooltip("Instruction input field (assisted authoring)")]
    public MRTKTMPInputField aInstructionInputField;
    [Tooltip("Button to edit generated text")]
    public GameObject aEditTextButton;
    [Tooltip("Button to finalize edited generated text")]
    public GameObject aEditTextDoneButton;

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

        // Make input fields not trigger usual callbacks
        mInstructionInputField.onValidateInput += (string text, int charIndex, char addedChar) =>
        {
            // By returning null, we "swallow" the native event.
            return '\0';
        };
        aInstructionInputField.onValidateInput += (string text, int charIndex, char addedChar) =>
        {
            // By returning null, we "swallow" the native event.s
            return '\0';
        };
    }

    #endregion


    #region Both manual and assisted authoring

    // Save instruction text
    public async Task SaveInstructionText()
    {
        //Log.Msg("Saving user input...");

        // Current input for textual instruction
        string userInput = mInstructionInputField.text;

        // Send textual instruction to PC
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.TEXTINSTRUCT, userInput);
        }

        // Cache authored text instruction (only in semi-automatic authoring mode)
        if (WebSocketClient.instance.assistedAuthMode)
        {
            InstructionManager.Instance.TextInstruction = userInput;
        }

        // Store user given instruction text on device
#if ENABLE_WINMD_SUPPORT
        // On HoloLens 2
        // Check if .txt file exists already
        if (textPath == null || textPath == string.Empty)
        {
            // Create .txt file containing textual instruction as given by user
            fileName = "text.txt";
            textFile = await WorkflowManager.instance.currentStepFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            // Save path
            textPath = textFile.Path;

            // Add path to list of instruction paths
            //textPaths.Add(textPath);

            //Log.Msg("Created .txt file for instruction text of this step");
        }

        // Write user input text to file
        await FileIO.WriteTextAsync(textFile, userInput);

        // Flag as authored
        textAuthored = true;

        //Log.Msg("Wrote user input text instruction to file");
#endif
    }

    // Reselect callback
    public void ReselectCallback()
    {
        StartCoroutine(Reselect());
    }

    // Select inputfield again after one frame to avoid conflict with deselect
    public IEnumerator Reselect()
    {
        yield return new WaitForSeconds(0);

        // Select inputfield again
        mInstructionInputField.Select();
    }

    // Deinitialize text authoring
    public void ResetTextAuth()
    {
        // Reset file refs
        textPath = null;
        fileName = null;
        textAuthored = false;

        // Reset text in inputfield
        mInstructionInputField.text = string.Empty;

        // Re-localize text preview
        mTextPreview.GetComponent<LocalizeStringEvent>().enabled = true;

        //Log.Msg("Reset text data");
    }

    #endregion


    #region Manual authoring only

    // Populate text instruction preview
    public void ManualPopulatePreview()
    {
        // Unlocalize text field
        mTextPreview.GetComponent<LocalizeStringEvent>().enabled = false;

        mTextPreview.text = mInstructionInputField.text;
    }

    // Populate preview with specified text
    public void ManualPopulatePreviewWithSpecified(string text)
    {
        // Unlocalize text field
        mTextPreview.GetComponent<LocalizeStringEvent>().enabled = false;

        mTextPreview.text = text;
    }

    // Verify if input for textual instruction given and we can move on
    public void ManualVerfiyInstructionInput()
    {
        if (mInstructionInputField.text != string.Empty)
        {
            // Enable done button
            mDoneButton.enabled = true;
        }
        else
        {
            // Disable button
            mDoneButton.enabled = false;
        }
    }

    // Check for user input during text authoring phase (manual)
    public IEnumerator ManualCheckForUserInput()
    {
        Debug.Log("Checking for user input now");
        //int caretAt = instructionInputField.caretPosition;

        // Select input field
        mInstructionInputField.Select();

        // Set caret position to end of text
        mInstructionInputField.caretPosition = mInstructionInputField.text.Length;

        while (true)
        {
            // Select input field to show caret (also combats loose focus)
            mInstructionInputField.Select();

            // Read keyboard input
            if (Input.anyKey)
            {
                // Get caret position
                int caretPos = mInstructionInputField.caretPosition;
                //mInstructionInputField.Select();


                // Handle return
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    //// New line
                    //mInstructionInputField.text += "\n";

                    // Insert newline at caret
                    mInstructionInputField.text = mInstructionInputField.text.Insert(caretPos, "\n");
                    // Move caret forward
                    mInstructionInputField.caretPosition = caretPos + 1;
                }
                // Handle backspace
                else if (Input.GetKey(KeyCode.Backspace))
                {
                    yield return null;
                    continue;

                    //// Delete last character (if any left)
                    //if (mInstructionInputField.text != string.Empty)
                    //{
                    //    if (elapsedTime == 0f || elapsedTime > repeatDelay)
                    //    {
                    //        //mInstructionInputField.text = mInstructionInputField.text[..^1];

                    //        // Remove 1 character behind the caret
                    //        mInstructionInputField.text = mInstructionInputField.text.Remove(caretPos - 1, 1);
                    //        // Move caret back
                    //        mInstructionInputField.caretPosition = caretPos - 1;

                    //        elapsedTime = 0f;
                    //    }
                    //}

                    //elapsedTime += Time.deltaTime;
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
                    // Add character(s) at caret position
                    //instructionInputField.text.Insert(caretAt, Input.inputString);
                    //mInstructionInputField.text += Input.inputString;

                    mInstructionInputField.text = mInstructionInputField.text.Insert(caretPos, Input.inputString);
                    // Move caret forward
                    mInstructionInputField.caretPosition = caretPos + 1;

                }
            }


            yield return null;
        }
    }

    #endregion


    #region Assisted authoring only

    // Populate preview with specified text
    public void AssistedPopulatePreviewWithSpecified(string text)
    {
        aTextPreview.text = text;
    }

    // User edits generated text
    public void AssistedEditText()
    {
        // Transfer generated text into editable input field
        aInstructionInputField.text = aTextPreview.text;

        // Update UI
        aEditTextButton.SetActive(false);
        aEditTextDoneButton.SetActive(true);
        UIAuth.instance.aNextStepInsituButton.enabled = false;

        // Interrupt face user behavior
        UIAuth.instance.aStepPreview.GetComponent<MakeObjFaceUser>().enabled = false;

        // Switch text preview panel with text input field
        aTextPreviewPanel.SetActive(false);
        aEditTextPanel.SetActive(true);

        // Disable far rays
        //UIAuth.instance.farRayLeft.SetActive(false);
        //UIAuth.instance.farRayRight.SetActive(false);

        // Inform client about the fact that text was edited
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.DIDEDITTEXT, string.Empty);
        }

        // Start checking for physical keyboard input
        readKeyboardRoutine = StartCoroutine(AssistedCheckForUserInput());
    }

    // User finalizes editing generated text
    public void AssistedFinalizeEditText()
    {
        // Transfer edited text into preview text panel
        aTextPreview.text = aInstructionInputField.text;

        // Switch text preview panel with text input field
        aTextPreviewPanel.SetActive(true);
        aEditTextPanel.SetActive(false);

        // Update UI
        aEditTextButton.SetActive(true);
        aEditTextDoneButton.SetActive(false);
        UIAuth.instance.aNextStepInsituButton.enabled = true;

        // Save edited text instruction (will be written to disk on next step)
        InstructionManager.Instance.currentInstructionSet.textInstruction = aTextPreview.text;

        // Enable far rays
        //UIAuth.instance.farRayLeft.SetActive(true);
        //UIAuth.instance.farRayRight.SetActive(true);

        // Stop checking for input
        StopCoroutine(readKeyboardRoutine);
    }

    // Verify if input for textual instruction given and we can move on
    public void AssistedVerfiyInstructionInput()
    {
        if (aInstructionInputField.text != string.Empty)
        {
            // Enable done button
            aEditTextDoneButton.GetComponent<PressableButton>().enabled = true;
        }
        else
        {
            // Disable button
            aEditTextDoneButton.GetComponent<PressableButton>().enabled = false;
        }
    }


    // Check for user input during text authoring phase (manual)
    public IEnumerator AssistedCheckForUserInput()
    {
        Debug.Log("Checking for user input now");
        //int caretAt = instructionInputField.caretPosition;

        // Select input field
        aInstructionInputField.Select();

        // Set caret position to end of text
        aInstructionInputField.caretPosition = aInstructionInputField.text.Length;

        while (true)
        {
            // Select input field to show caret (also combats loose focus)
            aInstructionInputField.Select();

            // Read keyboard input
            if (Input.anyKey)
            {
                // Get caret position
                int caretPos = aInstructionInputField.caretPosition;
                //aInstructionInputField.Select();

                // Handle return
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    //// New line
                    //mInstructionInputField.text += "\n";

                    // Insert newline at caret
                    aInstructionInputField.text = aInstructionInputField.text.Insert(caretPos, "\n");
                    // Move caret forward
                    aInstructionInputField.caretPosition = caretPos + 1;
                }
                // Handle backspace
                else if (Input.GetKey(KeyCode.Backspace))
                {
                    yield return null;
                    continue;

                    //// Delete last character (if any left)
                    //if (aInstructionInputField.text != string.Empty)
                    //{
                    //    if (elapsedTime == 0f || elapsedTime > repeatDelay)
                    //    {
                    //        //mInstructionInputField.text = mInstructionInputField.text[..^1];

                    //        // Remove 1 character behind the caret
                    //        aInstructionInputField.text = aInstructionInputField.text.Remove(caretPos - 1, 1);
                    //        // Move caret back
                    //        aInstructionInputField.caretPosition = caretPos - 1;

                    //        elapsedTime = 0f;
                    //    }
                    //}

                    //elapsedTime += Time.deltaTime;
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
                    // Add character(s) at caret position
                    //instructionInputField.text.Insert(caretAt, Input.inputString);
                    //mInstructionInputField.text += Input.inputString;

                    aInstructionInputField.text = aInstructionInputField.text.Insert(caretPos, Input.inputString);
                    // Move caret forward
                    aInstructionInputField.caretPosition = caretPos + 1;

                }
            }


            yield return null;
        }
    }

    #endregion
}

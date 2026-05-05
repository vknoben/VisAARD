///<summary>
/// This script manages logic behind organization of the authored workflow.
/// </summary>
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using MixedReality.Toolkit.UX;



#if ENABLE_WINMD_SUPPORT
using System;
using Windows.Storage;
#endif

public class WorkflowManager : MonoBehaviour
{
    // Single manager
    public static WorkflowManager instance;

    #region Properties

#if ENABLE_WINMD_SUPPORT
    public StorageFolder workflowFolder;
    public StorageFolder currentStepFolder;
    [Tooltip("Dynamically extended list of folders containing authored step data")]
    public List<StorageFolder> stepFolders = new();
#endif

    [Tooltip("Folder name of currently authored workflow. Contains all instructional data")]
    public string workflowFolderName;
    [Tooltip("Folder name of currently authored step. Contains all instructional data for this step")]
    public string currentStepFolderName;
    [Tooltip("Number of currently authored work step")]
    public int stepNumber = 0;
    [Tooltip("Number of steps in total")]
    public int totalStepCount = 0;

    #endregion


    #region UI elements


    #endregion



    #region Unity lifecycle

    void Start()
    {
        // Initialize step numbering to 0
        stepNumber = 1;
    }

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

    #endregion


    #region Methods

    // Create folder for workflow stuff to be stored in
    public async void CreateWorkflowFolder(string folderName)
    {
#if ENABLE_WINMD_SUPPORT
        // On HoloLens 2
        // Local app folder
        StorageFolder appFolder = ApplicationData.Current.LocalFolder;

        // Create new folder inside app folder (Saved in LocalAppData\Appname\LocalState\)
        workflowFolder = await appFolder.CreateFolderAsync(folderName);

        // Store workflow folder name
        workflowFolderName = folderName;

        // Associate workflow name and folder path via player prefs (to persist between sessions)
        PlayerPrefs.SetString(folderName, workflowFolder.Path);

        // Create entry which holds all keys if none exsists yet
        if (PlayerPrefs.HasKey("keys") == false)
        {
            // This is the very first workflow -> Create new entry
            PlayerPrefs.SetString("keys", folderName);
        }
        else
        {
            // Entry with keys exsists already -> Only add new key
            string keysValue = PlayerPrefs.GetString("keys");
            keysValue += " ";
            keysValue += folderName;
            PlayerPrefs.SetString("keys", keysValue);
        }

        // Create folder for first step
        currentStepFolder = await workflowFolder.CreateFolderAsync("step_1");
        ////Log.Msg($"Created step folder for step {stepNumber}");

        // Add folder to authored step folders for this workflow
        stepFolders.Add(currentStepFolder);

        // Store current step folder name
        currentStepFolderName = "step_1";

        // Send workflow name to pc
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(Msg.MsgType.WFNAME, folderName);
        }
#endif
    }

    // Move on to authoring next step. Returns true if next step has already authored content
    public async Task PrepareFolderForNextStep()
    {
        // Increase step counter
        stepNumber++;

#if ENABLE_WINMD_SUPPORT
        // Create folder for next step (if not already existing because previously returned)
        try
        {
            // No folder for this step exists yet
            currentStepFolder = await workflowFolder.CreateFolderAsync($"step_{stepNumber}", CreationCollisionOption.FailIfExists);
            currentStepFolderName = $"step_{stepNumber}";
            //Log.Msg($"Created new folder for step {stepNumber}");

            // Add folder to authored step folders for this workflow
            stepFolders.Add(currentStepFolder);
            //Log.Msg($"Saved newly created folder for step {stepNumber} into stepFolder");

            // Send new step number to pc
            if (WebSocketClient.instance.connected)
            {
                WebSocketClient.instance.SendMsg(Msg.MsgType.NEWSTEP, stepNumber.ToString());
            }
        }
        catch 
        {
            //Log.Msg("Something failed during preparation for next step");
        }
#endif
    }

    // Finish authoring of entire workflow
    public void FinishAuthoringWorkflow()
    {
        // Reset step counter
        stepNumber = 1;

        // Clear working directories
        workflowFolderName = null;
        currentStepFolderName = null;
#if ENABLE_WINMD_SUPPORT
        workflowFolder = null;
        currentStepFolder = null;
#endif

        // Reset data
        AuthorText.instance.ResetTextAuth();
        AuthorVideo.instance.ResetVideoAuthAssisted();
        AuthorVideo.instance.ResetVideoAuthManual();
        Author3d.instance.Reset3dAuth();
        HandtrackingManager.instance.ResetHandData();
    }

    // Save current number of steps as total steps count
    public void SaveStepCount()
    {
        totalStepCount = stepNumber;
        //Log.Msg($"Total step count is: {totalStepCount}");
    }

    #endregion
}

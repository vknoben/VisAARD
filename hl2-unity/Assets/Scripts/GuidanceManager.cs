///<summary>
/// This script manages logic behind organization of the authored workflow.
/// </summary>

using MixedReality.Toolkit;
using MixedReality.Toolkit.UX;
using SerializableHandtracking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using SerializableInstructions;
using GuidanceData;
using System.IO;
using System;
using MixedReality.Toolkit.SpatialManipulation;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

namespace GuidanceData
{
    [Tooltip("All data of a single work step, loaded into memory before guidance starts")]
    public class StepData
    {
        public string path;                                         // Path to step folder
        public SerializableInstructionPanelPose panelPose;          // Pose of instruction panel relative to qr-code (null if not available)
        public string text;                                         // Textual instruction
        public string videoPath;                                    // Path to video instruction (streamed on display)
        public SerializableInsituInstructionSet manualSet;          // In-situ instructions from manual authoring (3d.json, null if not available)
        public SerializableInsituInstructionSet autoSet;            // In-situ instructions from assisted authoring (instructions.json, null if not available)
        public HandTrackingDataset handtrackingData;                // Handtracking data for assisted in-situ hands (null if not available)
    }

    [Tooltip("All data of a single workflow, loaded into memory before guidance starts")]
    public class WorkflowData
    {
        public string name;                                         // Name of workflow (folder name)
        public string path;                                         // Path to workflow folder
        public List<StepData> steps = new();                        // Steps in numerical order
        public List<string> warnings = new();                       // Problems encountered while loading (logged on main thread)
    }

    [Tooltip("Phase of guidance session")]
    public enum GuidancePhase
    {
        Regular,                                                    // Single workflow (regular guidance)
        Sequential,                                                 // Study mode: workflows are shown one after another
        Comparison                                                  // Study mode: free switching between workflows on step level
    }
}

public class GuidanceManager : MonoBehaviour
{
    // Single manager
    public static GuidanceManager instance;

    #region Properties

    // Workflow
    [Tooltip("List of existing workflow names")]
    public List<string> workflowNames = new();
    [Tooltip("List of existing workflow paths")]
    public List<string> workflowPaths = new();

#if ENABLE_WINMD_SUPPORT
    [Tooltip("Folder of selected workflow")]
    StorageFolder selectedWorkflowFolder;
    [Tooltip("List of folders containing data for steps of selected workflow")]
    IReadOnlyList<StorageFolder> stepFolders;
#endif
    [Tooltip("Names of workflows guided through in this session, in presentation order (single workflow outside study mode)")]
    public List<string> sessionWorkflowNames = new();
    [Tooltip("Paths of workflows guided through in this session, in presentation order (single workflow outside study mode)")]
    public List<string> sessionWorkflowPaths = new();
    [Tooltip("Data of all workflows of this session, loaded into memory before guidance starts (same order as sessionWorkflowPaths)")]
    public List<WorkflowData> sets = new();
    [Tooltip("Index of currently shown workflow in sets")]
    public int activeSet = 0;
    [Tooltip("Current phase of guidance session")]
    public GuidancePhase phase = GuidancePhase.Regular;
    [Tooltip("Step number of currently shown instructions. Starts at 0")]
    public int currentStepNumber = 0;
    [Tooltip("Height of instruction panel above qr-code in study mode (meters). Stored panel poses are ignored in study mode")]
    public float studyPanelHeight = 0.5f;

    // Hands
    [Tooltip("Handtracking dataset")]
    private HandTrackingDataset handtrackingData;
    [Tooltip("Frame iterator synced to video time for hand playback")]
    public int frameIterator = 0;
    [Tooltip("Rigged visual joints (left) assigned when spawning in-situ hand")]
    private Transform[] leftRiggedVisualJointsArray = new Transform[(int)TrackedHandJoint.TotalJoints];
    [Tooltip("Rigged visual joints (right) assigned when spawning in-situ hand")]
    private Transform[] rightRiggedVisualJointsArray = new Transform[(int)TrackedHandJoint.TotalJoints];
    [Tooltip("Offset needed to synchronize video and hand animation")]
    public float syncOffset = 0.5f;
    [Tooltip("Wait of animation in initial pose before it starts")]
    public float animWaitTime = 2f;
    [Tooltip("Replay speed factor")]
    public float handAnimSpeedFactor = 1f;
    [Tooltip("Coroutine for hand animation playback")]
    public Coroutine handAnimRoutine;

    [Tooltip("Offsets applied to animated hands to match user authoring")]
    private Vector3 animOffsetPosL = Vector3.zero;
    private Quaternion animOffsetRotL = Quaternion.identity;
    private Vector3 animOffsetPosR = Vector3.zero;
    private Quaternion animOffsetRotR = Quaternion.identity;

    // 3D instructions
    [Tooltip("List of 3D instructions loaded from file")]
    private SerializableInsituInstructionSet iSet;
    // 3D instruction prefabs
    [Tooltip("Prefab for straight arrow instruction")]
    public GameObject straightArrowPrefab;
    [Tooltip("Prefab for curved arrow instruction")]
    public GameObject curvedArrowPrefab;
    [Tooltip("Prefab for circular arrow instruction")]
    public GameObject circularArrowPrefab;
    [Tooltip("Prefab for hammer instruction")]
    public GameObject hammerPrefab;
    [Tooltip("Prefab for pliers instruction")]
    public GameObject pliersPrefab;
    [Tooltip("Prefab for Allen key instruction")]
    public GameObject allenPrefab;
    [Tooltip("Prefab for Allen key instruction with cw rotary arrow")]
    public GameObject allenCwPrefab;
    [Tooltip("Prefab for Allen key instruction with ccw rotary arrow")]
    public GameObject allenCcwPrefab;
    [Tooltip("Prefab for wrench instruction")]
    public GameObject wrenchPrefab;
    [Tooltip("Prefab for screwdriver instruction")]
    public GameObject screwdriverPrefab;
    [Tooltip("Prefab for screwdriver with cw rotary arrow")]
    public GameObject screwdriverCwPrefab;
    [Tooltip("Prefab for screwdriver with ccw rotary arrow")]
    public GameObject screwdriverCcwPrefab;
    [Tooltip("Prefab for open hand instruction")]
    public GameObject openHandPrefab;
    [Tooltip("Prefab for pointing hand instruction")]
    public GameObject pointHandPrefab;
    [Tooltip("Prefab for picking hand instruction")]
    public GameObject pickHandPrefab;
    // List of 3d instructions instantiated for a single step
    private List<GameObject> instantiated3dObjs = null;

    // Colored materials for manually authored in-situ instructions
    public Material whiteMat;
    public Material grayMat;
    public Material brownMat;
    public Material pinkMat;
    public Material redMat;
    public Material purpleMat;
    public Material orangeMat;
    public Material yellowMat;
    public Material limeMat;
    public Material greenMat;
    public Material cyanMat;
    public Material blueMat;
    [Tooltip("Dictionary mapping color names to materials")]
    private Dictionary<string, Material> colorDict = new Dictionary<string, Material>();

    #endregion


    #region UI elements

    [Tooltip("Prefab of workflow list element")]
    public GameObject workflowListElementPrefab;

    [Tooltip("Workflow list")]
    public Transform workflowList;
    [Tooltip("Workflow list for study mode")]
    public Transform workflowListStudyMode;
    [Tooltip("No workflows yet text display")]
    public GameObject noWorkflowsText;
    [Tooltip("Collection of toggles for selecting workflow")]
    public ToggleCollection workflowToggles;
    [Tooltip("Select workflow button")]
    public PressableButton selectWorkflowButton;
    [Tooltip("Collection of toggles for selecting workflow in study mode")]
    public ToggleCollection studyWorkflowOrderCollection;
    [Tooltip("Confirm button for workflow selection in study mode")]
    public PressableButton selectStudyWorkflowsButton;
    [Tooltip("Number of workflows that have to be selected in study mode")]
    public int numOfStudyWorkflows = 3;
    // Folder in StreamingAssets containing tutorial workflows (one subfolder per workflow), not listed as regular workflow
    private const string TutorialFolderName = "Tutorial";
    [Tooltip("Toggles of study mode workflow list (same index as workflowNames)")]
    private List<PressableButton> studyWorkflowToggles = new();
    [Tooltip("Indices of workflows selected in study mode (ascending list order)")]
    public List<int> selectedStudyWorkflowIndices = new();
    [Tooltip("Presentation orders in study mode. Entry i belongs to i-th order toggle, values index into selectedStudyWorkflowIndices")]
    private readonly int[][] studyOrders =
    {
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 },
        new[] { 1, 0, 2 },
        new[] { 1, 2, 0 },
        new[] { 2, 0, 1 },
        new[] { 2, 1, 0 }
    };

    [Tooltip("Step number display")]
    public TMP_Text stepNumberLabel;

    // Navigation
    [Tooltip("Button to advance to next step")]
    public PressableButton nextStepButton;
    [Tooltip("Button on handmenu to advance to next step")]
    public PressableButton hmNextStepButton;
    [Tooltip("Button to return to previous step")]
    public PressableButton previousStepButton;
    [Tooltip("Button on handmenu to return to previous step")]
    public PressableButton hmPrevStepButton;
    [Tooltip("Step number label on handmenu")]
    public TMP_Text hmStepNumberLabel;
    [Tooltip("Toggle collection of switch menu on hand")]
    public ToggleCollection hmSwitchToggles;
    [Tooltip("Toggle collection of switch menu on instruction panel")]
    public ToggleCollection switchToggles;

    // Text
    [Tooltip("Text instruction panel")]
    public GameObject textInstructionPanel;
    [Tooltip("Textual instruction")]
    public TMP_Text instructionText;

    // Video
    [Tooltip("Video instruction panel")]
    public GameObject videoInstructionPanel;
    [Tooltip("Target texture for captured video")]
    public Texture targetVideoTexture;
    [Tooltip("Raw image for video display")]
    public RawImage videoImage;
    [Tooltip("Video player on video preview object during authoring")]
    public VideoPlayer videoPlayer;
    [Tooltip("Playbutton displayed on top of video")]
    public GameObject playButton;

    // Hands
    [Tooltip("Left hand object")]
    public GameObject leftHand;
    [Tooltip("Right hand object")]
    public GameObject rightHand;
    [Tooltip("Armature of left hand (wrist)")]
    public GameObject leftArmature;
    [Tooltip("Armature of right hand (wrist)")]
    public GameObject rightArmature;

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

        // Initialize the rigged visual joints array using a depth-first traversal of the armature (left and right)
        int index = (int)TrackedHandJoint.Wrist;
        List<Transform> leftJointsTransforms = leftArmature.transform.GetComponentsInChildren<Transform>().ToList();
        List<Transform> rightJointsTransforms = rightArmature.transform.GetComponentsInChildren<Transform>().ToList();
        for (int i = 0; i < leftJointsTransforms.Count; i++)
        {
            // Skip joint end tips
            if (leftJointsTransforms[i].name.Contains("end"))
            {
                continue;
            }

            leftRiggedVisualJointsArray[index] = leftJointsTransforms[i];
            rightRiggedVisualJointsArray[index] = rightJointsTransforms[i];
            index++;
        }
    }

    // Start
    void Start()
    {
        // List available workflows as scene starts with this menu
        ListAvailableWorkflows();

        // Initialize color dictionary
        colorDict = new Dictionary<string, Material>
        {
            { "white", whiteMat },
            { "gray", grayMat },
            { "brown", brownMat },
            { "pink", pinkMat },
            { "red", redMat },
            { "purple", purpleMat },
            { "orange", orangeMat },
            { "yellow", yellowMat },
            { "lime", limeMat },
            { "green", greenMat },
            { "cyan", cyanMat },
            { "blue", blueMat }
        };
    }

    #endregion


    #region Methods

    // List available workflows to user
    public void ListAvailableWorkflows()
    {
        // Clear existing lists to avoid duplicates
        workflowNames.Clear();
        workflowPaths.Clear();
        studyWorkflowToggles.Clear();

        // Study mode selection can only be confirmed once enough workflows are selected
        selectStudyWorkflowsButton.enabled = false;

        try
        {
            // List both local app data and streaming assets, as workflows could be stored in either location depending on authoring method
            string[] searchPaths = new string[] 
            { 
                Application.persistentDataPath, 
                Application.streamingAssetsPath
            };

            foreach (string basePath in searchPaths)
            {
                if (Directory.Exists(basePath))
                {
                    // Get all workflow directories
                    string[] directories = Directory.GetDirectories(basePath);

                    if (directories.Length > 0)
                    {
                        foreach (string dir in directories)
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(dir);

                            // Skip "Speech" folder created by MRTK's Speech Command system and tutorial folder (started separately)
                            if (dirInfo.Name.Equals("Speech", StringComparison.OrdinalIgnoreCase) ||
                                dirInfo.Name.Equals(TutorialFolderName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // Avoid duplicate workflows present in both StreamingAssets and persistent data
                            if (workflowNames.Contains(dirInfo.Name))
                            {
                                continue;
                            }

                            // Create list element
                            GameObject listElement = Instantiate(workflowListElementPrefab, workflowList);

                            // Set label to workflow name
                            listElement.GetComponentInChildren<TMP_Text>().text = dirInfo.Name;

                            // Create list element for study mode (multi selection, same index as regular list)
                            GameObject studyListElement = Instantiate(workflowListElementPrefab, workflowListStudyMode);
                            studyListElement.GetComponentInChildren<TMP_Text>().text = dirInfo.Name;

                            // Re-evaluate study selection whenever this element is (un)toggled
                            PressableButton studyToggle = studyListElement.GetComponentInChildren<PressableButton>();
                            studyToggle.IsToggled.OnEntered.AddListener(_ => UpdateStudySelection());
                            studyToggle.IsToggled.OnExited.AddListener(_ => UpdateStudySelection());
                            studyWorkflowToggles.Add(studyToggle);

                            // Store workflow names and paths
                            workflowNames.Add(dirInfo.Name);
                            workflowPaths.Add(dirInfo.FullName);
                        }
                    }
                }
            }

            // UI Update basierend darauf, ob Workflows gefunden wurden
            if (workflowNames.Count > 0)
            {
                workflowToggles.enabled = true;
                selectWorkflowButton.enabled = true;
                noWorkflowsText.SetActive(false);
            }
            else
            {
                noWorkflowsText.SetActive(true);
            }
        }
        catch (Exception ex)
        {
            Log.Msg($"Error listing workflows: {ex.Message}");
            noWorkflowsText.SetActive(true);
        }
    }

    // Collect workflows selected in study mode and unlock confirmation if exactly the required number is selected
    public void UpdateStudySelection()
    {
        selectedStudyWorkflowIndices.Clear();
        for (int i = 0; i < studyWorkflowToggles.Count; i++)
        {
            if (studyWorkflowToggles[i].IsToggled)
            {
                selectedStudyWorkflowIndices.Add(i);
            }
        }

        selectStudyWorkflowsButton.enabled = (selectedStudyWorkflowIndices.Count == numOfStudyWorkflows);
    }

    // Label order toggles with names of selected workflows in respective presentation order
    public void LabelStudyOrders()
    {
        List<StatefulInteractable> orderToggles = studyWorkflowOrderCollection.Toggles;
        for (int i = 0; i < orderToggles.Count && i < studyOrders.Length; i++)
        {
            string label = string.Join(" > ", studyOrders[i].Select(pos => workflowNames[selectedStudyWorkflowIndices[pos]]));
            orderToggles[i].GetComponentInChildren<TMP_Text>().text = label;
        }
    }

    // Arrange selected study workflows in chosen presentation order
    public void SetStudyOrder(int orderIndex)
    {
        sessionWorkflowNames.Clear();
        sessionWorkflowPaths.Clear();
        foreach (int pos in studyOrders[orderIndex])
        {
            int workflowIndex = selectedStudyWorkflowIndices[pos];
            sessionWorkflowNames.Add(workflowNames[workflowIndex]);
            sessionWorkflowPaths.Add(workflowPaths[workflowIndex]);
        }

        Log.Msg($"Study order {orderIndex + 1}: {string.Join(" > ", sessionWorkflowNames)}");

        // Study mode starts with workflows shown one after another
        phase = GuidancePhase.Sequential;
    }

    // Use fixed tutorial workflows for this session (study mode, familiarization with interface before actual study). Returns whether tutorial data is complete
    public bool SetTutorialSession()
    {
        sessionWorkflowNames.Clear();
        sessionWorkflowPaths.Clear();

        string tutorialPath = Path.Combine(Application.streamingAssetsPath, TutorialFolderName);
        if (!Directory.Exists(tutorialPath))
        {
            Log.Msg($"Tutorial data not found at {tutorialPath}");
            return false;
        }

        // Tutorial workflows are presented in alphabetical order of their folder names (A, B, C)
        foreach (string dir in Directory.GetDirectories(tutorialPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            sessionWorkflowNames.Add(new DirectoryInfo(dir).Name);
            sessionWorkflowPaths.Add(dir);
        }

        if (sessionWorkflowPaths.Count != numOfStudyWorkflows)
        {
            Log.Msg($"Tutorial requires {numOfStudyWorkflows} workflows, found {sessionWorkflowPaths.Count}");
            return false;
        }

        Log.Msg($"Tutorial: {string.Join(" > ", sessionWorkflowNames)}");

        // Tutorial simulates study mode, starting with workflows shown one after another
        phase = GuidancePhase.Sequential;
        return true;
    }

    // Use single selected workflow for this session (regular guidance)
    public void SetSingleWorkflow(int workflowIndex)
    {
        sessionWorkflowNames.Clear();
        sessionWorkflowPaths.Clear();
        sessionWorkflowNames.Add(workflowNames[workflowIndex]);
        sessionWorkflowPaths.Add(workflowPaths[workflowIndex]);

        phase = GuidancePhase.Regular;
    }

    // Load data of all workflows of this session into memory. Returns whether every workflow contains at least one step
    public async Task<bool> LoadSession()
    {
        sets.Clear();
        activeSet = 0;
        currentStepNumber = 0;

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Load workflows in parallel on background threads (file access and json parsing only, no other Unity API)
        WorkflowData[] workflows = await Task.WhenAll(sessionWorkflowPaths.Select(path => Task.Run(() => LoadWorkflowData(path))));
        sets.AddRange(workflows);

        // Log problems on main thread (Log.Msg creates UI elements on device)
        foreach (WorkflowData workflow in sets)
        {
            foreach (string warning in workflow.warnings)
            {
                Log.Msg(warning);
            }
        }
        Log.Msg($"Loaded {sets.Count} workflow(s) with {string.Join("/", sets.Select(w => w.steps.Count))} steps in {stopwatch.ElapsedMilliseconds} ms");

        return sets.Count > 0 && sets.All(w => w.steps.Count > 0);
    }

    // Read and parse all data of a single workflow. Runs on background thread, therefore only file access and JsonUtility (no other Unity API, no Log.Msg)
    private WorkflowData LoadWorkflowData(string workflowPath)
    {
        WorkflowData workflow = new()
        {
            name = new DirectoryInfo(workflowPath).Name,
            path = workflowPath
        };

        try
        {
            // Folders containing data for each step
            string[] unsortedPaths = Directory.GetDirectories(workflowPath);

            // Numerically sort folders by extracting the number after "step_"
            // Example: "step_2" -> 2, "step_10" -> 10
            string[] stepFoldersPaths = unsortedPaths.OrderBy(path =>
            {
                // Get just the folder name (e.g., "step_10")
                string folderName = new DirectoryInfo(path).Name;

                // Extract the numeric part (assuming format is "step_X")
                // Replace "step_" with "" to get just the number
                string numberString = folderName.Replace("step_", "");

                // Parse to integer for correct numerical sorting
                if (int.TryParse(numberString, out int number))
                {
                    return number;
                }

                // Fallback for folders that don't match the pattern
                return int.MaxValue;
            }).ToArray();

            foreach (string stepPath in stepFoldersPaths)
            {
                workflow.steps.Add(LoadStepData(stepPath, workflow));
            }
        }
        catch (Exception ex)
        {
            workflow.warnings.Add($"Error loading workflow data of {workflow.name}: {ex.Message}");
        }

        return workflow;
    }

    // Read and parse all data of a single step. Runs on background thread (see LoadWorkflowData)
    private StepData LoadStepData(string stepPath, WorkflowData workflow)
    {
        StepData step = new() { path = stepPath };
        string stepName = $"{workflow.name}/{new DirectoryInfo(stepPath).Name}";

        // Panel pose
        try
        {
            string panelPath = Path.Combine(stepPath, "panel.json");
            if (File.Exists(panelPath))
            {
                step.panelPose = JsonUtility.FromJson<SerializableInstructionPanelPose>(File.ReadAllText(panelPath));
            }
        }
        catch (Exception e)
        {
            workflow.warnings.Add($"Failed to load positional data of instruction panel ({stepName}): {e.Message}");
        }

        // Text instruction from manual authoring
        try
        {
            string textPath = Path.Combine(stepPath, "text.txt");
            if (File.Exists(textPath))
            {
                step.text = File.ReadAllText(textPath);
            }
        }
        catch (Exception e)
        {
            workflow.warnings.Add($"Failed to load text instruction ({stepName}): {e.Message}");
        }

        // In-situ instructions from manual authoring
        try
        {
            string manual3dPath = Path.Combine(stepPath, "3d.json");
            if (File.Exists(manual3dPath))
            {
                step.manualSet = JsonUtility.FromJson<SerializableInsituInstructionSet>(File.ReadAllText(manual3dPath));
            }
        }
        catch (Exception e)
        {
            workflow.warnings.Add($"Failed to load manual 3d instructions ({stepName}): {e.Message}");
        }

        // Text and in-situ instructions from assisted authoring (incl. handtracking data for in-situ hands)
        try
        {
            string instructionsPath = Path.Combine(stepPath, "instructions.json");
            if (File.Exists(instructionsPath))
            {
                step.autoSet = JsonUtility.FromJson<SerializableInsituInstructionSet>(File.ReadAllText(instructionsPath));
                step.text = step.autoSet.textInstruction;

                string handsPath = Path.Combine(stepPath, "hands.json");
                if (File.Exists(handsPath))
                {
                    step.handtrackingData = JsonUtility.FromJson<HandTrackingDataset>(File.ReadAllText(handsPath));
                }
                else
                {
                    workflow.warnings.Add($"No handtracking data for assisted instructions ({stepName})");
                }
            }
        }
        catch (Exception e)
        {
            workflow.warnings.Add($"Failed to load assisted instructions ({stepName}): {e.Message}");
        }

        // Video (prefer trimmed video instruction)
        string videoPath = Path.Combine(stepPath, "trimmed_video.mp4");
        if (!File.Exists(videoPath))
        {
            videoPath = Path.Combine(stepPath, "video.mp4");
        }
        if (File.Exists(videoPath))
        {
            step.videoPath = videoPath;
        }
        else
        {
            workflow.warnings.Add($"No video instruction ({stepName})");
        }

        return step;
    }

    // Show current step of active workflow. All data is already in memory (see LoadSession), only video is streamed from file
    public void ShowStep()
    {
        // Set step number labels (instruction panel and handmenu)
        stepNumberLabel.text = (currentStepNumber + 1).ToString();
        hmStepNumberLabel.text = stepNumberLabel.text;
        // Get data of current step
        StepData step = sets[activeSet].steps[currentStepNumber];

        // Panel position
        try
        {
            if (phase != GuidancePhase.Regular)
            {
                // Study mode: same panel position for all steps and workflows (stored poses are incomplete), gravity aligned above qr-code
                UIGuidance.instance.instructionView.transform.position = QRGuidance.instance.qrTransform.position + Vector3.up * studyPanelHeight;

                // Face user once (rotation around y-axis only, see MakeObjFaceUser on instruction panel)
                UIGuidance.instance.instructionView.GetComponent<MakeObjFaceUser>().enabled = true;
            }
            else if (step.panelPose != null)
            {
                // Set pose of instruction panel
                // Cache previous parent of instruction panel
                Transform previousParent = UIGuidance.instance.instructionView.transform.parent;

                // Attach to qr-code reference
                UIGuidance.instance.instructionView.transform.SetParent(QRGuidance.instance.qrTransform);
                UIGuidance.instance.instructionView.transform.SetLocalPositionAndRotation(step.panelPose.position, step.panelPose.rotation);

                // Reparent instruction panel
                UIGuidance.instance.instructionView.transform.SetParent(previousParent, true);
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to set pose of instruction panel: {e.Message}");
        }

        // Text (cleared if missing so no text of another step or workflow remains)
        instructionText.text = step.text ?? "";
        textInstructionPanel.SetActive(true);

        // Video
        try
        {
            if (step.videoPath != null)
            {
                // Display video instruction to user
                videoImage.texture = targetVideoTexture;
                videoPlayer.url = step.videoPath;

                // Prepare video player
                StartCoroutine(PrepareVideoForPlayback(videoPlayer));

                // Enable this modality's instruction panel
                videoInstructionPanel.SetActive(true);
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to show video instruction: {e.Message}");
        }

        // 3D instructions
        try
        {
            // Clear any existing 3D instructions and hand animation
            ClearInsituInstructions();

            // Manually authored in-situ instructions
            if (step.manualSet != null)
            {
                SpawnManual3d(step.manualSet);
            }

            // Semi-automatically authored in-situ instructions
            if (step.autoSet != null)
            {
                SpawnAuto3d(step.autoSet, step.handtrackingData);
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to show 3d instructions: {e.Message}");
        }
    }

    // Remove instructions of current step from scene (e.g. while transition dialog between workflows is shown)
    public void ClearStep()
    {
        ClearInsituInstructions();
        videoPlayer.Stop();
    }

    // Destroy instantiated 3d instructions and stop any ongoing hand animation
    private void ClearInsituInstructions()
    {
        if (instantiated3dObjs != null)
        {
            foreach (var obj in instantiated3dObjs)
            {
                Destroy(obj);
            }
        }
        instantiated3dObjs = new();

        ResetVisualization(false);
    }

    // Whether current step is last step of active workflow
    public bool IsLastStep()
    {
        return currentStepNumber >= sets[activeSet].steps.Count - 1;
    }

    // Whether another workflow follows active workflow in this session
    public bool HasNextSet()
    {
        return activeSet < sets.Count - 1;
    }

    // Switch to given workflow of this session while keeping current step number
    public void SelectSet(int setIndex)
    {
        activeSet = setIndex;
        currentStepNumber = Mathf.Min(currentStepNumber, sets[activeSet].steps.Count - 1);
        SyncSwitches();
    }

    // Mark active workflow on both switch menus (instruction panel and handmenu). Toggle collections ignore selections while inactive,
    // therefore also called when handmenu content gets shown (onFirstHandDetected of right handmenu)
    public void SyncSwitches()
    {
        if (switchToggles.isActiveAndEnabled)
        {
            switchToggles.SetSelection(activeSet);
        }
        if (hmSwitchToggles.isActiveAndEnabled)
        {
            hmSwitchToggles.SetSelection(activeSet);
        }
    }

    // Method spawns 3d instructions authored using manual methods
    public void SpawnManual3d(SerializableInsituInstructionSet manualSet)
    {
        try
        {
            // Instruction set of this step
            iSet = manualSet;

            // Extract instruction types stored in instructionType string (possibly multiple in case of manual authoring)
            string[] instructionTypes = iSet.instructionType.Split(" ");

            // Get reference base (qr code)
            Transform refBase = QRGuidance.instance.qrTransform;

            // Iterate over instructions and instantiate them
            for (int i = 0; i < instructionTypes.Length; i++)
            {
                // Get position, rotation, and scale of this 3d instruction
                Vector3 pos = iSet.objPositions[i];
                Quaternion rot = iSet.objRotations[i];
                Vector3 scale = iSet.objScales[i];

                // Instantiate 3d instruction depending on type
                GameObject obj = null;
                if (instructionTypes[i] == "straightArrow")
                {
                    obj = Instantiate(straightArrowPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                else if (instructionTypes[i] == "curvedArrow")
                {
                    obj = Instantiate(curvedArrowPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                else if (instructionTypes[i] == "circularArrow")
                {
                    obj = Instantiate(circularArrowPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                //else if (instructionTypes[i] == "hammer")
                //{
                //    obj = Instantiate(hammerPrefab);
                //    obj.transform.SetParent(refBase);
                //    obj.transform.SetLocalPositionAndRotation(pos, rot);
                //    obj.transform.localScale = scale;
                //    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                //    {
                //        renderer.material = colorDict[iSet.objColors[i]];
                //    }
                //}
                //else if (instructionTypes[i] == "pliers")
                //{
                //    obj = Instantiate(pliersPrefab);
                //    obj.transform.SetParent(refBase);
                //    obj.transform.SetLocalPositionAndRotation(pos, rot);
                //    obj.transform.localScale = scale;
                //    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                //    {
                //        renderer.material = colorDict[iSet.objColors[i]];
                //    }
                //}
                else if (instructionTypes[i] == "allen")
                {
                    obj = Instantiate(allenPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                else if (instructionTypes[i] == "wrench")
                {
                    obj = Instantiate(wrenchPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                else if (instructionTypes[i] == "screwdriver")
                {
                    obj = Instantiate(screwdriverPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.material = colorDict[iSet.objColors[i]];
                    }
                }
                else if (instructionTypes[i] == "openHand")
                {
                    obj = Instantiate(openHandPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    obj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], colorDict[iSet.objColors[i]], obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
                }
                else if (instructionTypes[i] == "pointingHand")
                {
                    obj = Instantiate(pointHandPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    obj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], colorDict[iSet.objColors[i]], obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
                }
                else if (instructionTypes[i] == "pickingHand")
                {
                    obj = Instantiate(pickHandPrefab);
                    obj.transform.SetParent(refBase);
                    obj.transform.SetLocalPositionAndRotation(pos, rot);
                    obj.transform.localScale = scale;
                    obj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], colorDict[iSet.objColors[i]], obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
                }

                // Disable object manipulator, bounding boxes, and colliders of 3d instructions
                if (obj != null)
                {
                    var manipulator = obj.GetComponent<ObjectManipulator>();
                    if (manipulator != null) manipulator.enabled = false;

                    var collider = obj.GetComponentInChildren<BoxCollider>();
                    if (collider != null) collider.enabled = false;

                    // Add instantiated 3d instruction to list for later deactivation
                    instantiated3dObjs.Add(obj);
                }
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Error loading manual 3D data: {e.Message}");
        }
    }

    //Method spawns 3d instructions authored using semi-automatic methods
    public void SpawnAuto3d(SerializableInsituInstructionSet autoSet, HandTrackingDataset hands)
    {
        try
        {
            // Instruction set and handtracking data of this step (used by in-situ hands and hand animation)
            iSet = autoSet;
            handtrackingData = hands;

            // Get reference base (qr code)
            Transform refBase = QRGuidance.instance.qrTransform;

            // Load instruction type
            string instructionType = iSet.instructionType;
            Log.Msg($"Instruction type for this step: {instructionType}");

            // Different handling depending on instruction type
            GameObject obj = null;
            if (instructionType == "press button")
            {
                // Spawn static hand instruction at specified key point in time
                SpawnInsituHand(iSet.keyTime, iSet.leftUsed, iSet.rightUsed, iSet.objPositions[0], iSet.objRotations[0]);
            }
            else if (instructionType == "rotate switch")
            {
                // Consists of only a single insitu instruction
                obj = Instantiate(circularArrowPrefab);
                obj.transform.SetParent(refBase);
                obj.transform.SetLocalPositionAndRotation(iSet.objPositions[0], iSet.objRotations[0]);
                obj.transform.localScale = iSet.objScales[0];

                var manipulator = obj.GetComponent<ObjectManipulator>();
                if (manipulator != null) manipulator.enabled = false;

                var collider = obj.GetComponentInChildren<BoxCollider>();
                if (collider != null) collider.enabled = false;

                instantiated3dObjs.Add(obj);
            }
            else if (instructionType == "pick and place")
            {
                // Spawn static hand instruction at specified start time (pick)
                SpawnInsituHand(iSet.startTime, iSet.leftUsed, iSet.rightUsed, iSet.objPositions[0], iSet.objRotations[0]);

                // Spawn straight arrow (place)
                obj = Instantiate(straightArrowPrefab);
                obj.transform.SetParent(refBase);
                obj.transform.SetLocalPositionAndRotation(iSet.objPositions[1], iSet.objRotations[1]);
                obj.transform.localScale = iSet.objScales[1];

                var manipulator = obj.GetComponent<ObjectManipulator>();
                if (manipulator != null) manipulator.enabled = false;

                var collider = obj.GetComponentInChildren<BoxCollider>();
                if (collider != null) collider.enabled = false;

                instantiated3dObjs.Add(obj);
            }
            else if (instructionType == "open/close")
            {
                // Playback hand animation
                handAnimRoutine = StartCoroutine(AnimateHandsLooped(iSet.startTime, iSet.endTime));
            }
            else if (instructionType == "use Allen")
            {
                // Spawn static hand instruction at specified start time (pick)
                SpawnInsituHand(iSet.startTime, iSet.leftUsed, iSet.rightUsed, iSet.objPositions[0], iSet.objRotations[0]);

                // Spawn tool (engage)
                obj = Instantiate(allenPrefab);
                obj.transform.SetParent(refBase);
                obj.transform.SetLocalPositionAndRotation(iSet.objPositions[1], iSet.objRotations[1]);
                obj.transform.localScale = iSet.objScales[1];

                var manipulator = obj.GetComponent<ObjectManipulator>();
                if (manipulator != null) manipulator.enabled = false;

                var collider = obj.GetComponentInChildren<BoxCollider>();
                if (collider != null) collider.enabled = false;

                instantiated3dObjs.Add(obj);
            }
            else if (instructionType == "use wrench")
            {
                // Spawn static hand instruction at specified start time (pick)
                SpawnInsituHand(iSet.startTime, iSet.leftUsed, iSet.rightUsed, iSet.objPositions[0], iSet.objRotations[0]);

                // Spawn tool (engage)
                obj = Instantiate(wrenchPrefab);
                obj.transform.SetParent(refBase);
                obj.transform.SetLocalPositionAndRotation(iSet.objPositions[1], iSet.objRotations[1]);
                obj.transform.localScale = iSet.objScales[1];

                var manipulator = obj.GetComponent<ObjectManipulator>();
                if (manipulator != null) manipulator.enabled = false;

                var collider = obj.GetComponentInChildren<BoxCollider>();
                if (collider != null) collider.enabled = false;

                instantiated3dObjs.Add(obj);
            }
            else if (instructionType == "use screwdriver")
            {
                // Spawn static hand instruction at specified start time (pick)
                SpawnInsituHand(iSet.startTime, iSet.leftUsed, iSet.rightUsed, iSet.objPositions[0], iSet.objRotations[0]);

                // Spawn tool (engage)
                obj = Instantiate(screwdriverPrefab);
                obj.transform.SetParent(refBase);
                obj.transform.SetLocalPositionAndRotation(iSet.objPositions[1], iSet.objRotations[1]);
                obj.transform.localScale = iSet.objScales[1];

                var manipulator = obj.GetComponent<ObjectManipulator>();
                if (manipulator != null) manipulator.enabled = false;

                var collider = obj.GetComponentInChildren<BoxCollider>();
                if (collider != null) collider.enabled = false;

                instantiated3dObjs.Add(obj);
            }
            else if (instructionType == "other")
            {
                // Playback hand animation
                handAnimRoutine = StartCoroutine(AnimateHandsLooped(iSet.startTime, iSet.endTime));
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Error loading auto 3D data: {e.Message}");
        }
    }

    // Method to spawn in-situ hand based on handtracking data and specific point in time
    public void SpawnInsituHand(float pvFrameTime, bool leftUsed, bool rightUsed, Vector3 wristPos, Quaternion wristRot)
    {
        // Get handframe closest to target point in time
        float handFrameTime = handtrackingData.frames[frameIterator].timestamp;
        // Stop at last frame if target time exceeds handtracking data
        while (pvFrameTime > handFrameTime && frameIterator < handtrackingData.frames.Count - 1)
        {
            frameIterator++;
            handFrameTime = handtrackingData.frames[frameIterator].timestamp;
        }

        // Check if previous or next iterator closer to target time
        if (frameIterator > 0)
        {
            float prevhandFrameTime = handtrackingData.frames[frameIterator - 1].timestamp;
            if (Mathf.Abs(pvFrameTime - prevhandFrameTime) < Mathf.Abs(pvFrameTime - handFrameTime))
            {
                frameIterator--;
                handFrameTime = prevhandFrameTime;
            }
        }

        // Target handframe
        FrameData frame = handtrackingData.frames[frameIterator];

        // Get reference base (qr code)
        Transform refBase = QRGuidance.instance.qrTransform;

        // Left hand was used
        if (leftUsed)
        {
            // Instantiate hand object relative to QR-Code
            GameObject obj = Instantiate(leftHand, refBase, false);
            obj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Local array for rigged joints of cloned hand
            Transform[] cloneLeftJointsArray = new Transform[(int)TrackedHandJoint.TotalJoints];

            // Initialize the rigged visual joints array for instantiated hand (left). 2nd child is armature
            int index = (int)TrackedHandJoint.Wrist;
            List<Transform> leftJointsTransforms = obj.transform.GetChild(1).transform.GetComponentsInChildren<Transform>().ToList();
            for (int i = 0; i < leftJointsTransforms.Count; i++)
            {
                // Skip joint end tips
                if (leftJointsTransforms[i].name.Contains("end"))
                {
                    continue;
                }

                //leftRiggedVisualJointsArray[index] = leftJointsTransforms[i];
                cloneLeftJointsArray[index] = leftJointsTransforms[i];
                index++;
            }

            // Set hand pose based on handtracking data
            for (int i = 0; i < 26; i++)
            {
                // Pose data
                SerializableHandJointPose sLeftJointPose = frame.jointPosesLeft[i];

                // Joint transform to be set
                //Transform leftJointTransform = leftRiggedVisualJointsArray[i];
                Transform leftJointTransform = cloneLeftJointsArray[i];

                switch ((TrackedHandJoint)i)
                {
                    case TrackedHandJoint.Palm:
                        // Don't track the palm. The hand mesh shouldn't have a "palm bone".
                        break;
                    case TrackedHandJoint.Wrist:
                        // Set wrist pose
                        leftJointTransform.SetPositionAndRotation(sLeftJointPose.position, sLeftJointPose.rotation);

                        break;
                    case TrackedHandJoint.ThumbTip:
                    case TrackedHandJoint.IndexTip:
                    case TrackedHandJoint.MiddleTip:
                    case TrackedHandJoint.RingTip:
                    case TrackedHandJoint.LittleTip:
                        // The tip bone uses the joint rotation directly.
                        leftJointTransform.rotation = frame.jointPosesLeft[i - 1].rotation;
                        leftJointTransform.rotation = frame.jointPosesLeft[i - 1].rotation;

                        break;
                    case TrackedHandJoint.ThumbMetacarpal:
                    case TrackedHandJoint.IndexMetacarpal:
                    case TrackedHandJoint.MiddleMetacarpal:
                    case TrackedHandJoint.RingMetacarpal:
                    case TrackedHandJoint.LittleMetacarpal:
                        // Special case metacarpals, because Wrist is not always i-1.
                        // This is the same "simple IK" as the default case, but with special index logic.
                        leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - frame.jointPosesLeft[(int)MixedReality.Toolkit.TrackedHandJoint.Wrist].position, new Pose(sLeftJointPose.position, sLeftJointPose.rotation).up);

                        break;
                    default:
                        // For all other bones, do a simple "IK" from the rigged joint to the joint data's position.
                        leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - leftJointTransform.position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);

                        break;
                }
            }

            // Transform wrist into reference coords (based on potentially modified pose stored in serialized instruction data)
            //leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(wristPos, wristRot);
            cloneLeftJointsArray[1].transform.SetLocalPositionAndRotation(wristPos, wristRot);

            // Enable left hand instruction
            obj.SetActive(true);

            // Add instantiated 3d instruction to list for later deactivation
            instantiated3dObjs.Add(obj);
        }

        // Right hand was used
        if (rightUsed)
        {
            // Instantiate hand object relative to QR-Code
            GameObject obj = Instantiate(rightHand, refBase, false);
            obj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Local array for rigged joints of cloned hand
            Transform[] clonedRightJointsArray = new Transform[(int)TrackedHandJoint.TotalJoints];

            // Initialize the rigged visual joints array for instantiated hand (right). 2nd child is armature
            int index = (int)TrackedHandJoint.Wrist;
            List<Transform> rightJointsTransforms = obj.transform.GetChild(1).transform.GetComponentsInChildren<Transform>().ToList();
            for (int i = 0; i < rightJointsTransforms.Count; i++)
            {
                // Skip joint end tips
                if (rightJointsTransforms[i].name.Contains("end"))
                {
                    continue;
                }

                //rightRiggedVisualJointsArray[index] = rightJointsTransforms[i];
                clonedRightJointsArray[index] = rightJointsTransforms[i];
                index++;
            }

            // Set hand pose based on handtracking data
            for (int i = 0; i < 26; i++)
            {
                // Pose data
                SerializableHandJointPose sRightJointPose = frame.jointPosesRight[i];

                // Joint transform to be set
                Transform rightJointTransform = clonedRightJointsArray[i];

                switch ((TrackedHandJoint)i)
                {
                    case TrackedHandJoint.Palm:
                        // Don't track the palm. The hand mesh shouldn't have a "palm bone".
                        break;
                    case TrackedHandJoint.Wrist:
                        // Set wrist pose
                        rightJointTransform.SetPositionAndRotation(sRightJointPose.position, sRightJointPose.rotation);

                        break;
                    case TrackedHandJoint.ThumbTip:
                    case TrackedHandJoint.IndexTip:
                    case TrackedHandJoint.MiddleTip:
                    case TrackedHandJoint.RingTip:
                    case TrackedHandJoint.LittleTip:
                        // The tip bone uses the joint rotation directly.                      
                        rightJointTransform.rotation = frame.jointPosesRight[i - 1].rotation;
                        rightJointTransform.rotation = frame.jointPosesRight[i - 1].rotation;

                        break;
                    case TrackedHandJoint.ThumbMetacarpal:
                    case TrackedHandJoint.IndexMetacarpal:
                    case TrackedHandJoint.MiddleMetacarpal:
                    case TrackedHandJoint.RingMetacarpal:
                    case TrackedHandJoint.LittleMetacarpal:
                        // Special case metacarpals, because Wrist is not always i-1.
                        // This is the same "simple IK" as the default case, but with special index logic.
                        rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - frame.jointPosesRight[(int)MixedReality.Toolkit.TrackedHandJoint.Wrist].position, new Pose(sRightJointPose.position, sRightJointPose.rotation).up);

                        break;
                    default:
                        // For all other bones, do a simple "IK" from the rigged joint to the joint data's position.
                        rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - rightJointTransform.position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);

                        break;
                }
            }

            // Transform wrist into reference coords
            //rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(wristPos, wristRot);
            clonedRightJointsArray[1].transform.SetLocalPositionAndRotation(wristPos, wristRot);
        
            // Enable right hand instruction
            obj.SetActive(true);

            // Add instantiated 3d instruction to list for later deactivation
            instantiated3dObjs.Add(obj);
        }
    }

    // Handle next and prev navigation button behavior
    public void ConfigureNavButtons()
    {
        // In first study phase, last step of a workflow leads to next workflow (or to comparison phase)
        bool canProceed = !IsLastStep() || phase == GuidancePhase.Sequential;
        nextStepButton.enabled = canProceed;
        hmNextStepButton.enabled = canProceed;
        previousStepButton.enabled = (currentStepNumber > 0);
        hmPrevStepButton.enabled = (currentStepNumber > 0);
    }

    #endregion


    #region Video

    public IEnumerator PrepareVideoForPlayback(VideoPlayer vp)
    {
        // Wait one frame (to correctly update thumbnail)
        yield return null;

        // Prepare video player
        vp.Prepare();

        // Set preview to first frame (0 not valid)
        vp.Play();
        //vp.frame = 1;
        //vp.Pause();

        // Configure video player
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = false;
        //vp.loopPointReached += VideoEnded;
        vp.isLooping = true;
    }

    // Callback for playing video
    public void PlayVideo()
    {
        // Hide play button
        playButton.SetActive(false);

        // Replay captured video (handtracking is synced automatically)
        videoPlayer.Play();
    }

    // Callback for when video ended (both authoring and review)
    public void VideoEnded(VideoPlayer vp)
    {
        //Log.Msg("End of video reached");

        // Reset preview to first frame
        vp.Play();
        vp.frame = 1;
        vp.Pause();

        // Reset hand playback
        ResetVisualization(false);

        // Show play button
        //playButton.SetActive(true);
    }

    // Callback for when ready frame caught
    public void CaughtReadyFrame(VideoPlayer vp, long frameReady)
    {
        //Log.Msg($"Caught frame {frameReady}");

        // Disregard first frame as it is only used for thumbnail and doesn't contain critical handtracking data
        if (frameReady == 1)
        {
            //Log.Msg("Caught first frame");

            return;
        }

        // Visualize single handtracking frame
        VisualizeHandFrame(frameReady, vp.time);
    }

    #endregion


    #region Handtracking

    // Prepare hand visualization
    public void PrepareHandVisualization()
    {
        // Reset replay frame to 0
        frameIterator = 0;

        // Attach hands to qrcode (reference system)
        Transform refBase = QRGuidance.instance.qrTransform;
        leftHand.transform.SetParent(refBase);
        leftHand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rightHand.transform.SetParent(refBase);
        rightHand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Disable hands by default
        leftHand.SetActive(false);
        rightHand.SetActive(false);

        //Log.Msg("Prepared hands for visualization");
    }

    // Visualize captured handtracking data
    public void VisualizeHandFrame(long videoFrame, double videoTime)
    {
        //Log.Msg($"Video at frame {videoFrame} and time {videoTime}");

        if (handtrackingData != null)
        {
            if (frameIterator < handtrackingData.frames.Count)
            {
                // Get temporally closest handtracking frame
                float frameTime = handtrackingData.frames[frameIterator].timestamp;
                while (videoTime + syncOffset > frameTime)
                {
                    frameIterator++;
                    frameTime = handtrackingData.frames[frameIterator].timestamp;
                }

                //Log.Msg($"VTime: {videoTime}, FTime: {frameTime}, FI: {frameIterator}");

                // Get data frame
                //FrameData frame = handtrackingDataset.frames[(int)videoFrame];
                FrameData frame = handtrackingData.frames[frameIterator - 1];

                // Make sure not visualizing null data
                if (frame != null)
                {
                    // Check if frame is to be visualized (non-zero)
                    if (!frame.isValidLeft && !frame.isValidRight)
                    {
                        // Zero frame data

                        // Disable hands if not already
                        if (leftHand.activeSelf)
                        {
                            leftHand.SetActive(false);
                        }

                        if (rightHand.activeSelf)
                        {
                            rightHand.SetActive(false);
                        }

                        // Skip visualization
                        //Log.Msg($"Skipped visualization because hand invalid. FI: {frameIterator}");
                        return;
                    }
                    else
                    {
                        // Non-zero frame data (either or both left, right)

                        // Enable/Disable hands depending on whether handtracking data valid
                        if (frame.isValidLeft)
                        {
                            if (!leftHand.activeSelf)
                            {
                                leftHand.SetActive(true);
                            }
                        }
                        else
                        {
                            if (leftHand.activeSelf)
                            {
                                leftHand.SetActive(false);
                            }
                        }

                        if (frame.isValidRight)
                        {
                            if (!rightHand.activeSelf)
                            {
                                rightHand.SetActive(true);
                            }
                        }
                        else
                        {
                            if (rightHand.activeSelf)
                            {
                                rightHand.SetActive(false);
                            }
                        }

                        // Set hands based on frame data
                        for (int i = 0; i < 26; i++)
                        {
                            // Pose data
                            SerializableHandJointPose sLeftJointPose = frame.jointPosesLeft[i];
                            SerializableHandJointPose sRightJointPose = frame.jointPosesRight[i];

                            // Joint transform to be set
                            Transform leftJointTransform = leftRiggedVisualJointsArray[i];
                            Transform rightJointTransform = rightRiggedVisualJointsArray[i];

                            switch ((TrackedHandJoint)i)
                            {
                                case TrackedHandJoint.Palm:
                                    // Don't track the palm. The hand mesh shouldn't have a "palm bone".
                                    break;
                                case TrackedHandJoint.Wrist:
                                    // Set wrist pose
                                    leftJointTransform.SetPositionAndRotation(sLeftJointPose.position, sLeftJointPose.rotation);
                                    rightJointTransform.SetPositionAndRotation(sRightJointPose.position, sRightJointPose.rotation);

                                    break;
                                case TrackedHandJoint.ThumbTip:
                                case TrackedHandJoint.IndexTip:
                                case TrackedHandJoint.MiddleTip:
                                case TrackedHandJoint.RingTip:
                                case TrackedHandJoint.LittleTip:
                                    // The tip bone uses the joint rotation directly.
                                    leftJointTransform.rotation = frame.jointPosesLeft[i - 1].rotation;
                                    leftJointTransform.rotation = frame.jointPosesLeft[i - 1].rotation;
                                    rightJointTransform.rotation = frame.jointPosesRight[i - 1].rotation;
                                    rightJointTransform.rotation = frame.jointPosesRight[i - 1].rotation;

                                    break;
                                case TrackedHandJoint.ThumbMetacarpal:
                                case TrackedHandJoint.IndexMetacarpal:
                                case TrackedHandJoint.MiddleMetacarpal:
                                case TrackedHandJoint.RingMetacarpal:
                                case TrackedHandJoint.LittleMetacarpal:
                                    // Special case metacarpals, because Wrist is not always i-1.
                                    // This is the same "simple IK" as the default case, but with special index logic.
                                    leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - frame.jointPosesLeft[(int)MixedReality.Toolkit.TrackedHandJoint.Wrist].position, new Pose(sLeftJointPose.position, sLeftJointPose.rotation).up);
                                    rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - frame.jointPosesRight[(int)MixedReality.Toolkit.TrackedHandJoint.Wrist].position, new Pose(sRightJointPose.position, sRightJointPose.rotation).up);

                                    break;
                                default:
                                    // For all other bones, do a simple "IK" from the rigged joint to the joint data's position.
                                    leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - leftJointTransform.position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);
                                    rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - rightJointTransform.position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);

                                    break;
                            }
                        }

                        // Transform wrist into reference coords
                        leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.leftWrist.position, frame.leftWrist.rotation);
                        rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.rightWrist.position, frame.rightWrist.rotation);

                        //Log.Msg($"Set joints poses for frame {frameIterator}");
                    }
                }
                else
                {
                    //Log.Msg("Null-Frame");
                }
            }
            else
            {
                //Log.Msg($"All frames from handtracking dataset shown. Frameiterator: {frameIterator}");
            }
        }
        else
        {
            //Log.Msg("Empty handtracking dataset");
        }
    }

    // Terminate any ongoing hand visualization
    public void ResetVisualization(bool resetPos)
    {
        // Stop hand animation if ongoing
        if (handAnimRoutine != null)
        {
            StopCoroutine(handAnimRoutine);
            handAnimRoutine = null;
        }

        // Reset frame iterator
        frameIterator = 0;

        if (resetPos)
        {
            // Attach back to handracking manager (this)
            leftHand.transform.SetParent(transform);
            rightHand.transform.SetParent(transform);
        }

        // Disable hand visuals
        leftHand.SetActive(false);
        rightHand.SetActive(false);

        //Log.Msg("Reset hand visualization");
    }

    // Visualize single handtracking data frame (e.g. for animation)
    private void VisualizeHTFrame(float frameTime)
    {
        //Log.Msg($"Video at frame {videoFrame} and time {videoTime}");

        if (handtrackingData != null)
        {
            // Make sure not to exceed container limits (0 because we always needs 2 frames for interpolation)
            if (frameIterator < handtrackingData.frames.Count || frameIterator == 0)
            {
                // Get temporally closest handtracking frame
                float handTime = handtrackingData.frames[frameIterator].timestamp;
                // Stop at last frame if frame time exceeds handtracking data
                while (handTime <= frameTime && frameIterator < handtrackingData.frames.Count - 1)
                {
                    frameIterator++;
                    handTime = handtrackingData.frames[frameIterator].timestamp;
                }

                //// Also cache hand time before current selection so we have 2 values for interpolation
                //float prevHandTime = handtrackingDataset.frames[frameIterator - 1].timestamp;
                //// Calculate interpolation factor
                //float t = (frameTime - prevHandTime) / (handTime - prevHandTime);
                //// Get previous data frame
                //FrameData prevFrame = handtrackingDataset.frames[frameIterator - 1];

                // Get data frame
                FrameData frame = handtrackingData.frames[frameIterator];

                // Set hands based on frame data
                Transform refBase = QRGuidance.instance.qrTransform;

                // Target and offset values for wrist transformation (left and right)
                Vector3 finalWristPosL = animOffsetPosL + (animOffsetRotL * frame.leftWrist.position);
                Quaternion finalWristRotL = animOffsetRotL * frame.leftWrist.rotation;
                Vector3 finalWristPosR = animOffsetPosR + (animOffsetRotR * frame.rightWrist.position);
                Quaternion finalWristRotR = animOffsetRotR * frame.rightWrist.rotation;

                // Generate delta rotation to correct offset (left and right)
                Quaternion deltaRotL = Quaternion.identity;
                if (frame.isValidLeft) deltaRotL = (refBase.rotation * finalWristRotL) * Quaternion.Inverse(frame.jointPosesLeft[1].rotation);
                Quaternion deltaRotR = Quaternion.identity;
                if (frame.isValidRight) deltaRotR = (refBase.rotation * finalWristRotR) * Quaternion.Inverse(frame.jointPosesRight[1].rotation);

                // Iterate through joints
                for (int i = 0; i < 26; i++)
                {
                    // Pose data
                    SerializableHandJointPose sLeftJointPose = frame.jointPosesLeft[i];
                    SerializableHandJointPose sRightJointPose = frame.jointPosesRight[i];

                    // Joint transform to be set
                    Transform leftJointTransform = leftRiggedVisualJointsArray[i];
                    Transform rightJointTransform = rightRiggedVisualJointsArray[i];

                    switch ((TrackedHandJoint)i)
                    {
                        case TrackedHandJoint.Palm:
                        case TrackedHandJoint.Wrist:
                            // Wrist will be repositioned later via SetLocalPositionAndRotation
                            break;
                        case TrackedHandJoint.ThumbTip:
                        case TrackedHandJoint.IndexTip:
                        case TrackedHandJoint.MiddleTip:
                        case TrackedHandJoint.RingTip:
                        case TrackedHandJoint.LittleTip:
                            leftJointTransform.rotation = deltaRotL * frame.jointPosesLeft[i - 1].rotation;
                            rightJointTransform.rotation = deltaRotR * frame.jointPosesRight[i - 1].rotation;
                            break;
                        case TrackedHandJoint.ThumbMetacarpal:
                        case TrackedHandJoint.IndexMetacarpal:
                        case TrackedHandJoint.MiddleMetacarpal:
                        case TrackedHandJoint.RingMetacarpal:
                        case TrackedHandJoint.LittleMetacarpal:
                            leftJointTransform.rotation = deltaRotL * Quaternion.LookRotation(sLeftJointPose.position - frame.jointPosesLeft[1].position, new Pose(sLeftJointPose.position, sLeftJointPose.rotation).up);
                            rightJointTransform.rotation = deltaRotR * Quaternion.LookRotation(sRightJointPose.position - frame.jointPosesRight[1].position, new Pose(sRightJointPose.position, sRightJointPose.rotation).up);
                            break;
                        default:
                            leftJointTransform.rotation = deltaRotL * Quaternion.LookRotation(sLeftJointPose.position - frame.jointPosesLeft[i - 1].position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);
                            rightJointTransform.rotation = deltaRotR * Quaternion.LookRotation(sRightJointPose.position - frame.jointPosesRight[i - 1].position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);
                            break;
                    }
                }

                // Transform wrist relative bounds directly (offset applied!)
                leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(finalWristPosL, finalWristRotL);
                rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(finalWristPosR, finalWristRotR);

                //Log.Msg($"Set joints poses for frame {frameIterator}");
            }
        }
        else
        {
            //Log.Msg("Empty handtracking dataset (Visualize)");
        }
    }

    // Coroutine for animating hand
    public IEnumerator AnimateHandsLooped(float startTime, float endTime)
    {
        // Prepare hands for animation
        PrepareHandVisualization();

        if (handtrackingData == null)
        {
            //Log.Msg("Handtracking dataset is NULL! Aborting animation");
            yield break;
        }

        // Calculate animation offset once
        animOffsetPosL = Vector3.zero; animOffsetRotL = Quaternion.identity;
        animOffsetPosR = Vector3.zero; animOffsetRotR = Quaternion.identity;

        if (iSet != null && handtrackingData.frames.Count > 0)
        {
            // Take first frame for offset calculation
            FrameData keyFrame = handtrackingData.frames[0];
            float minDelta = float.MaxValue;
            foreach (var f in handtrackingData.frames)
            {
                float d = Mathf.Abs(f.timestamp - iSet.keyTime);
                if (d < minDelta) { minDelta = d; keyFrame = f; }
            }

            // Left hand offset
            if (iSet.leftUsed && iSet.objPositions.Count > 0)
            {
                animOffsetRotL = iSet.objRotations[0] * Quaternion.Inverse(keyFrame.leftWrist.rotation);
                animOffsetPosL = iSet.objPositions[0] - (animOffsetRotL * keyFrame.leftWrist.position);
            }

            // Right hand offset
            if (iSet.rightUsed)
            {
                int rIdx = iSet.leftUsed ? 1 : 0;
                if (iSet.objPositions.Count > rIdx)
                {
                    animOffsetRotR = iSet.objRotations[rIdx] * Quaternion.Inverse(keyFrame.rightWrist.rotation);
                    animOffsetPosR = iSet.objPositions[rIdx] - (animOffsetRotR * keyFrame.rightWrist.position);
                }
            }
        }

        while (true)
        {
            // Set animation time to start time
            float animTime = startTime;
            // Reset frame iterator used in determining closes hand frame
            frameIterator = 0;

            // Visually reset hand as well
            VisualizeHTFrame(animTime);

            // Show hands
            leftHand.SetActive(iSet.leftUsed);
            rightHand.SetActive(iSet.rightUsed);

            // Wait before starting animation
            yield return new WaitForSecondsRealtime(animWaitTime);

            // Animate within identified key frame time
            while (animTime <= endTime)
            {
                // Calculate time since capture start 
                animTime += Time.deltaTime * handAnimSpeedFactor;

                // Visualize next hand frame
                VisualizeHTFrame(animTime);

                yield return null;
            }
        }
    }


    #endregion
}

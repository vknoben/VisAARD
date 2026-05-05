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
using System.IO;
using System;
using MixedReality.Toolkit.SpatialManipulation;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

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

    [Tooltip("Name of selected workflow to guide user")]
    public string selectedWorkflowName;
    [Tooltip("Path to selected workflow data")]
    public string selectedWorkflowPath;
#if ENABLE_WINMD_SUPPORT
    [Tooltip("Folder of selected workflow")]
    StorageFolder selectedWorkflowFolder;
    [Tooltip("List of folders containing data for steps of selected workflow")]
    IReadOnlyList<StorageFolder> stepFolders;
#endif
    [Tooltip("List of folders containing data for steps of selected workflow (System.IO version)")]
    public string[] stepFoldersPaths;
    [Tooltip("Number of steps of selected workflow")]
    public int numOfSteps;
    [Tooltip("Step number of currently shown instructions. Starts at 0")]
    public int currentStepNumber = 0;

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
    [Tooltip("No workflows yet text display")]
    public GameObject noWorkflowsText;
    [Tooltip("Collection of toggles for selecting workflow")]
    public ToggleCollection workflowToggles;
    [Tooltip("Select workflow button")]
    public PressableButton selectWorkflowButton;

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

                            // Skip "Speech" folder created by MRTK's Speech Command system
                            if (dirInfo.Name.Equals("Speech", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // Vermeide doppelte Workflows (falls gleiche Namen in beiden Ordnern existieren)
                            if (workflowNames.Contains(dirInfo.Name))
                            {
                                continue;
                            }

                            // Create list element
                            GameObject listElement = Instantiate(workflowListElementPrefab, workflowList);

                            // Set label to workflow name
                            listElement.GetComponentInChildren<TMP_Text>().text = dirInfo.Name;

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

    //// List available workflows to user
    //public void ListAvailableWorkflows()
    //{
    //    // Check if any workflows available at all
    //    if (PlayerPrefs.HasKey("keys") == true)
    //    {
    //        // At least 1 workflow available

    //        // Get all keys (structure: "key_name_1 key_name_2 ..."; key names represent folder names for each workflow)
    //        string[] keys = PlayerPrefs.GetString("keys").Split(" ");

    //        //Log.Msg($"Found {keys.Length} existing workflows");

    //        // Create list entry for each key
    //        foreach (string key in keys)
    //        {
    //            // Create list element
    //            GameObject listElement = Instantiate(workflowListElementPrefab, workflowList);

    //            // Set label to workflow name
    //            listElement.GetComponentInChildren<TMP_Text>().text = key;

    //            // Store workflow names and paths
    //            workflowNames.Add(key);
    //            workflowPaths.Add(PlayerPrefs.GetString(key));
    //        }    

    //        // Update/Enable toggle collection
    //        workflowToggles.enabled = true;

    //        // Enable select button 
    //        selectWorkflowButton.enabled = true;
    //    }
    //    else
    //    {
    //        // No workflow has been created yet
    //        noWorkflowsText.SetActive(true);
    //    }
    //}

    // Load data for selected workflow
    public async Task LoadWorkflow(int workflowIndex)
    {
        // Selected workflow name and path
        selectedWorkflowName = workflowNames[workflowIndex];
        selectedWorkflowPath = workflowPaths[workflowIndex];

        try
        {
            // Folders containing data for each step
            string[] unsortedPaths = Directory.GetDirectories(selectedWorkflowPath);

            // Numerically sort folders by extracting the number after "step_"
            // Example: "step_2" -> 2, "step_10" -> 10
            stepFoldersPaths = unsortedPaths.OrderBy(path =>
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

            // Number of steps (equal to number of folders)
            numOfSteps = stepFoldersPaths.Length;

            // Set initial step to 0
            currentStepNumber = 0;

            //Log.Msg($"Selected workflow consists of {numOfSteps} steps");
        }
        catch (Exception ex)
        {
            Log.Msg($"Error loading workflow data: {ex.Message}");
            numOfSteps = 0;
            currentStepNumber = 0;
        }
    }

    // Load specific step
    public async Task LoadStep()
    {
        // Set step number label
        stepNumberLabel.text = (currentStepNumber + 1).ToString();
        // Get relevant path for current step data
        string currentStepPath = stepFoldersPaths[currentStepNumber];

        // Load panel position
        try
        {
            string panelPath = Path.Combine(currentStepPath, "panel.json");

            if (File.Exists(panelPath))
            {
                // Read json data
                string json = await File.ReadAllTextAsync(panelPath);

                // Deserialize json data
                SerializableInstructionPanelPose panelPose = JsonUtility.FromJson<SerializableInstructionPanelPose>(json);

                // Set pose of instruction panel
                // Cache previous parent of instruction panel
                Transform previousParent = UIGuidance.instance.instructionView.transform.parent;

                // Attach to qr-code reference
                UIGuidance.instance.instructionView.transform.SetParent(QRGuidance.instance.qrTransform);
                UIGuidance.instance.instructionView.transform.SetLocalPositionAndRotation(panelPose.position, panelPose.rotation);

                // Reparent instruction panel
                UIGuidance.instance.instructionView.transform.SetParent(previousParent, true);

                //Log.Msg($"Successfully loaded instruction panel pose for step {currentStepNumber}");
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to load positional data of instruction panel: {e.Message}");
        }

        // Load text
        try
        {
            // Text instruction from manual authoring
            string textPath = Path.Combine(currentStepPath, "text.txt");
            if (File.Exists(textPath))
            {
                // Get textual instruction
                string text = await File.ReadAllTextAsync(textPath);

                // Display textual instruction to user
                instructionText.text = text;

                // Enable this modality's instruction panel
                textInstructionPanel.SetActive(true);

                //Log.Msg($"Successfully loaded textual data for step {currentStepNumber}");
            }

            // Text instructions from assisted authoring
            string instructionsPath = Path.Combine(currentStepPath, "instructions.json");
            if (File.Exists(instructionsPath))
            {
                // Get instruction json data
                string json = await File.ReadAllTextAsync(instructionsPath);

                // Deserialize json
                iSet = JsonUtility.FromJson<SerializableInsituInstructionSet>(json);

                // Get text instruction field
                instructionText.text = iSet.textInstruction;

                // Enable this modality's instruction panel
                textInstructionPanel.SetActive(true);
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to load text instruction: {e.Message}");
        }

        // Video
        try
        {
            string videoPath = Path.Combine(currentStepPath, "trimmed_video.mp4");

            // First: Try get trimmed video instruction
            if (!File.Exists(videoPath))
            {
                // No trimmed instruction exists, try default
                videoPath = Path.Combine(currentStepPath, "video.mp4");
            }

            if (File.Exists(videoPath))
            {
                // Display video instruction to user
                videoImage.texture = targetVideoTexture;
                videoPlayer.url = videoPath;

                // Prepare video player
                StartCoroutine(PrepareVideoForPlayback(videoPlayer));

                // Enable this modality's instruction panel
                videoInstructionPanel.SetActive(true);

                //Log.Msg($"Successfully loaded video data for step {currentStepNumber}");
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to load video instruction: {e.Message}");
        }

        // 3D instructions
        try
        {
            // Clear any existing 3D instructions
            if (instantiated3dObjs != null)
            {
                foreach (var obj in instantiated3dObjs)
                {
                    Destroy(obj);
                }
            }
            instantiated3dObjs = new();

            // Stop any ongoing hand animation
            ResetVisualization(false);

            // Check if manually authored in-situ instructions or semi-automatically authored ones available for this step
            string manual3dPath = Path.Combine(currentStepPath, "3d.json");
            if (File.Exists(manual3dPath))
            {
                Log.Msg("Found manually authored 3d instructions for this step");
                // Load manually authored 3d instructions
                await LoadManual3d(manual3dPath);
            }

            // Check if semi-automatically authored instructions exist
            string autoInstructionsPath = Path.Combine(currentStepPath, "instructions.json");
            if (File.Exists(autoInstructionsPath))
            {
                Log.Msg("Found semi-automatically authored 3d instructions for this step");
                // Load semi-automatically authored 3d instructions
                await LoadAuto3d(currentStepPath);
            }
        }
        catch (Exception e)
        {
            Log.Msg($"Failed to load 3d instructions or no instructions available for this step: {e.Message}");
        }
    }

    // Method loads 3d instructions authored using manual methods
    public async Task LoadManual3d(string filePath)
    {
        try
        {
            // Deserialize
            string json = await File.ReadAllTextAsync(filePath);

            // Deserialize json data
            iSet = JsonUtility.FromJson<SerializableInsituInstructionSet>(json);

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

    //Method loads 3d instructions authored using semi-automatic methods
    public async Task LoadAuto3d(string filePath)
    {
        try
        {
            // Get reference base (qr code)
            Transform refBase = QRGuidance.instance.qrTransform;

            // Load instruction type
            string instructionType = iSet.instructionType;
            Log.Msg($"Instruction type for this step: {instructionType}");

            // Load handtracking data if animated instruction set
            string handsPath = Path.Combine(filePath, "hands.json");

            if (File.Exists(handsPath))
            {
                try
                {
                    // Get handtracking data
                    string hJson = await File.ReadAllTextAsync(handsPath);

                    // Deserialize json data
                    handtrackingData = JsonUtility.FromJson<HandTrackingDataset>(hJson);
                }
                catch (Exception e)
                {
                    Log.Msg($"Failed to load hand-based instruction: {e.Message}");
                    throw;
                }
            }

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
        while (pvFrameTime > handFrameTime)
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
                Transform rightJointTransform = rightRiggedVisualJointsArray[i];

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
        nextStepButton.enabled = (numOfSteps > currentStepNumber + 1);
        hmNextStepButton.enabled = (numOfSteps > currentStepNumber + 1);
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
                while (handTime <= frameTime)
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


                            // Interpolate between previous and current wrist positions and rotations
                            //Vector3 interpolatedPosLeft = Vector3.Lerp(prevFrame.leftWrist.position, frame.leftWrist.position, t);
                            //Quaternion interpolatedRotLeft = Quaternion.Slerp(prevFrame.leftWrist.rotation, frame.leftWrist.rotation, t);
                            //Vector3 interpolatedPosRight = Vector3.Lerp(prevFrame.rightWrist.position, frame.rightWrist.position, t);
                            //Quaternion interpolatedRotRight = Quaternion.Slerp(prevFrame.rightWrist.rotation, frame.rightWrist.rotation, t);

                            //// Move wrist to next pose
                            //leftJointTransform.SetLocalPositionAndRotation(interpolatedPosLeft, interpolatedRotLeft);
                            //rightJointTransform.transform.SetLocalPositionAndRotation(interpolatedPosRight, interpolatedRotRight);

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

                //// Interpolate between previous and current wrist positions and rotations
                //Vector3 interpolatedPosLeft = Vector3.Lerp(prevFrame.leftWrist.position, frame.leftWrist.position, t);
                //Quaternion interpolatedRotLeft = Quaternion.Slerp(prevFrame.leftWrist.rotation, frame.leftWrist.rotation, t);
                //Vector3 interpolatedPosRight = Vector3.Lerp(prevFrame.rightWrist.position, frame.rightWrist.position, t);
                //Quaternion interpolatedRotRight = Quaternion.Slerp(prevFrame.rightWrist.rotation, frame.rightWrist.rotation, t);

                //// Move wrist to next pose
                ////leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.leftWrist.position, frame.leftWrist.rotation);
                ////rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.rightWrist.position, frame.rightWrist.rotation);
                //leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(interpolatedPosLeft, interpolatedRotLeft);
                //rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(interpolatedPosRight, interpolatedRotRight);
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

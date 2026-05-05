using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;
using Msg;
using Serializable3dInstructions;
using SerializableHandtracking;
using SerializableInstructions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

namespace SerializableInstructions
{
    [Tooltip("Class used to represent a set of instructions for a single work step. Used in semi-automatic authoring mode to manage in-situ authoring during runtime (memory-based)")]
    public class InstructionSet
    {
        // Following properties are provided by the author
        public int stepNumber;                          // Step number in the workflow
        public string videoInstructionPath;             // Path to video instruction for step
        public HandTrackingDataset handtrackingData;    // Complete handtracking dataset for step

        // Following properties are determined based on VLM-result
        public string instructionType;                  // Instruction/action type ("pick and place", "open/close", "press button", "rotate switch", "use Allen")
        public string textInstruction;                  // Textual instruction for step
        public bool leftUsed;                           // Whether left hand was used in this step
        public bool rightUsed;                          // Whether right hand was used in this step
        public bool isAnimated;                         // Whether this step is animated (e.g. for "open/close")
        public float startTime;                         // Start time for potential animation
        public float endTime;                           // End time for potential animation
        public float keyTime;                           // Key time for extraction of static moment (e.g. "press button", "open/close")

        public List<GameObject> insituObjects;          // List of GameObjects for this step (e.g. "pick and place" consists of 2 objects)

        // Constructor
        public InstructionSet(string vidPath)
        {
            stepNumber = 0;
            videoInstructionPath = vidPath;
            handtrackingData = new HandTrackingDataset();

            instructionType = "";
            textInstruction = "";
            leftUsed = false;
            rightUsed = false;
            isAnimated = false;
            startTime = 0f;
            endTime = 0f;
            keyTime = 0f;
            insituObjects = new List<GameObject>();
        }
    }

    [Serializable, Tooltip("Serializable class for in-situ instructions. Contains all additional data which needs to be saved to disk for reload on use. Used for both manual and auomatic authoring when storing in-situ instructions on disk")]
    public class SerializableInsituInstructionSet
    {
        public string instructionType;                  // Instruction/action type (e.g. "pick and place", "open/close", "press button", "rotate switch")
        public string textInstruction;                  // Textual instruction for step
        public bool leftUsed;                           // Whether left hand was used in this step
        public bool rightUsed;                          // Whether right hand was used in this step
        public bool isAnimated;                         // Whether this step is animated (e.g. for "open/close")
        public float startTime;                         // Start time for potential animation
        public float endTime;                           // End time for potential animation
        public float keyTime;                           // Key time for extraction of static moment (e.g. "press button", "open/close")
        public List<Vector3> objPositions;              // List of positions (relative to qr-code) for in-situ objects 
        public List<Quaternion> objRotations;           // List of rotations for in-situ objects 
        public List<Vector3> objScales;                 // List of scales for in-situ objects
        public List<string> objColors;                  // List of colors for in-situ objects (Only relevant for manually authored in-situ instructions)    

        // Constructors

        // For semi-automatic authored in-situ instructions (multiple objects with one type per set)
        public SerializableInsituInstructionSet(InstructionSet instructionSet)
        {
            instructionType = instructionSet.instructionType;
            textInstruction = instructionSet.textInstruction;
            leftUsed = instructionSet.leftUsed;
            rightUsed = instructionSet.rightUsed;
            isAnimated = instructionSet.isAnimated;
            startTime = instructionSet.startTime;
            endTime = instructionSet.endTime;
            keyTime = instructionSet.keyTime;
            objPositions = new List<Vector3>();
            objRotations = new List<Quaternion>();
            objScales = new List<Vector3>();
            objColors = new List<string>();

            foreach (var obj in instructionSet.insituObjects)
            {
                // Transform into qr-code reference frame
                Vector3 pos = QRAuth.instance.qrTransform.InverseTransformPoint(obj.transform.position);
                Quaternion rot = Quaternion.Inverse(QRAuth.instance.qrTransform.rotation) * obj.transform.rotation;
                Vector3 scale = obj.transform.localScale;

                // Take armature pose in case of static hand instructions (2nd child of hand object)
                if (obj.CompareTag("hand"))
                {
                    // Is static hand instruction (necessary if user modifies pose!)
                    pos = QRAuth.instance.qrTransform.InverseTransformPoint(obj.transform.GetChild(1).position);
                    rot = Quaternion.Inverse(QRAuth.instance.qrTransform.rotation) * obj.transform.GetChild(1).rotation;
                    scale = obj.transform.GetChild(1).localScale;
                }

                // Add to serializable object
                objPositions.Add(pos);
                objRotations.Add(rot);
                objScales.Add(scale);
            }
        }

        // For manually authored in-situ instructions (multiple objects with different types per set)
        public SerializableInsituInstructionSet(List<Instruction3d> instructions)
        {
            // Set instruction type to concatenated string of all types in the set (e.g. "straightArrow" + "allen")
            instructionType = string.Join(" ", instructions.Select(i => i.type));

            // Initialize lists
            objPositions = new List<Vector3>();
            objRotations = new List<Quaternion>();
            objScales = new List<Vector3>();
            objColors = new List<string>();

            // Set pose and color data in order
            foreach (var i in instructions)
            {
                objPositions.Add(i.position);
                objRotations.Add(i.rotation);
                objScales.Add(i.scale);
                objColors.Add(i.color);
            }

            // Set other fields to default values
            leftUsed = false;
            rightUsed = false;
            isAnimated = false;
            startTime = 0f;
            endTime = 0f;
            keyTime = 0f;
        }
    }

    [Serializable, Tooltip("Serializable class for instruction panel pose")]
    public class SerializableInstructionPanelPose
    {
        public Vector3 position;                     // Position of instruction panel relative to qr-code
        public Quaternion rotation;                  // Rotation of instruction panel relative to qr-code

        // Constructor
        public SerializableInstructionPanelPose(Transform panelTransform)
        {
            position = QRAuth.instance.qrTransform.InverseTransformPoint(panelTransform.position);
            rotation = Quaternion.Inverse(QRAuth.instance.qrTransform.rotation) * panelTransform.rotation;
        }
    }
}

public class InstructionManager : MonoBehaviour
{
    public static InstructionManager Instance { get; private set; }

    #region Properties

    [Tooltip("Queue of instruction sets accumulated for workflow")]
    public Queue<InstructionSet> instructionSets = new Queue<InstructionSet>();
    [Tooltip("Queue of incomplete instruction sets (missing VLM data)")]
    public Queue<InstructionSet> incompleteInstructionSets = new Queue<InstructionSet>();
    [Tooltip("Currently authored instruction set (text, video, hand tracking data). Also used to point to currently displayed instruction set")]
    public InstructionSet currentInstructionSet = null;

    [Tooltip("Total number of instruction sets and thus steps in the workflow")]
    public int totalStepCount = 0;

    [Tooltip("Default offset of text and video instructions (step preview/in-situ authoring panels) relative to in-situ instruction")]
    public Vector3 textVideoInstructionOffset = new Vector3(0.3f, 0.7f, 0.35f);

    // Following properties are provided by the author
    public string VideoPath
    {
        set
        {
            // Create a new instruction set passing in video path
            currentInstructionSet = new InstructionSet(value);

            // Add current instruction set to list of incomplete instruction sets
            incompleteInstructionSets.Enqueue(currentInstructionSet);

            // Increase step count
            totalStepCount++;
            currentInstructionSet.stepNumber = totalStepCount;
            //Log.Msg($"Created new instruction set for step {currentInstructionSet}");

            // Disable start button for control loop dialog by default so that in-situ authoring can begin (gets enabled if in-situ instruction generated). Signals that not all in-situ instructions have been generated yet
            //UIAuth.instance.startControlLoopButton.enabled = false;

            //Log.Msg($"Set video path for step {currentInstructionSet.stepNumber}");
        }
    }
    public HandTrackingDataset HandTrackingData
    {
        set
        {
            // Set hand tracking data for the current instruction set
            currentInstructionSet.handtrackingData = value;

            //Log.Msg($"Completed text, video, handtracking for step {currentInstructionSet.stepNumber}. Added to incomplete list");
        }
    }

    // Following properties are determined based on VLM-result
    public string InstructionType
    {
        set
        {
            // Set instruction type for the current instruction set
            incompleteInstructionSets.Peek().instructionType = value;
            //Log.Msg($"Set instruction type for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public string TextInstruction
    {
        set
        {
            // Set text instruction for current instruction set
            incompleteInstructionSets.Peek().textInstruction = value;
            //Log.Msg($"Set generated text instruction for step {currentInstructionSet.stepNumber}: {value}");
        }
    }
    public bool LeftUsed
    {
        set
        {
            // Set left hand usage flag for earliest incomplete instruction set
            incompleteInstructionSets.Peek().leftUsed = value;

            //Log.Msg($"Set left hand usage for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public bool RightUsed
    {
        set
        {
            // Set right hand usage flag for earliest incomplete instruction set
            incompleteInstructionSets.Peek().rightUsed = value;

            //Log.Msg($"Set right hand usage for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public bool IsAnimated
    {
        set
        {
            // Set animation flag for ealiest incomplete instruction set
            incompleteInstructionSets.Peek().isAnimated = value;

            //Log.Msg($"Set animation flag for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public float StartTime
    {
        set
        {
            // Set start time for earliest incomplete instruction set
            incompleteInstructionSets.Peek().startTime = value;

            //Log.Msg($"Set start time for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public float EndTime
    {
        set
        {
            // Set end time for earliest incomplete instruction set
            incompleteInstructionSets.Peek().endTime = value;

            //Log.Msg($"Set end time for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public float KeyTime
    {
        set
        {
            // Set key time for earliest incomplete instruction set
            incompleteInstructionSets.Peek().keyTime = value;
            //Log.Msg($"Set key time for step {incompleteInstructionSets.Peek().stepNumber}: {value}");
        }
    }
    public List<GameObject> InsituObjects
    {
        set
        {
            // Set insitu objects for earliest incomplete instruction set
            incompleteInstructionSets.Peek().insituObjects = value;

            // Remove the completed instruction set from the queue
            InstructionSet iSet = incompleteInstructionSets.Dequeue();

            // Add now complete instruction set to the list of instruction sets
            instructionSets.Enqueue(iSet);

            //Log.Msg($"Set in-situ objects for step {iSet.stepNumber}. Finished generation of this in-situ instruction set");

            // Signals that last in-situ instruction set has been generated
            if (incompleteInstructionSets.Count == 0)
            {
                if (UIAuth.instance.aStartInsituDialog.activeSelf)
                {
                    // Play sound
                    UIAuth.instance.aStartInsituDialog.GetComponent<AudioSource>().Play();
                    // Enable start review loop button
                    UIAuth.instance.startControlLoopButton.enabled = true;

                }
            }
        }
    }

    [Tooltip("Delete 3D instruction UI panel (static instructions)")]
    public GameObject delete3dUI;
    [Tooltip("Delete 3D instruction UI panel (animated instructions)")]
    public GameObject deleteAnimated3dUI;
    [Tooltip("Index of object to be deleted. Passed on select")]
    public int objToDeleteIndex;
    //[Tooltip("Indices of objects that were deleted so they're not considered during saving")]
    //public List<int> deletedObjsIndices;

    #endregion


    #region Methods

    // Method to show generated in-situ instructions for next step during in-situ authoring (loading from memory)
    public void ShowInstructionSet(bool showNext = true)
    {
        // Is this method called when moving to next step in review or when regenerating same step?
        if (showNext)
        {
            // Disable any in-situ static or animated instructions
            RemoveCurrentInsituandText();

            // Get next set in instruction set queue
            currentInstructionSet = instructionSets.Dequeue();

            // Also set current workflow folder (important in case of redoing demonstration, so that new video is saved in right place)
#if ENABLE_WINMD_SUPPORT
            WorkflowManager.instance.currentStepFolder = WorkflowManager.instance.stepFolders[currentInstructionSet.stepNumber - 1];
#endif
            // Inform client that we're moving to next step in review loop
            if (WebSocketClient.instance.connected)
            {
                WebSocketClient.instance.SendMsg(MsgType.NEWSTEPINSITU, currentInstructionSet.stepNumber.ToString());
            }
        }

        // Position step preview (text and video instruction panel) relative to object of interest
        // Object of interest to set step preview relative to (in case of hand, use armature as reference as it represents wrist)
        Transform refObj = null;
        switch (currentInstructionSet.instructionType)
        {
            case "pick and place":
                // Use pick location (hand)
                refObj = currentInstructionSet.insituObjects[0].transform.GetChild(1);

                break;
            case "press button":
                // Use press location (hand)
                refObj = currentInstructionSet.insituObjects[0].transform.GetChild(1);

                break;
            case "rotate switch":
                // Use rotate location (circular arrow)
                refObj = currentInstructionSet.insituObjects[0].transform;
                break;
            case "use Allen":
                // Use Allen pick location (hand)
                refObj = currentInstructionSet.insituObjects[0].transform.GetChild(1);

                break;
            case "open/close":
                // Use grab location (hand)
                refObj = currentInstructionSet.insituObjects[0].transform.GetChild(1);

                break;
            default:
                // Use init hand location
                refObj = currentInstructionSet.insituObjects[0].transform.GetChild(1);
                break;
        }

        // Determine local offset (for x and z only, y should be gravity aligned and thus global)
        //Vector3 localOffset = refObj.transform.right * textVideoInstructionOffset.x + refObj.transform.forward * textVideoInstructionOffset.z;
        // Determine global offset (for y only)
        Vector3 globalOffset = Vector3.up * textVideoInstructionOffset.y;

        // Apply offsets (only y for now. Orientation messed up)
        UIAuth.instance.aStepPreview.transform.position = refObj.position /*+ localOffset*/ + globalOffset;
        //Log.Msg("Positioned panels automatically");

        // Enable face user constraint for step preview panel
        var faceUserComp = UIAuth.instance.aStepPreview.GetComponent<MakeObjFaceUser>();
        faceUserComp.enabled = true;

        // Display current step number
        UIAuth.instance.aStepNumberOnPreview.text = $"{currentInstructionSet.stepNumber}";

        // Load textual instruction
        AuthorText.instance.AssistedPopulatePreviewWithSpecified(currentInstructionSet.textInstruction);

        // Load video instruction (only necessary if loading next step. Regeneration keeps same video, redo loads video before result received already)
        if (showNext)
        {
            AuthorVideo.instance.ResetOngoingPlaybackAssisted(AuthorVideo.instance.aVideoPlayerReview);
            AuthorVideo.instance.PopulateVideoPreview(currentInstructionSet.videoInstructionPath, AuthorVideo.instance.aVideoPlayerReview);
        }

        // Enable panel arrangement events (again)
        var arrangeComp = UIAuth.instance.aStepPreview.GetComponentInChildren<DidManipulate>();
        arrangeComp.enabled = true;
        arrangeComp.didArrange = false;

        // Enable text and video instruction panel
        UIAuth.instance.aStepPreview.SetActive(true);

        // Static vs. animated instructions
        if (currentInstructionSet.instructionType != "open/close" && currentInstructionSet.instructionType != "other")
        {
            // Load (automatically generated) 3D instructions from memory
            for (int i = 0; i < currentInstructionSet.insituObjects.Count; i++)
            {
                GameObject iso = currentInstructionSet.insituObjects[i];

                // Pass reference to InstructionManager to object
                iso.GetComponentInChildren<DeletableSelectable>().instructionManager = Instance;
                //Log.Msg("Passed InstructionManager as ref to in-situ instruction");

                // Set object name to count index for reference (e.g. deletion). Make sure to take object with comp on it (important in case of hand where child armature is named based on index)
                iso.GetComponentInChildren<DeletableSelectable>().gameObject.name = i.ToString();
                //Log.Msg("Set name of in-situ instruction to " + i.ToString());

                // Disable StatefulInteractable and enable ObjectManipulator by default
                var interactableComps = iso.GetComponentsInChildren<StatefulInteractable>();
                foreach (var comp in interactableComps)
                {
                    if (comp.GetType() == typeof(ObjectManipulator))
                    {
                        comp.enabled = true;
                        //Log.Msg("Enabled ObjectManipulator by default");

                        // Add script which checks if object was manipulated and informs client (study purposes)
                        var didManComp = comp.gameObject.AddComponent<DidManipulate>();
                        ObjectManipulator omComp = comp as ObjectManipulator;
                        omComp.firstSelectEntered.AddListener((args) => didManComp.WasManipulated());
                    }
                    else if (comp.GetType() == typeof(StatefulInteractable))
                    {
                        comp.enabled = false;
                        //Log.Msg("Disabled StatefulInteractable by default");
                    }
                }

                iso.SetActive(true);
            }
        }
        else
        {
            // Start animation between start and end time provided by VLM-result and cache coroutine
            HandtrackingManager.instance.StartHandAnimation(currentInstructionSet.startTime, currentInstructionSet.endTime);
        }

        // Make dirctional indicator point to instruction panel
        UIAuth.instance.visualCue.DirectionalTarget = UIAuth.instance.aStepPreview.transform;
        UIAuth.instance.visualCue.gameObject.SetActive(true);

        //Log.Msg($"Showing instruction set for step {currentInstructionSet.stepNumber}");
    }

    // Make in-situ instructions manipulable/editable
    public void Make3dEditable()
    {
        // Get relevant in-situ objects
        foreach (var obj in currentInstructionSet.insituObjects)
        {
            // Get ObjectManipulator and StatefulInteractable comps
            var interactableComps = obj.GetComponentsInChildren<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                comp.enabled = false;
            }
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = false;
                    //Log.Msg("Disabled StatefulInteractable");
                }
                else if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = true;
                    //Log.Msg("Enabled ObjectManipulator");
                }
            }
        }
    }

    // Make in-situ instructions deletable
    public void Make3dDeletable()
    {
        // Get relevant in-situ objects
        foreach (var obj in currentInstructionSet.insituObjects)
        {
            // Get ObjectManipulator and StatefulInteractable comps
            var interactableComps = obj.GetComponentsInChildren<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                comp.enabled = false;
            }
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                    //Log.Msg("Disabled ObjectManipulator");
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = true;
                    //Log.Msg("Enabled StatefulInteractable");
                }
            }
        }
    }

    // Delete selected in-situ instruction
    public void Delete3d()
    {
        // Remove existing references and destroy/hide object
        GameObject objToDelete = currentInstructionSet.insituObjects[objToDeleteIndex];
        //currentInstructionSet.insituObjects.RemoveAt(objToDeleteIndex);
        //Destroy(objToDelete);
        objToDelete.SetActive(false);

        // Store index of object to delete for later exclusion during save
        //deletedObjsIndices.Add(objToDeleteIndex);

        // Check if any 3D instructions active (if not: prevent user from continuing)
        if (currentInstructionSet.insituObjects.Count(obj => obj != null && obj.activeInHierarchy) == 0)
        {
            // Prevent user from moving on to next step or finishing in-situ authoring
            UIAuth.instance.aNextStepInsituButton.enabled = false;
        }
    }

    // Delete animated in-situ instruction
    public void DeleteAnimated3d()
    {
        // Disable any ongoing hand animation (animated)
        HandtrackingManager.instance.ResetHands(false);
        //Log.Msg("Reset hand animation");

        // Check if any 3D instructions active (if not: prevent user from continuing)
        if (currentInstructionSet.insituObjects.Count(obj => obj != null && obj.activeInHierarchy) == 0)
        {
            // Prevent user from moving on to next step or finishing in-situ authoring
            UIAuth.instance.aNextStepInsituButton.enabled = false;
        }

        // Update information of this instruction set
        currentInstructionSet.isAnimated = false;

        // Add indices of static placeholder in-situ hands (used for spatial placement) for later disconsideration during saving. If left or right used: First object needs to be deleted; If both: first two (since animation will always be up front, authored by system)
        //if (currentInstructionSet.leftUsed)
        //{
        //    deletedObjsIndices.Add(0);
        //}
        //if (currentInstructionSet.rightUsed)
        //{
        //    deletedObjsIndices.Add(1);
        //}
    }

    // Method to save in-situ instruction data to disk
    public async Task SaveInsituInstructions()
    {
        // Make sure only saving if there's is stuff to save
        if (currentInstructionSet.insituObjects.Count == 0)
        {
            return;
        }

#if ENABLE_WINMD_SUPPORT
        // Remove previously deleted objects so they're not considered for saving. Sort and reverse to remove elements without index error
        //if (deletedObjsIndices.Count > 0)
        //{
        //    deletedObjsIndices.Sort();
        //    deletedObjsIndices.Reverse();
        //    foreach (int i in deletedObjsIndices)
        //    {
        //        currentInstructionSet.insituObjects.RemoveAt(i);
        //    }
        //}

        // Create serializable in-situ instruction set
        SerializableInsituInstructionSet serializableInsituInstructionSet = new SerializableInsituInstructionSet(currentInstructionSet);

        // Create json from serializable in-situ instruction set
        string json = JsonUtility.ToJson(serializableInsituInstructionSet, true);

        // Create json file
        string fileName = "instructions.json";
        StorageFile file = await WorkflowManager.instance.stepFolders[currentInstructionSet.stepNumber - 1].CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        // Write data to file
        await FileIO.WriteTextAsync(file, json);

        //Log.Msg($"Saved in-situ instruction(s) for step {currentInstructionSet.stepNumber}");

        // Also save instruction panel pose
        await SaveInstructionPanelPose();
#endif
    }

    // Method to save step preview (text and video instruction panel) pose to disk
    public async Task SaveInstructionPanelPose()
    {
#if ENABLE_WINMD_SUPPORT
        // Create serializable instruction panel pose
        SerializableInstructionPanelPose serializableInstructionPanelPose = new SerializableInstructionPanelPose(UIAuth.instance.aStepPreview.transform);

        // Create json from serializable instruction panel pose
        string json = JsonUtility.ToJson(serializableInstructionPanelPose, true);
        StorageFile file = await WorkflowManager.instance.stepFolders[currentInstructionSet.stepNumber - 1].CreateFileAsync("panel.json", CreationCollisionOption.ReplaceExisting);

        // Write data to file
        await FileIO.WriteTextAsync(file, json);

        //Log.Msg($"Saved instruction panel pose for step {currentInstructionSet.stepNumber}");
#endif
    }

    // Remove currently displayed in-situ and text instructions
    public void RemoveCurrentInsituandText()
    {
        // Disable any previous 3D instructions (static)
        if (currentInstructionSet != null && currentInstructionSet.insituObjects != null)
        {
            currentInstructionSet.insituObjects.ForEach(obj => obj.SetActive(false));
            //Log.Msg("Disabled previous in-situ objects");
        }

        // Disable any ongoing hand animation (animated)
        HandtrackingManager.instance.ResetHands(false);
        //Log.Msg("Reset hand animation");

        // Remove text (relevant for regeneration only)
        // Load textual instruction
        AuthorText.instance.AssistedPopulatePreviewWithSpecified("Waiting for new result...");
    }

    #endregion


    #region Unity lifecycle

    void Awake()
    {
        // Make sure only one instance of this class exists
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    #endregion
}

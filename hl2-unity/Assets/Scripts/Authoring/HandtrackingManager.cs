using UnityEngine;
using System.Collections.Generic;
using MixedReality.Toolkit.Subsystems;
using MixedReality.Toolkit;
using UnityEngine.XR;
using System;
using SerializableHandtracking;
using System.Linq;
using MixedReality.Toolkit.SpatialManipulation;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.MixedReality.OpenXR;


#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

namespace SerializableHandtracking
{
    [System.Serializable]
    public class SerializableHandJointPose
    {
        // Pos, rot, and radius (only if scaling)
        public Vector3 position;
        public Quaternion rotation;
        public float radius;

        // Constructor
        public SerializableHandJointPose(HandJointPose pose)
        {
            position = pose.Position;
            rotation = pose.Rotation;
            radius = pose.Radius;
        }
    }

    [System.Serializable]
    public class FrameData
    {
        // Flag whether pose is to be visualized (non-zero, hand undetected)
        public bool isValidLeft;
        public bool isValidRight;

        // List of serializable joint poses for left and right hand
        public SerializableHandJointPose[] jointPosesLeft;
        public SerializableHandJointPose[] jointPosesRight;

        // Wrist transforms relative to ref. system. 2 per frame (left, right)
        public Pose leftWrist;
        public Pose rightWrist;

        // Timestamp
        public float timestamp;

        // Constructors
        public FrameData(SerializableHandJointPose[] jointPosesLeft, SerializableHandJointPose[] jointPosesRight)
        {
            this.jointPosesLeft = jointPosesLeft;
            this.jointPosesRight = jointPosesRight;

            leftWrist = Pose.identity;
            rightWrist = Pose.identity;
        }

        public FrameData(SerializableHandJointPose[] jointPosesLeft, SerializableHandJointPose[] jointPosesRight, Pose leftWrist, Pose rightWrist)
        {
            this.jointPosesLeft = jointPosesLeft;
            this.jointPosesRight = jointPosesRight;
            this.leftWrist = leftWrist;
            this.rightWrist = rightWrist;
        }

        public FrameData(SerializableHandJointPose[] jointPosesLeft, SerializableHandJointPose[] jointPosesRight, Pose leftWrist, Pose rightWrist, bool isValidLeft, bool isValidRight)
        {
            this.isValidLeft = isValidLeft;
            this.isValidRight = isValidRight;
            this.jointPosesLeft = jointPosesLeft;
            this.jointPosesRight = jointPosesRight;
            this.leftWrist = leftWrist;
            this.rightWrist = rightWrist;
        }

        public FrameData(SerializableHandJointPose[] jointPosesLeft, SerializableHandJointPose[] jointPosesRight, Pose leftWrist, Pose rightWrist, bool isValidLeft, bool isValidRight, float timestamp)
        {
            this.isValidLeft = isValidLeft;
            this.isValidRight = isValidRight;
            this.jointPosesLeft = jointPosesLeft;
            this.jointPosesRight = jointPosesRight;
            this.leftWrist = leftWrist;
            this.rightWrist = rightWrist;
            this.timestamp = timestamp;
        }
    }

    [System.Serializable]
    public class HandTrackingDataset
    {
        public List<FrameData> frames = new List<FrameData>();
    }
}

public class HandtrackingManager : MonoBehaviour
{
    // Singleton 
    public static HandtrackingManager instance;

    #region Visualization properties

    [Tooltip("Left hand object")]
    public GameObject leftHand;
    [Tooltip("Right hand object")]
    public GameObject rightHand;

    [Tooltip("Armature of left hand (wrist)")]
    public GameObject leftArmature;
    [Tooltip("Armature of right hand (wrist)")]
    public GameObject rightArmature;

    [Tooltip("Renderer for left hand")]
    public SkinnedMeshRenderer leftSkinnedMeshRenderer;
    [Tooltip("Renderer for right hand")]
    public SkinnedMeshRenderer rightSkinnedMeshRenderer;

    [Tooltip("Rigged visual joints (left)")]
    private Transform[] leftRiggedVisualJointsArray = new Transform[(int)MixedReality.Toolkit.TrackedHandJoint.TotalJoints];
    [Tooltip("Rigged visual joints (left)")]
    private Transform[] rightRiggedVisualJointsArray = new Transform[(int)MixedReality.Toolkit.TrackedHandJoint.TotalJoints];

    [Tooltip("Frame iterator synced to video time for hand playback")]
    private int frameIterator = 0;

    [Tooltip("Fixed time subtracted and added to key frame time if only one key frame provided e.g. to determine rotation direction or animate")]
    public float singleFrameOffset = 0.25f;

    [Tooltip("Offset to synchronize video and handtracking. Not needed")]
    public float syncOffset = 0f;

    /// <summary>
    /// Enum representing tracked hand joints.
    /// </summary>
    public enum TrackedHandJoint
    {
        Palm = 0,
        Wrist = 1,
        ThumbMetacarpal,
        ThumbProximal,
        ThumbDistal,
        ThumbTip,
        IndexMetacarpal,
        IndexProximal,
        IndexIntermediate,
        IndexDistal,
        IndexTip,
        MiddleMetacarpal,
        MiddleProximal,
        MiddleIntermediate,
        MiddleDistal,
        MiddleTip,
        RingMetacarpal,
        RingProximal,
        RingIntermediate,
        RingDistal,
        RingTip,
        LittleMetacarpal,
        LittleProximal,
        LittleIntermediate,
        LittleDistal,
        LittleTip,
        TotalJoints
    }

    //[Tooltip("Cached positions of left [0] and right [1] index proximals. Used for alignment of place, rotate switch instructions.")]
    //private Vector3[] indexProximalPositions = new Vector3[2];
    //[Tooltip("Cached rotations of left [0] and right [1] index proximals. Used for alignment of place, rotate switch instructions.")]
    //private Quaternion[] indexProximalRotations = new Quaternion[2];

    [Tooltip("Cached hand joint pose for left index proximal")]
    private SerializableHandJointPose indexProxLeft;
    [Tooltip("Cached hand joint pose for right index proximal")]
    private SerializableHandJointPose indexProxRight;
    [Tooltip("Cached hand joint pose for left thumb proximal")]
    private SerializableHandJointPose thumbProxLeft;
    [Tooltip("Cached hand joint pose for right thumb proximal")]
    private SerializableHandJointPose thumbProxRight;
    [Tooltip("Cached hand joint pose for left middle metacarpal")]
    private SerializableHandJointPose middleMetaLeft;
    [Tooltip("Cached hand joint pose for right middle metacarpal")]
    private SerializableHandJointPose middleMetaRight;
    [Tooltip("Cached hand joint pose for left index tip")]
    private SerializableHandJointPose indexTipLeft;
    [Tooltip("Cached hand joint pose for right index tip")]
    private SerializableHandJointPose indexTipRight;
    [Tooltip("Cached hand joint pose for left middle tip")]
    private SerializableHandJointPose middleTipLeft;
    [Tooltip("Cached hand joint pose for right middle tip")]
    private SerializableHandJointPose middleTipRight;
    [Tooltip("Cached hand joint pose for left thumb tip")]
    // Used for Allen key alignment
    private SerializableHandJointPose thumbTipLeft;
    [Tooltip("Cached hand joint pose for right thumb tip")]
    private SerializableHandJointPose thumbTipRight;
    [Tooltip("Cached hand joint pose for left pinky knuckle")]
    private SerializableHandJointPose pinkyKnuckleLeft;
    [Tooltip("Cached hand joint pose for right pinky knuckle")]
    private SerializableHandJointPose pinkyKnuckleRight;

    // 3D instruction prefabs
    [Tooltip("Straight arrow instruction prefab")]
    public GameObject straightArrowPrefab;
    [Tooltip("Default dimensions of straight arrow")]
    public Vector3 straightArrowDefaultDims;
    [Tooltip("Default offset of straight arrow so that it suitably indicates place")]
    public Vector3 straightArrowOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Circular arrow instruction prefab")]
    public GameObject circularArrowPrefab;
    [Tooltip("Default dimensions of circular arrow")]
    public Vector3 circularArrowDefaultDims;
    [Tooltip("Orientaional offset for circular arrow in Euler angles along axes x, y, z")]
    public Vector3 circularArrowRotOffset = new Vector3(0f, -15f, 0f);

    [Tooltip("Screwdriver instruction prefab")]
    public GameObject screwdriverPrefab;
    [Tooltip("Screwdriver instruction prefab with cw arrow indication")]
    public GameObject screwdriverCwPrefab;
    [Tooltip("Screwdriver instruction prefab with ccw arrow indication")]
    public GameObject screwdriverCcwPrefab;
    [Tooltip("Default dimensions of screwdriver")]
    public Vector3 screwdriverDefaultDims;
    [Tooltip("Default offset of screwdriver so that screwdriver lies inside hand (left)")]
    public Vector3 sdPosOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of screwdriver so that screwdriver lies inside hand (left)")]
    public Vector3 sdRotOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default offset of screwdriver so that screwdriver lies inside hand (right)")]
    public Vector3 sdPosOffRight = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of screwdriver so that screwdriver lies inside hand (right)")]
    public Vector3 sdRotOffRight = new Vector3(0f, 0f, 0f);

    [Tooltip("Allen key instruction prefab")]
    public GameObject allenKeyPrefab;
    [Tooltip("Allen key instruction prefab with cw arrow indication")]
    public GameObject allenKeyCwPrefab;
    [Tooltip("Allen key instruction prefab with ccw arrow indication")]
    public GameObject allenKeyCcwPrefab;
    [Tooltip("Default dimensions of Allen key")]
    public Vector3 allenKeyDefaultDims;
    [Tooltip("Default offset of Allen key so that Allen key lies inside hand (left)")]
    public Vector3 akPosOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of Allen key so that Allen key lies inside hand (left)")]
    public Vector3 akRotOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default offset of Allen key so that Allen key lies inside hand (right)")]
    public Vector3 akPosOffRight = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of Allen key so that Allen key lies inside hand (right)")]
    public Vector3 akRotOffRight = new Vector3(0f, 0f, 0f);

    [Tooltip("Wrench instruction prefab")]
    public GameObject wrenchPrefab;
    [Tooltip("Default dimensions of wrench")]
    public Vector3 wrenchDefaultDims;
    [Tooltip("Default offset of wrench so that wrench lies inside hand (left)")]
    public Vector3 wrenchPosOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of wrench so that wrench lies inside hand (left)")]
    public Vector3 wrenchRotOffLeft = new Vector3(0f, 0f, 0f);
    [Tooltip("Default offset of wrench so that wrench lies inside hand (right)")]
    public Vector3 wrenchPosOffRight = new Vector3(0f, 0f, 0f);
    [Tooltip("Default rotation offset of wrench so that wrench lies inside hand (right)")]
    public Vector3 wrenchRotOffRight = new Vector3(0f, 0f, 0f);

    [Tooltip("Start frame time for animation. Determined by VLM")]
    private float animStartTime = 0f;
    [Tooltip("End frame time for animation. Determined by VLM")]
    private float animEndTime = 0f;
    [Tooltip("Time substracted at the end of animation span to remove unnecessary animation at the end")]
    public float animEndReduce = 0.5f;
    [Tooltip("Frame iterator at starting pose of action")]
    private int startIterator = 0;
    [Tooltip("Replay speed factor")]
    public float handAnimSpeedFactor = 1f;
    [Tooltip("Coroutine used to animate hands")]
    public Coroutine handAnimRoutine = null;
    [Tooltip("Wait of animation in initial pose before it starts")]
    public float animWaitTime = 2f;

    #endregion


    #region Capture properties

    [Tooltip("Path to handtracking data file")]
    public string handtrackingPath;
    [Tooltip("Handtracking file name (used in WINMD)")]
    private string fileName;
#if ENABLE_WINMD_SUPPORT
    private StorageFile handtrackingFile;
#endif

    [Tooltip("The dataset holding all the handtracking data after capture")]
    public HandTrackingDataset handtrackingDataset = new HandTrackingDataset();

    [Tooltip("Framerate of data capturing")]
    public float sampleFps;
    private float sampleInterval = 0f;
    [Tooltip("Elapsed time used for timestamping")]
    private float timeSinceCaptureStart = 0f;
    [Tooltip("Elapsed time used for checking against sampling interval")]
    private float timeSinceLastSample = 0f;

    [Tooltip("Subsytem responsible for handtracking")]
    private HandsAggregatorSubsystem handsSubsys;

    [Tooltip("Flag indicating whether handtracking data is being captured")]
    private bool capturingHands = false;
    [Tooltip("Flag indicating whether capture handtracking data is to be visualized")]
    private bool visualizingHands = false;

    [Tooltip("Coroutine for capturing hands")]
    private Coroutine captureHandsRoutine = null;
    [Tooltip("Coroutine responsible for visualizting hands")]
    private Coroutine visHandsRoutine = null;

    //[Tooltip("Pre-allocated array for joint poses (left)")]
    //private SerializableHandJointPose[] sJointPosesLeft = new SerializableHandJointPose[26];
    //[Tooltip("Pre-allocated array for joint poses (right)")]
    //private SerializableHandJointPose[] sJointPosesRight = new SerializableHandJointPose[26];

    [Tooltip("List containing all captured joint poses. Used for post-processing (left)")]
    private List<IReadOnlyList<HandJointPose>> lPoseList = new();
    [Tooltip("List containing all captured joint poses. Used for post-processing (right)")]
    private List<IReadOnlyList<HandJointPose>> rPoseList = new();

    #endregion


    #region Other

    [Tooltip("List of generated 3D instructions based on VLM result for single returned result")]
    private List<GameObject> generated3dInstructions = new List<GameObject>();

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
        int index = (int)MixedReality.Toolkit.TrackedHandJoint.Wrist;
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

    private void Start()
    {
        // Cache sample interval
        sampleInterval = 1f / sampleFps;

        // Debugging: Read handtracking data from file
        //InitializeHandRecording();
        //string json = System.IO.File.ReadAllText(Application.persistentDataPath + "/hands.json");
        //handtrackingDataset = JsonUtility.FromJson<HandTrackingDataset>(json);

        // Debugging: Test straight arrow alignment based on hand pose
        //SpawnPickAndPlaceInstruction(true, true);

        // Debugging Allen alignment
        //testObj1 = Instantiate(allenKeyCwPrefab);
        //testObj1.transform.localScale = allenKeyDefaultDims;
        //testObj2 = Instantiate(allenKeyCwPrefab);
        //testObj2.transform.localScale = allenKeyDefaultDims;
        //handsSubsys = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();

        //// Create debug lines
        //GameObject leftLineObj = new GameObject("LeftHandDebugLine");
        //leftLineRenderer = leftLineObj.AddComponent<LineRenderer>();
        //leftLineRenderer.startWidth = 0.003f;
        //leftLineRenderer.endWidth = 0.003f;
        //leftLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        //leftLineRenderer.startColor = Color.yellow;
        //leftLineRenderer.endColor = Color.yellow;
        //leftLineRenderer.positionCount = 2;

        //GameObject rightLineObj = new GameObject("RightHandDebugLine");
        //rightLineRenderer = rightLineObj.AddComponent<LineRenderer>();
        //rightLineRenderer.startWidth = 0.003f;
        //rightLineRenderer.endWidth = 0.003f;
        //rightLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        //rightLineRenderer.startColor = Color.cyan;
        //rightLineRenderer.endColor = Color.cyan;
        //rightLineRenderer.positionCount = 2;
    }
    //// Debugging
    //GameObject testObj1 = null;
    //GameObject testObj2 = null;
    //// Add to class properties (after testObj1/testObj2)
    //[Header("Debugging Visualization")]
    //public LineRenderer leftLineRenderer;
    //public LineRenderer rightLineRenderer;

    // Capture handtracking data in FixedUpdate
    private void Update()
    {
        // Debugging Allen - Using ThumbTip and Pinky Knuckle (LittleProximal)
        //if (handsSubsys != null)
        //{
        //    // Get only 2 joints: Thumb tip and Pinky knuckle
        //    handsSubsys.TryGetJoint(MixedReality.Toolkit.TrackedHandJoint.ThumbTip, XRNode.LeftHand, out HandJointPose leftThumbTip);
        //    handsSubsys.TryGetJoint(MixedReality.Toolkit.TrackedHandJoint.LittleProximal, XRNode.LeftHand, out HandJointPose leftPinkyKnuckle);

        //    handsSubsys.TryGetJoint(MixedReality.Toolkit.TrackedHandJoint.ThumbTip, XRNode.RightHand, out HandJointPose rightThumbTip);
        //    handsSubsys.TryGetJoint(MixedReality.Toolkit.TrackedHandJoint.LittleProximal, XRNode.RightHand, out HandJointPose rightPinkyKnuckle);

        //    // LEFT HAND - Check if both joints valid
        //    if (leftThumbTip.Position != Vector3.zero && leftPinkyKnuckle.Position != Vector3.zero)
        //    {
        //        // Average position of the two joints
        //        Vector3 targetPosLeft = (leftThumbTip.Position + leftPinkyKnuckle.Position) / 2f;

        //        // Create line direction from thumb to pinky
        //        Vector3 lineDirectionLeft = (leftPinkyKnuckle.Position - leftThumbTip.Position).normalized;
        //        Quaternion targetRotLeft = Quaternion.LookRotation(lineDirectionLeft, leftPinkyKnuckle.Up);

        //        // Apply offset
        //        targetPosLeft += targetRotLeft * Vector3.up * akPosOffLeft.x;
        //        targetPosLeft += targetRotLeft * Vector3.right * akPosOffLeft.y;
        //        targetPosLeft += targetRotLeft * Vector3.forward * akPosOffLeft.z;
        //        targetRotLeft *= Quaternion.Euler(akRotOffLeft);

        //        testObj1.transform.SetPositionAndRotation(targetPosLeft, targetRotLeft);
        //        testObj1.SetActive(true);

        //        // Visualize line
        //        leftLineRenderer.SetPosition(0, leftThumbTip.Position);
        //        leftLineRenderer.SetPosition(1, leftPinkyKnuckle.Position);
        //        leftLineRenderer.enabled = true;

        //        Debug.Log("Left: Using ThumbTip + PinkyKnuckle");
        //    }
        //    else
        //    {
        //        testObj1.SetActive(false);
        //        leftLineRenderer.enabled = false;
        //        Debug.Log("Left: ThumbTip or PinkyKnuckle not tracked");
        //    }

        //    // RIGHT HAND - Check if both joints valid
        //    if (rightThumbTip.Position != Vector3.zero && rightPinkyKnuckle.Position != Vector3.zero)
        //    {
        //        // Average position of the two joints
        //        Vector3 targetPosRight = (rightThumbTip.Position + rightPinkyKnuckle.Position) / 2f;

        //        // Create line direction from thumb to pinky
        //        Vector3 lineDirectionRight = (rightPinkyKnuckle.Position - rightThumbTip.Position).normalized;
        //        Quaternion targetRotRight = Quaternion.LookRotation(lineDirectionRight, rightPinkyKnuckle.Up);

        //        // Apply offset
        //        targetPosRight += targetRotRight * Vector3.up * akPosOffRight.x;
        //        targetPosRight += targetRotRight * Vector3.right * akPosOffRight.y;
        //        targetPosRight += targetRotRight * Vector3.forward * akPosOffRight.z;
        //        targetRotRight *= Quaternion.Euler(akRotOffRight);

        //        testObj2.transform.SetPositionAndRotation(targetPosRight, targetRotRight);
        //        testObj2.SetActive(true);

        //        // Visualize line
        //        rightLineRenderer.SetPosition(0, rightThumbTip.Position);
        //        rightLineRenderer.SetPosition(1, rightPinkyKnuckle.Position);
        //        rightLineRenderer.enabled = true;

        //        Debug.Log("Right: Using ThumbTip + PinkyKnuckle");
        //    }
        //    else
        //    {
        //        testObj2.SetActive(false);
        //        rightLineRenderer.enabled = false;
        //        Debug.Log("Right: ThumbTip or PinkyKnuckle not tracked");
        //    }
        //}

        // Capture handtracking data
        if (capturingHands)
        {
            // Measure elapsed time between frames
            timeSinceLastSample += Time.deltaTime;

            // Check if elapsed time reached sample interval
            if (timeSinceLastSample >= sampleInterval)
            {
                // Calculate time since capture start 
                timeSinceCaptureStart += timeSinceLastSample;
                
                // Save curreng hand pose
                SaveCurrentHandtracking(timeSinceCaptureStart);

                // Reset elapsed time
                timeSinceLastSample = 0f;
            }
        }

        //// Visualize/Animate handtracking data
        //if (animateHands)
        //{
        //    // Calculate time since capture start 
        //    animTime += Time.deltaTime * handAnimSpeedFactor;

        //    // Check if animation end time surpassed
        //    if (animTime >= animEndTime)
        //    {
        //        // Restart animation
        //        animTime = animStartTime;

        //        // Reset iterator to initial hand frame
        //        frameIterator = startIterator;
        //    }

        //    // Visualize hand frame
        //    VisualizeHTFrame(animTime);
        //}
    }

    #endregion


    #region Class methods

    // Prepare hand capturing
    public void InitializeHandRecording()
    {
        // Initialize handtracking dataset
        handtrackingDataset = new HandTrackingDataset();

        // Reset timers
        timeSinceCaptureStart = 0f;
        timeSinceLastSample = 0f;

        // Initialize hands aggregator subsystem
        handsSubsys = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();

        //Log.Msg("Initialized hand tracking capture");
    }

    // Deinitialize hand capturing device
    public void ResetHandData()
    {
        // Stop any visualization if ongoing
        visualizingHands = false;

        // Disable visuals
        leftHand.SetActive(false);
        rightHand.SetActive(false);

        // Deinitialize hands aggregator subsystem
        handsSubsys = null;

        // Deinitialize handtracking dataset
        handtrackingDataset = null;

        // Reset variables
        timeSinceCaptureStart = 0f;
        timeSinceLastSample = 0f;
        frameIterator = 0;

        // Reset file related references
        handtrackingPath = null;
        fileName = null;

        //Log.Msg("Reset hand data");
    }

    // Start capturing handtracking data
    public void StartCapturingHands()
    {
        // Enable capturing
        capturingHands = true;
        //captureHandsRoutine = StartCoroutine(CaptureHands());
    }

    // Stop capturing handtracking data
    public void StopCapturingHands()
    {
        // Disabled capturing
        capturingHands = false;
    }

    // Temporarily store joint poses
    private void GetCurrentHands(float timestamp)
    {
        // Try to get joint poses if valid (26 joints (palm unnecessary): [palm (0)], wrist (1), thumb (4), index (5), middle (5), ring (5), pinky (5))
        handsSubsys.TryGetEntireHand(XRNode.LeftHand, out IReadOnlyList<HandJointPose> leftJointPoses);
        handsSubsys.TryGetEntireHand(XRNode.RightHand, out IReadOnlyList<HandJointPose> rightJointPoses);

        // Add to list of joint poses
        lPoseList.Add(leftJointPoses);
        rPoseList.Add(rightJointPoses);

        // Store timestamp
        //timestamps.Add(timestamp);
    }
    
    // Post-process captured handtracking data (serializable format, wrist coord system transform)
    private void PostProcessHandData()
    {
        //// Go through saved all poses
        //for (int i = 0; i < lPoseList.Count; i++)
        //{
        //    // Save relative wrist positions
        //    Pose leftWrist = Pose.identity;
        //    Pose rightWrist = Pose.identity;

        //    for (int j = 0; j < 26; j++)
        //    {
        //        sJointPosesLeft[j] = new SerializableHandJointPose(lPoseList[i][j]);
        //        sJointPosesRight[j] = new SerializableHandJointPose(rPoseList[i][j]);

        //        // Store wrist position relative to reference 
        //        if (j == 1)
        //        {
        //            Transform refBase = QRCodeTracking.instance.qrTransform;

        //            leftWrist.position = refBase.InverseTransformPoint(sJointPosesLeft[j].position);
        //            leftWrist.rotation = Quaternion.Inverse(refBase.rotation) * sJointPosesLeft[j].rotation;

        //            rightWrist.position = refBase.InverseTransformPoint(sJointPosesRight[j].position);
        //            rightWrist.rotation = Quaternion.Inverse(refBase.rotation) * sJointPosesRight[j].rotation;
        //        }
        //    }

        //    // Create frame and add to dataset
        //    // If wrist is undetected (zero pose), no need to visualize frame
        //    bool isValidLeft = (lPoseList[i][1].Position != Vector3.zero);
        //    bool isValidRight = (rPoseList[i][1].Position != Vector3.zero);
        //    FrameData dataFrame = new FrameData(sJointPosesLeft, sJointPosesRight, leftWrist, rightWrist, isValidLeft, isValidRight, timestamps[i]);
        //    handtrackingDataset.frames.Add(dataFrame);
        //}

        //Log.Msg($"Finished post-processing of handtracking data. Number of frames: {lPoseList.Count}");

        //// Free list resources
        //lPoseList = null;
        //rPoseList = null;
    }

    // Capturing logic
    private void SaveCurrentHandtracking(float timestamp)
    {
        // Try to get joint poses if valid (26 joints (palm unnecessary): [palm (0)], wrist (1), thumb (4), index (5), middle (5), ring (5), pinky (5))
        handsSubsys.TryGetEntireHand(XRNode.LeftHand, out IReadOnlyList<HandJointPose> leftJointPoses);
        handsSubsys.TryGetEntireHand(XRNode.RightHand, out IReadOnlyList<HandJointPose> rightJointPoses);
        
        // Save relative wrist positions
        Pose leftWrist = Pose.identity;
        Pose rightWrist = Pose.identity;

        SerializableHandJointPose[] sJointPosesLeft = new SerializableHandJointPose[26];
        SerializableHandJointPose[] sJointPosesRight = new SerializableHandJointPose[26];

        for (int i = 0; i < 26; i++)
        {
            //SerializableHandJointPose sPoseLeft = new SerializableHandJointPose(leftJointPoses[i]);
            //SerializableHandJointPose sPoseRight = new SerializableHandJointPose(rightJointPoses[i]);
            //sJointPosesLeft.Add(sPoseLeft);
            //sJointPosesRight.Add(sPoseRight);
            sJointPosesLeft[i] = new SerializableHandJointPose(leftJointPoses[i]);
            sJointPosesRight[i] = new SerializableHandJointPose(rightJointPoses[i]);
            
            // Store wrist position relative to reference 
            if (i == 1)
            {
                Transform refBase = QRAuth.instance.qrTransform;

                leftWrist.position = refBase.InverseTransformPoint(sJointPosesLeft[i].position);
                leftWrist.rotation = Quaternion.Inverse(refBase.rotation) * sJointPosesLeft[i].rotation;

                rightWrist.position = refBase.InverseTransformPoint(sJointPosesRight[i].position);
                rightWrist.rotation = Quaternion.Inverse(refBase.rotation) * sJointPosesRight[i].rotation;
            }
        }

        // Create frame and add to dataset
        // If wrist is undetected (zero pose), no need to visualize frame
        bool isValidLeft = (leftJointPoses[1].Position != Vector3.zero);
        bool isValidRight = (rightJointPoses[1].Position != Vector3.zero);
        FrameData dataFrame = new FrameData(sJointPosesLeft, sJointPosesRight, leftWrist, rightWrist, isValidLeft, isValidRight, timestamp);
        handtrackingDataset.frames.Add(dataFrame);
    }

    // Write current handtracking to disk
    public async Task WriteHandtrackingToDisk(int specificStep = -1)
    {
#if ENABLE_WINMD_SUPPORT
        // Save handtracking data for this workstep
        string json = JsonUtility.ToJson(handtrackingDataset, true);

        // Create json file
        fileName = "hands.json";
        if (specificStep == -1)
        {
            handtrackingFile = await WorkflowManager.instance.currentStepFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }
        else
        {
            handtrackingFile = await WorkflowManager.instance.stepFolders[specificStep - 1].CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }
        handtrackingPath = handtrackingFile.Path;

        // Write handtracking data to file
        await FileIO.WriteTextAsync(handtrackingFile, json);
#endif
    }

    // Visualize captured handtracking data
    private void VisualizeHandFrame(double frameTime = 0)
    {
        //Log.Msg($"Video at frame {videoFrame} and time {videoTime}");

        if (handtrackingDataset != null)
        {
            if (frameIterator < handtrackingDataset.frames.Count)
            {
                // Get temporally closest handtracking frame
                float handTime = handtrackingDataset.frames[frameIterator].timestamp;
                while (frameTime > handTime)
                {
                    frameIterator++;
                    handTime = handtrackingDataset.frames[frameIterator].timestamp;
                }

                //Log.Msg($"VTime: {videoTime}, FTime: {frameTime}, FI: {frameIterator}");

                // Get data frame
                //FrameData frame = handtrackingDataset.frames[(int)videoFrame];
                FrameData frame = handtrackingDataset.frames[frameIterator];

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

    // Prepare placeholder hands for determination of hand frame(s) corresponding to timestamp
    public void PrepareHands(bool resetIterator = true)
    {
        // Reset replay frame to 0
        if (resetIterator)
        {
            frameIterator = 0;
        }
        else
        {
            // Cache current iterator as starting index
            startIterator = frameIterator;
        }

        // Attach hands to qrcode (reference system)
        Transform refBase = QRAuth.instance.qrTransform;
        leftHand.transform.SetParent(refBase, true);
        leftHand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rightHand.transform.SetParent(refBase, true);
        rightHand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Disable hands by default
        leftHand.SetActive(false);
        rightHand.SetActive(false);

        //Log.Msg("Prepared hands for alignment");
    }

    // Terminate any ongoing hand visualization and disable hands
    public void ResetHands(bool resetPos)
    {
        //visualizingHands = false;
        if (handAnimRoutine != null)
        {
            StopCoroutine(handAnimRoutine);
            handAnimRoutine = null;
        }

        if (leftHand == null || rightHand == null)
        {
            //Log.Msg("Hands not initialized. Aborting reset");
            return;
        }

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

    // Dedicated callback for animation routine
    public void StartHandAnimation(float startTime, float endTime)
    {
        handAnimRoutine = StartCoroutine(AnimateHandsLooped(startTime, endTime));
    }

    // Visualize single handtracking data frame (e.g. for animation)
    private void VisualizeHTFrame(float frameTime)
    {
        //Log.Msg($"Video at frame {videoFrame} and time {videoTime}");

        if (handtrackingDataset != null)
        {
            // Make sure not to exceed container limits (0 because we always needs 2 frames for interpolation)
            if (frameIterator < handtrackingDataset.frames.Count || frameIterator == 0)
            {
                // Get temporally closest handtracking frame
                float handTime = handtrackingDataset.frames[frameIterator].timestamp;
                while (handTime <= frameTime)
                {
                    frameIterator++;
                    handTime = handtrackingDataset.frames[frameIterator].timestamp;
                }

                //// Also cache hand time before current selection so we have 2 values for interpolation
                //float prevHandTime = handtrackingDataset.frames[frameIterator - 1].timestamp;
                //// Calculate interpolation factor
                //float t = (frameTime - prevHandTime) / (handTime - prevHandTime);
                //// Get previous data frame
                //FrameData prevFrame = handtrackingDataset.frames[frameIterator - 1];

                // Get data frame
                FrameData frame = handtrackingDataset.frames[frameIterator];

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
                            //leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - leftJointTransform.position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);
                            leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - frame.jointPosesLeft[i - 1].position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);
                            //rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - rightJointTransform.position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);
                            rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - frame.jointPosesRight[i - 1].position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);
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
        PrepareHands();

        // Assign handtracking data set of this step as underlying data
        handtrackingDataset = InstructionManager.Instance.currentInstructionSet.handtrackingData;

        if (handtrackingDataset == null)
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
            leftHand.SetActive(InstructionManager.Instance.currentInstructionSet.leftUsed);
            rightHand.SetActive(InstructionManager.Instance.currentInstructionSet.rightUsed);

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


    #region Assistance when authoring 3D

    // Determines which hand was probably used based on total motion. Returns true if right hand used, false if left hand used.
    private bool DetermineHandedness(bool regenerated = false)
    {
        // Total motion
        float motionLeft = 0f;
        float motionRight = 0f;

        //Log.Msg($"Length of handtracking dataset: {handtrackingDataset.frames.Count}");

        // Get handtracking dataset corresponding to result returned by VLM
        HandTrackingDataset htds = new();
        if (regenerated)
        {
            // Regenerated instruction set during review -> Take current data
            htds = InstructionManager.Instance.currentInstructionSet.handtrackingData;
        }
        else
        {
            // Currently authoring demos -> Take last unfinished instruction set
            htds = InstructionManager.Instance.incompleteInstructionSets.Peek().handtrackingData;
        }
            

        // Iterate over captured wrist poses [1] in pose array
        for (int i = 1; i < htds.frames.Count; i++)
        {
            // Time between hand frames
            float deltaTime = htds.frames[i].timestamp - htds.frames[i - 1].timestamp;

            // Check if frames valid
            if (htds.frames[i].isValidLeft && htds.frames[i - 1].isValidLeft)
            {
                // Motion between frames
                float leftVelo = (htds.frames[i].leftWrist.position - htds.frames[i - 1].leftWrist.position).magnitude / deltaTime;
                
                // Overall motion
                motionLeft += leftVelo * deltaTime;
            }

            if (htds.frames[i].isValidRight && htds.frames[i - 1].isValidRight)
            {
                // Motion between frames
                float rightVelo = (htds.frames[i].rightWrist.position - htds.frames[i - 1].rightWrist.position).magnitude / deltaTime;
                
                // Overall motion
                motionRight += rightVelo * deltaTime;
            }
        }

        //Log.Msg($"Total motion left: {motionLeft}, Total motion right: {motionRight}");

        if (motionLeft > motionRight)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    // Determines in which direction the hand is rotated based on palm orientation (clockwise or counter-clockwise). Returns true if clockwise, false if counter-clockwise.
    private bool DetermineHandRotDirection(bool rightUsed, float[] frameTimes, bool regenerated = false)
    {
        // Get handtracking dataset corresponding to result returned by VLM
        HandTrackingDataset htds = new();
        if (regenerated)
        {
            // Regenerated instruction set during review -> Take current data
            htds = InstructionManager.Instance.currentInstructionSet.handtrackingData;
        }
        else
        {
            // Currently authoring demos -> Take last unfinished instruction set
            htds = InstructionManager.Instance.incompleteInstructionSets.Peek().handtrackingData;
        }

        // Array containing start and end times of key frames showing rotary action
        float[] handFrameStartEndTimes = new float[2];

        // Check if multiple key frames provided or only one
        if (frameTimes.Length > 1)
        {
            // Assign first timestamp as start and last as end 
            handFrameStartEndTimes[0] = frameTimes[0];
            handFrameStartEndTimes[1] = frameTimes[^1];
        }
        else
        {
            // Choose start time and end time at fixed temporal distance around key timestamp
            handFrameStartEndTimes[0] = frameTimes[0] - singleFrameOffset;
            handFrameStartEndTimes[1] = frameTimes[0] + singleFrameOffset;
        }

        // Indicies of start and end frame of key frames showing rotary action
        int[] startEndIndices = new int[2];
        for (int i = 0; i < handFrameStartEndTimes.Length; i++)
        {
            // Get handtracking frame index corresponding to target time (both start and end)
            if (htds != null)
            {
                // Get index of temporally closest handtracking frame
                int index = 0;
                float handFrameTime = htds.frames[index].timestamp;
                while (handFrameStartEndTimes[i] > handFrameTime)
                {
                    index++;
                    handFrameTime = htds.frames[index].timestamp;
                }

                // Check if previous or next iterator closer to target time
                if (index > 0)
                {
                    float prevhandFrameTime = htds.frames[index - 1].timestamp;
                    if (Mathf.Abs(handFrameStartEndTimes[i] - prevhandFrameTime) < Mathf.Abs(handFrameStartEndTimes[i] - handFrameTime))
                    {
                        index--;
                    }
                }

                // Save start and end indices
                startEndIndices[i] = index;
            }
        }

        //Log.Msg($"Start index: {startEndIndices[0]}, End index: {startEndIndices[1]}");

        // Go through (palm) poses of relevant handtracking frames for used hand. Make sure not to exceed array bounds (+1)
        for (int i = startEndIndices[0] + 1; i < startEndIndices[1]; i++)
        {
            // Previous and current palm upward rotations 
            Vector3 prevPalmUp;
            Vector3 currPalmUp;
            // Palm normal used as comparion axis as we are interested in rotation around that axis
            Vector3 palmNormal;
            // Signed rotation
            float totalRot = 0f;

            if (rightUsed)
            {
                // Upward rotations
                prevPalmUp = htds.frames[i - 1].jointPosesRight[0].rotation * Vector3.up;
                currPalmUp = htds.frames[i].jointPosesRight[0].rotation * Vector3.up;

                // Rotation axis to compare to (Use forward vector of palm [0] in previous frame)
                palmNormal = htds.frames[i - 1].jointPosesRight[0].rotation * Vector3.forward;

                // Compute signed rotation 
                totalRot += Vector3.SignedAngle(prevPalmUp, currPalmUp, palmNormal);
            }
            else
            {
                // Upward rotations
                prevPalmUp = htds.frames[i - 1].jointPosesLeft[0].rotation * Vector3.up;
                currPalmUp = htds.frames[i].jointPosesLeft[0].rotation * Vector3.up;

                // Rotation axis to compare to (Use forward vector of palm [0] in previous frame)
                palmNormal = htds.frames[i - 1].jointPosesLeft[0].rotation * Vector3.forward;

                // Compute signed rotation 
                totalRot += Vector3.SignedAngle(prevPalmUp, currPalmUp, palmNormal);
            }

            // Determine if rotation clockwise or counter-clockwise
            if (totalRot > 0f)
            {
                //Log.Msg("Found counter-clockwise rotation");

                // Counter-clockwise
                return false;
            }
            else
            {
                //Log.Msg("Found clockwise rotation");

                // Clockwise
                return true;
            }
        }

        // Default return value clockwise  
        return true;
    }

    // Generate in-situ instructions based on returned result (during authoring) 
    public void Initialize3DInstructions(string action, string[] keyFrameNums, string handUsed, string rotDirection)
    {
        // Cache action/instruction type (can imply multiple instruction objects e.g. "pick and place")
        InstructionManager.Instance.InstructionType = action;

        // Get frame indices as ints
        int[] frameNums = Array.ConvertAll(keyFrameNums, s => int.Parse(s));
        //foreach (var fNum in frameNums)
        //{
        //    Log.Msg($"Frame number: {fNum}");
        //}

        // Convert to timestamp based on sampling rate
        float[] frameTimes = new float[frameNums.Length];
        //frameTimes = new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f};
        for (int i = 0; i < frameNums.Length; i++)
        {
            frameTimes[i] = frameNums[i] * WebSocketClient.instance.samplingInterval / 30f;// 30 because we are always capturing at 30fps which is max. fps for shared capture on HL2
            //Log.Msg($"Corresponding time (in seconds) for frame {frameNums[i]}: {frameTimes[i]}");
        }

        // Determine whether left, right, or both hands used
        bool leftUsed = true;
        bool rightUsed = true;
        if (handUsed == "both")
        {
            // Both hands relevant
            leftUsed = true;
            rightUsed = true;
        }
        else
        {
            // Only one hand used
            rightUsed = DetermineHandedness();
            leftUsed = !rightUsed;

            // Inform client about used hand if information does not match gpt result
            if (handUsed == "left" && rightUsed || handUsed == "right" && leftUsed)
            {
                if (WebSocketClient.instance.connected)
                {
                    WebSocketClient.instance.SendMsg(Msg.MsgType.HANDUSED, rightUsed ? "right" : "left");
                }
            }
        }

        // Cache used hands
        InstructionManager.Instance.LeftUsed = leftUsed;
        InstructionManager.Instance.RightUsed = rightUsed;

        //Log.Msg($"Hands used: Left: {leftUsed}, Right: {rightUsed}");

        // Initialize placeholder hands
        PrepareHands(true);

        // Reset 3D instruction data
        generated3dInstructions = new List<GameObject>();

        // Differentiate between actions
        if (action == "pick and place")
        {
            // Align hands with pick moment
            AlignHands(frameTimes[0]);
            // Spawn pick instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with place moment
            AlignHands(frameTimes[1]);
            // Spawn place instruction
            SpawnPlaceInstruction(leftUsed, rightUsed);

            // Save pick and place times as start and end time
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[1];
        }
        else if (action == "press button")
        {
            // Align hands with press moment
            AlignHands(frameTimes[0]);
            // Spawn press button instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when pressing button)
            InstructionManager.Instance.KeyTime = frameTimes[0];
        }
        else if (action == "rotate switch")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with rotate moment. Choose mid-most frame time if multiple provided
            AlignHands(frameTimes[frameTimes.Length / 2]);

            // Spawn rotate switch instruction
            SpawnRotateSwitchInstruction(leftUsed, rightUsed, cwRot);

            // Cache key time (when rotating switch)
            InstructionManager.Instance.KeyTime = frameTimes[frameTimes.Length / 2];
        }
        else if (action == "pull/push")
        {
            // Align hands with initial grab moment
            AlignHands(frameTimes[0]);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when grabbing onto handle)
            InstructionManager.Instance.KeyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.IsAnimated = true;
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[^1];
        }
        else if (action == "open/close")
        {
            // Align hands with initial grab moment
            AlignHands(frameTimes[0]);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when grabbing onto handle)
            InstructionManager.Instance.KeyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.IsAnimated = true;
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[^1];
        }
        else if (action == "use screwdriver")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with pick screwdriver moment
            AlignHands(frameTimes[0]);
            // Spawn pick screwdriver instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with use screwdriver moment
            AlignHands(frameTimes[1]);
            // Spawn use screwdriver instruction
            SpawnUseScrewdriverInstruction(leftUsed, rightUsed, cwRot);

            // Save pick and use times as start and end time
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[1];
        }
        else if (action == "use Allen")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with pick Allen key moment
            AlignHands(frameTimes[0]);
            // Spawn pick Allen key instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with use Allen key moment
            AlignHands(frameTimes[1]);
            // Spawn use Allen key instruction
            SpawnUseAllenInstruction(leftUsed, rightUsed, cwRot);

            // Save pick and use times as start and end time
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[1];
        }
        else if (action == "use wrench")
        {
            // Align hands with pick wrench moment
            AlignHands(frameTimes[0]);
            // Spawn pick wrench instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with use wrench moment
            AlignHands(frameTimes[1]);
            // Spawn use wrench instruction
            SpawnUseWrenchInstruction(leftUsed, rightUsed);

            // Save pick and use times as start and end time
            InstructionManager.Instance.StartTime = frameTimes[0];
            InstructionManager.Instance.EndTime = frameTimes[1];
        }
        else
        {
            // Animated hands for any action not present in action set
            // Align hands with initial action moment (Only relevant for panel placement)
            AlignHands(frameTimes[0]);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (init hand action moment)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.currentInstructionSet.isAnimated = true;
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[^1];
        }

        // Cache generated 3D instructions
        InstructionManager.Instance.InsituObjects = generated3dInstructions;

        // Clear hand data now to prepare for any next steps
        //ResetHandData();
    }

    // (Re-)Generate in-situ instructions based on returned result (during review)
    public void Regenerate3DInstructions(string action, string[] keyFrameNums, string handUsed, string rotDirection)
    {
        // Cache action/instruction type (can imply multiple instruction objects e.g. "pick and place")
        InstructionManager.Instance.currentInstructionSet.instructionType = action;
        //Log.Msg("Updated action type");

        // Get frame indices as ints
        int[] frameNums = Array.ConvertAll(keyFrameNums, s => int.Parse(s));
        //foreach (var fNum in frameNums)
        //{
        //    Log.Msg($"Frame number: {fNum}");
        //}

        // Convert to timestamp based on sampling rate
        float[] frameTimes = new float[frameNums.Length];
        //frameTimes = new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f};
        for (int i = 0; i < frameNums.Length; i++)
        {
            frameTimes[i] = frameNums[i] * WebSocketClient.instance.samplingInterval / 30f;// 30 because we are always capturing at 30fps which is max. fps for shared capture on HL2
            //Log.Msg($"Corresponding time (in seconds) for frame {frameNums[i]}: {frameTimes[i]}");
        }

        // Determine whether left, right, or both hands used
        bool leftUsed = true;
        bool rightUsed = true;
        if (handUsed == "both")
        {
            // Both hands relevant
            leftUsed = true;
            rightUsed = true;
        }
        else
        {
            // Only one hand used
            rightUsed = DetermineHandedness(true);
            leftUsed = !rightUsed;

            //// Inform client about used hand if information does not match gpt result
            //if (handUsed == "left" && rightUsed || handUsed == "right" && leftUsed)
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.HANDUSED, rightUsed ? "right" : "left");
            //    }
            //}
        }

        // Cache used hands
        InstructionManager.Instance.currentInstructionSet.leftUsed = leftUsed;
        InstructionManager.Instance.currentInstructionSet.rightUsed= rightUsed;

        //Log.Msg($"Hands used: Left: {leftUsed}, Right: {rightUsed}");

        // Initialize placeholder hands
        PrepareHands(true);

        // Reset 3D instruction data
        generated3dInstructions = new List<GameObject>();
        //Log.Msg("Preparation successful (regenerate)");

        // Differentiate between actions
        if (action == "pick and place")
        {
            // Align hands with pick moment
            AlignHands(frameTimes[0], true);
            // Spawn pick instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with place moment
            AlignHands(frameTimes[1], true);
            // Spawn place instruction
            SpawnPlaceInstruction(leftUsed, rightUsed);

            // Save pick and place times as start and end time
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[1];
        }
        else if (action == "press button")
        {
            // Align hands with press moment
            AlignHands(frameTimes[0], true);
            // Spawn press button instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when pressing button)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[0];
        }
        else if (action == "rotate switch")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes, true);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with rotate moment. Choose mid-most frame time if multiple provided
            AlignHands(frameTimes[frameTimes.Length / 2], true);

            // Spawn rotate switch instruction
            SpawnRotateSwitchInstruction(leftUsed, rightUsed, cwRot);

            // Cache key time (when rotating)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[frameTimes.Length / 2];
        }
        else if (action == "pull/push")
        {
            // Align hands with initial grab moment
            AlignHands(frameTimes[0], true);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when grabbing onto handle)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.currentInstructionSet.isAnimated = true;
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[^1];
        }
        else if (action == "open/close")
        {
            // Align hands with initial grab moment
            AlignHands(frameTimes[0], true);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (when grabbing onto handle)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.currentInstructionSet.isAnimated = true;
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[^1];
        }
        else if (action == "use screwdriver")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes, true);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with pick screwdriver moment
            AlignHands(frameTimes[0], true);
            // Spawn pick screwdriver instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with use screwdriver moment
            AlignHands(frameTimes[1], true);
            // Spawn use screwdriver instruction
            SpawnUseScrewdriverInstruction(leftUsed, rightUsed, cwRot);

            // Cache key times (pick and operate tool)
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[1];
        }
        else if (action == "use Allen")
        {
            // Determine direction of rotation within specified time frame
            //bool cwRot = DetermineHandRotDirection(rightUsed, frameTimes, true);
            bool cwRot = true; // For now only cw rotation

            //// Inform client about rotation direction if information does not match gpt result
            //if (cwRot && rotDirection == "ccw" || !cwRot && rotDirection == "cw")
            //{
            //    if (WebSocketClient.instance.connected)
            //    {
            //        WebSocketClient.instance.SendMsg(Msg.MsgType.ROTDIRECTION, cwRot ? "cw" : "ccw");
            //    }
            //}

            // Align hands with pick Allen key moment
            AlignHands(frameTimes[0], true);
            //Log.Msg("Aligned hands");
            // Spawn pick Allen key instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);
            //Log.Msg("Spawned hand");

            // Align hands with use Allen key moment
            AlignHands(frameTimes[1], true);
            //Log.Msg("Aligned hands");
            // Spawn use Allen key instruction
            SpawnUseAllenInstruction(leftUsed, rightUsed, cwRot);
            //Log.Msg("Spawned Allen");

            // Cache key times (pick and operate tool)
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[1];
        }
        else if (action == "use wrench")
        {
            // Align hands with pick wrench moment
            AlignHands(frameTimes[0], true);
            // Spawn pick wrench instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Align hands with use wrench moment
            AlignHands(frameTimes[1], true);
            // Spawn use wrench instruction
            SpawnUseWrenchInstruction(leftUsed, rightUsed);

            // Cache key times (pick and operate tool)
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[1];
        }
        else
        {
            // Animated hands for any action not present in action set
            // Align hands with initial action moment (Only relevant for panel placement)
            AlignHands(frameTimes[0], true);
            // Spawn static hand instruction
            SpawnStaticHandInstruction(leftUsed, rightUsed);

            // Cache key time (init hand action moment)
            InstructionManager.Instance.currentInstructionSet.keyTime = frameTimes[0];

            // Cache start and end times for potential animation
            InstructionManager.Instance.currentInstructionSet.isAnimated = true;
            InstructionManager.Instance.currentInstructionSet.startTime = frameTimes[0];
            InstructionManager.Instance.currentInstructionSet.endTime = frameTimes[^1];
        }

        // Cache generated 3D instructions
        InstructionManager.Instance.currentInstructionSet.insituObjects = generated3dInstructions;

        // Clear hand data now to prepare for any next steps
        //ResetHandData();
    }

    // Sets placeholder hands to captured spatial pose of hands at associated and specified capture time
    public void AlignHands(float pvFrameTime/*, bool leftUsed, bool rightUsed, string action*/, bool regenerated = false)
    {
        // Get handtracking dataset corresponding to result returned by VLM
        HandTrackingDataset htds = new();
        if (regenerated)
        {
            // Regenerated instruction set during review -> Take current data
            htds = InstructionManager.Instance.currentInstructionSet.handtrackingData;
        }
        else
        {
            // Currently authoring demos -> Take last unfinished instruction set
            htds = InstructionManager.Instance.incompleteInstructionSets.Peek().handtrackingData;
        }

        if (htds != null)
        {
            if (frameIterator < htds.frames.Count)
            {
                // Get temporally closest handtracking frame
                float handFrameTime = htds.frames[frameIterator].timestamp;
                while (pvFrameTime > handFrameTime)
                {
                    frameIterator++;
                    handFrameTime = htds.frames[frameIterator].timestamp;
                }

                // Check if previous or next iterator closer to target time
                if (frameIterator > 0)
                {
                    float prevhandFrameTime = htds.frames[frameIterator - 1].timestamp;
                    if (Mathf.Abs(pvFrameTime - prevhandFrameTime) < Mathf.Abs(pvFrameTime - handFrameTime))
                    {
                        frameIterator--;
                        handFrameTime = prevhandFrameTime;
                    }
                }

                //Log.Msg($"PV time: {pvFrameTime}, Hand time: {handFrameTime}, hand frame iterator: {frameIterator}");

                // Get data frame
                FrameData frame = htds.frames[frameIterator];

                // Make sure not visualizing null data
                if (frame != null)
                {
                    // Check if frame is to be visualized (non-zero)
                    if (!frame.isValidLeft && !frame.isValidRight)
                    {
                        // Zero frame data -> Skip visualization
                        //Log.Msg($"Skipped visualization because hand invalid. FI: {frameIterator}");
                        return;
                    }
                    else
                    {
                        // Non-zero frame data (either or both left, right)

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
                                    //leftJointTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                                    //rightJointTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

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

                                    // Cache thumb tip
                                    if ((TrackedHandJoint)i == TrackedHandJoint.ThumbTip)
                                    {
                                        thumbTipLeft = sLeftJointPose;
                                        thumbTipRight = sRightJointPose;
                                    }

                                    // Cache index tip for use in place instruction
                                    if ((TrackedHandJoint)i == TrackedHandJoint.IndexTip)
                                    {
                                        indexTipLeft = sLeftJointPose;
                                        indexTipRight = sRightJointPose;
                                    }

                                    // Cache middle tip for use in place instruction
                                    if ((TrackedHandJoint)i == TrackedHandJoint.MiddleTip)
                                    {
                                        middleTipLeft = sLeftJointPose;
                                        middleTipRight = sRightJointPose;
                                    }

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

                                    // Cache middle metacarpal for use in screwdriver, Allen, and wrench instructions
                                    if ((TrackedHandJoint)i == TrackedHandJoint.MiddleMetacarpal)
                                    {
                                        middleMetaLeft = sLeftJointPose;
                                        middleMetaRight = sRightJointPose;
                                    }

                                    break;
                                default:
                                    // For all other bones, do a simple "IK" from the rigged joint to the joint data's position.
                                    leftJointTransform.rotation = Quaternion.LookRotation(sLeftJointPose.position - leftJointTransform.position, new Pose(frame.jointPosesLeft[i - 1].position, frame.jointPosesLeft[i - 1].rotation).up);
                                    rightJointTransform.rotation = Quaternion.LookRotation(sRightJointPose.position - rightJointTransform.position, new Pose(frame.jointPosesRight[i - 1].position, frame.jointPosesRight[i - 1].rotation).up);

                                    // Cache index proximal for use in rotate switch instruction
                                    if ((TrackedHandJoint)i == TrackedHandJoint.IndexProximal)
                                    {
                                        indexProxLeft = sLeftJointPose;
                                        indexProxRight = sRightJointPose;
                                    }

                                    // Cache thumb proximal for use in rotate switch instruction
                                    if ((TrackedHandJoint)i == TrackedHandJoint.ThumbProximal)
                                    {
                                        thumbProxLeft = sLeftJointPose;
                                        thumbProxRight = sRightJointPose;
                                    }

                                    // Cache pinky knuckle
                                    if ((TrackedHandJoint)i == TrackedHandJoint.LittleProximal)
                                    {
                                        pinkyKnuckleLeft = sLeftJointPose;
                                        pinkyKnuckleRight = sRightJointPose;
                                    }

                                    break;
                            }
                        }

                        // Transform wrist into reference coords
                        //leftHand.transform.SetLocalPositionAndRotation(frame.leftWrist.position, frame.leftWrist.rotation);
                        //rightHand.transform.SetLocalPositionAndRotation(frame.rightWrist.position, frame.rightWrist.rotation);

                        leftRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.leftWrist.position, frame.leftWrist.rotation);
                        rightRiggedVisualJointsArray[1].transform.SetLocalPositionAndRotation(frame.rightWrist.position, frame.rightWrist.rotation);

                        return;
                    }
                }
                else
                {
                    //Log.Msg("Null-Frame");
                }
            }
            else
            {
                //Log.Msg($"Frameiterator exceeds handtracking dataset size. Frameiterator: {frameIterator}");
            }
        }
        else
        {
            //Log.Msg("Empty handtracking dataset (Align hands)");
        }

        return;
    }

    // Press button: Simply replicate captured hand pose at associated point in time
    private void SpawnPressButtonInstruction(bool leftUsed, bool rightUsed)
    {
        // For press button: Simply replicate authors hand at the moment of button press
        if (leftUsed)
        {
            // Clone left hand and place at associated location
            GameObject clonedLeft = Instantiate(leftHand, QRAuth.instance.qrTransform, false);
            clonedLeft.SetActive(false);

            // Add to list of spawned instructions
            generated3dInstructions.Add(clonedLeft);
        }

        if (rightUsed)
        {
            // Clone right hand and place at associated location
            GameObject clonedRight = Instantiate(rightHand, QRAuth.instance.qrTransform, false);
            clonedRight.SetActive(false);

            // Add to list of spawned instructions
            generated3dInstructions.Add(clonedRight);
        }

        //Log.Msg($"Generated press button instruction.");


        return;
    }

    // General hand pose instruction (e.g. pick)
    private void SpawnStaticHandInstruction(bool leftUsed, bool rightUsed)
    {
        if (leftUsed)
        {
            // Clone left hand and place at associated location
            GameObject clonedLeft = Instantiate(leftHand, QRAuth.instance.qrTransform, false);
            clonedLeft.SetActive(false);

            // Add to list of spawned instructions
            generated3dInstructions.Add(clonedLeft);
        }

        if (rightUsed)
        {
            // Clone right hand and place at associated location
            GameObject clonedRight = Instantiate(rightHand, QRAuth.instance.qrTransform, false);
            clonedRight.SetActive(false);

            // Add to list of spawned instructions
            generated3dInstructions.Add(clonedRight);
        }

        //Log.Msg($"Generaed static hand instruction.");

        return;
    }

    // Place: Place arrow at target location
    private void SpawnPlaceInstruction(bool leftUsed, bool rightUsed)
    {
        // For place: Place hand at place position
        if (leftUsed && rightUsed)
        {
            // Position instruction in between two hands
            Vector3 targetPos = (indexTipLeft.position + indexTipRight.position) / 2f;

            // Instantiate straight arrow
            GameObject instruction = Instantiate(straightArrowPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = straightArrowDefaultDims;
            instruction.transform.position = targetPos + straightArrowOffset;
            // Make rotation gravity-aligned
            instruction.transform.up = Vector3.up;

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }
        else if (leftUsed)
        {
            // Position instruction in the middle between index and middle tip
            Vector3 targetPos = (indexTipLeft.position + middleTipLeft.position) / 2f;

            // Orient instruction according to index proximal
            //Quaternion targetRot = indexProximalRotations[0];
            //Quaternion targetRot = Quaternion.identity;

            // Instantiate straight arrow
            GameObject instruction = Instantiate(straightArrowPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = straightArrowDefaultDims;
            instruction.transform.position = targetPos + straightArrowOffset;
            // Make rotation gravity-aligned
            instruction.transform.up = Vector3.up;

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }
        else if (rightUsed)
        {
            // Position instruction in the iddle between index and middle tip
            Vector3 targetPos = (indexTipRight.position + middleTipRight.position) / 2f;

            // Orient instruction according to index proximal
            //Quaternion targetRot = indexProximalRotations[1];
            //Quaternion targetRot = Quaternion.identity;

            // Instantiate straight arrow
            GameObject instruction = Instantiate(straightArrowPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = straightArrowDefaultDims;
            instruction.transform.position = targetPos + straightArrowOffset;
            // Make rotation gravity-aligned
            instruction.transform.up = Vector3.up;

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        //Log.Msg($"Generated place instruction.");


        return;
    }

    // Rotate switch: Place circular arrow at rotation location based on index proximal (knuckle)
    private void SpawnRotateSwitchInstruction(bool leftUsed, bool rightUsed, bool cwRot)
    {
        if (leftUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = (thumbProxLeft.position + indexProxLeft.position) / 2f;

            // Orient instruction according to index proximal
            Quaternion targetRot = thumbProxLeft.rotation;

            // Add rotational offset so that arrow is parallel to object interacted with
            targetRot *= Quaternion.Euler(-circularArrowRotOffset);

            // Adjust rotation based on clockwise or counter-clockwise rotation
            //if (cwRot)
            if (true)
            {
                // Rotate by 180 degrees
                targetRot *= Quaternion.Euler(0f, 180f, 0f);
            }

            // Instantiate circular arrow
            GameObject instruction = Instantiate(circularArrowPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = circularArrowDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        if (rightUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = (thumbProxRight.position + indexProxRight.position) / 2f;

            // Orient instruction according to index proximal
            Quaternion targetRot = thumbProxRight.rotation;

            // Add rotational offset so that arrow is parallel to object interacted with
            targetRot *= Quaternion.Euler(circularArrowRotOffset);

            // Adjust rotation based on clockwise or counter-clockwise rotation
            //if (cwRot)
            if (true)
            {
                // Rotate by 180 degrees
                targetRot *= Quaternion.Euler(0f, 180f, 0f);
            }

            // Instantiate circular arrow
            GameObject instruction = Instantiate(circularArrowPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = circularArrowDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        //Log.Msg($"Generated rotate switch instruction.");

        return;
    }

    // Use screwdriver: Place screwdriver at rotation location
    private void SpawnUseScrewdriverInstruction(bool leftUsed, bool rightUsed, bool cwRot)
    {
        if (leftUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = middleMetaLeft.position;
            // Offset so that screwdriver rests in hand palm
            targetPos += middleMetaLeft.rotation * Vector3.up * sdPosOffLeft.x;
            targetPos += middleMetaLeft.rotation * Vector3.right * sdPosOffLeft.y;
            targetPos += middleMetaLeft.rotation * Vector3.up * sdPosOffLeft.z;

            // Orient instruction according to index proximal
            Quaternion targetRot = middleMetaLeft.rotation;
            // Offset so that screwdriver rests in hand palm
            targetRot *= Quaternion.Euler(sdRotOffLeft);

            // Instantiate screwdriver
            GameObject instruction = Instantiate(cwRot ? screwdriverCwPrefab : screwdriverCcwPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = screwdriverDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        if (rightUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = middleMetaRight.position;
            // Offset so that screwdriver rests in hand palm
            targetPos += middleMetaRight.rotation * Vector3.up * sdPosOffRight.x;
            targetPos += middleMetaRight.rotation * Vector3.right * sdPosOffRight.y;
            targetPos += middleMetaRight.rotation * Vector3.up * sdPosOffRight.z;

            // Orient instruction according to index proximal
            Quaternion targetRot = middleMetaRight.rotation;
            // Rotate by 180 degrees to align with right hand
            targetRot *= Quaternion.Euler(0f, 180f, 0f);
            // Offset so that screwdriver rests in hand palm
            targetRot *= Quaternion.Euler(sdRotOffRight);

            // Instantiate screwdriver
            GameObject instruction = Instantiate(cwRot ? screwdriverCwPrefab : screwdriverCcwPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = screwdriverDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }
    }

    // Use Allen key: Place Allen key at rotation location
    private void SpawnUseAllenInstruction(bool leftUsed, bool rightUsed, bool cwRot)
    {
        if (leftUsed)
        {
            // Average position of the two joints
            Vector3 targetPos = (thumbTipLeft.position + pinkyKnuckleLeft.position) / 2f;

            // Create line direction from thumb to pinky
            Vector3 lineDirection = (pinkyKnuckleLeft.position - thumbTipLeft.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lineDirection, pinkyKnuckleLeft.rotation * Vector3.up);

            // Apply offset
            targetPos += targetRot * Vector3.up * akPosOffLeft.x;
            targetPos += targetRot * Vector3.right * akPosOffLeft.y;
            targetPos += targetRot * Vector3.forward * akPosOffLeft.z;
            targetRot *= Quaternion.Euler(akRotOffLeft);

            // Instantiate Allen key
            GameObject instruction = Instantiate(cwRot ? allenKeyCwPrefab : allenKeyCcwPrefab, QRAuth.instance.qrTransform, false);
            //GameObject instruction = Instantiate(allenKeyCwPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = allenKeyDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        if (rightUsed)
        {
            // Average position of the two joints
            Vector3 targetPos = (thumbTipRight.position + pinkyKnuckleRight.position) / 2f;

            // Create line direction from thumb to pinky
            Vector3 lineDirection = (pinkyKnuckleRight.position - thumbTipRight.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lineDirection, pinkyKnuckleRight.rotation * Vector3.up);

            // Apply offset
            targetPos += targetRot * Vector3.up * akPosOffRight.x;
            targetPos += targetRot * Vector3.right * akPosOffRight.y;
            targetPos += targetRot * Vector3.forward * akPosOffRight.z;
            targetRot *= Quaternion.Euler(akRotOffRight);

            // Instantiate Allen key
            GameObject instruction = Instantiate(cwRot ? allenKeyCwPrefab : allenKeyCcwPrefab, QRAuth.instance.qrTransform, false);
            //GameObject instruction = Instantiate(allenKeyCwPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = allenKeyDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }
    }

    // Use wrench: Place wrench at rotation location
    private void SpawnUseWrenchInstruction(bool leftUsed, bool rightUsed)
    {
        if (leftUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = middleMetaLeft.position;
            // Offset so that Allen key rests in hand palm
            targetPos += middleMetaLeft.rotation * Vector3.up * wrenchPosOffLeft.x;
            targetPos += middleMetaLeft.rotation * Vector3.right * wrenchPosOffLeft.y;
            targetPos += middleMetaLeft.rotation * Vector3.up * wrenchPosOffLeft.z;

            // Orient instruction according to index proximal
            Quaternion targetRot = middleMetaLeft.rotation;
            // Offset so that Allen key rests in hand palm
            targetRot *= Quaternion.Euler(wrenchRotOffLeft);

            // Instantiate Allen key
            GameObject instruction = Instantiate(wrenchPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = wrenchDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }

        if (rightUsed)
        {
            // Position instruction according to index proximal
            Vector3 targetPos = middleMetaRight.position;
            // Offset so that Allen key rests in hand palm
            targetPos += middleMetaRight.rotation * Vector3.up * wrenchPosOffRight.x;
            targetPos += middleMetaRight.rotation * Vector3.right * wrenchPosOffRight.y;
            targetPos += middleMetaRight.rotation * Vector3.up * wrenchPosOffRight.z;

            // Orient instruction according to index proximal
            Quaternion targetRot = middleMetaRight.rotation;
            // Offset so that Allen key rests in hand palm
            targetRot *= Quaternion.Euler(wrenchRotOffRight);

            // Instantiate Allen key
            GameObject instruction = Instantiate(wrenchPrefab, QRAuth.instance.qrTransform, false);
            instruction.SetActive(false);

            // Set local scale, position, and rotation
            instruction.transform.localScale = wrenchDefaultDims;
            instruction.transform.SetPositionAndRotation(targetPos, targetRot);

            // Add to list of spawned instructions
            generated3dInstructions.Add(instruction);
        }
    }

    #endregion


    #region UI callbacks

    // Debugging: Set tracking fps
    public void SetTrackingFps(int userSpecifiedFps)
    {
        sampleFps = userSpecifiedFps;
        sampleInterval = 1f / sampleFps;

        //Log.Msg($"Set handtracking fps to {sampleFps}");
    }

    #endregion
}

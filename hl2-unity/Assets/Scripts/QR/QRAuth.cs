using Microsoft.MixedReality.OpenXR;
using MixedReality.Toolkit.UX;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

[System.Serializable]
public class StorablePose
{
    public Vector3 position;
    public Quaternion rotation;
}

public class QRAuth : MonoBehaviour
{
    public static QRAuth instance;

    #region Properties

    [Tooltip("ARMarkerManager script. attached to object which holds XROrigin (Camera Offset)")]
    public  ARMarkerManager arMarkerManager;

    [Tooltip("Visualizer material for good tracking quality (green)")]
    public Material goodTrackingMat;
    [Tooltip("Visualizer material for bad tracking quality (red)")]
    public Material badTrackingMat;

    [Tooltip("Tracking ID of 1st QR-Code")]
    private TrackableId idQr1;
    [Tooltip("Tracking ID of 2nd QR-Code")]
    private TrackableId idQr2;
    [Tooltip("Required payload of 1st QR-Code (for anchoring complete session)")]
    public string payloadQr1;
    [Tooltip("Required payload of 2nd QR-Code (for anchoring additional e.g. toolbox)")]
    public string payloadQr2;

    //[Tooltip("Renderer component on QR-Code visualizer")]
    //private MeshRenderer visRenderer;
    //[Tooltip("Renderer components on QR-Code visualizer")]
    //private List<MeshRenderer> visRenderer = null;

    // Properties for stabilizing qr-code tracking
    [Tooltip("Size of each position bin in meters")]
    public float posRes = 0.005f;
    [Tooltip("Size of each rotation bin in degrees")]
    public float rotRes = 2.0f;
    [Tooltip("Counter for number of times qr-code should be tracked until saturated")]
    public int trackingSaturationCounter = 100;
    [Tooltip("Counter for number of times qr-code has been tracked")]
    private int trackingCounter = 0;
    [Tooltip("Timestamp of tracking initialization (in seconds since startup)")]
    private float initTrackingTime = 0f;

    // Class for pose key, accessing pose bins
    public struct PoseKey
    {
        public Vector3Int posBin;
        public Vector3Int rotBin;

        public PoseKey(Vector3 pos, Vector3 euler, float pRes, float rRes)
        {
            posBin = new Vector3Int(
                Mathf.RoundToInt(pos.x / pRes),
                Mathf.RoundToInt(pos.y / pRes),
                Mathf.RoundToInt(pos.z / pRes)
            );
            rotBin = new Vector3Int(
                Mathf.RoundToInt(euler.x / rRes),
                Mathf.RoundToInt(euler.y / rRes),
                Mathf.RoundToInt(euler.z / rRes)
            );
        }
    }

    // Class for qr-code bin of tracked poses
    public class PoseBin
    {
        public List<Vector3> positions = new List<Vector3>();
        public List<Quaternion> rotations = new List<Quaternion>();
    }

    // Container for qr-code pose bins
    private Dictionary<PoseKey, PoseBin> poseVotes = new Dictionary<PoseKey, PoseBin>();

    [Tooltip("Reference transform of last tracked qrcode")]
    public Transform qrTransform;

    [Tooltip("Duration after successful tracking for which qr code visuals remain visible (in seconds)")]
    public float qrVisualsStayActiveFor = 3f;

    [Tooltip("Visuals outlining qr-code")]
    private List<MeshRenderer> visRenderer;

    [Tooltip("Child object of qr-code transform housing all visual content (MarkerContent)")]
    public GameObject markerContent;


    #endregion


    #region UI elements

    [Tooltip("QR code panel")]
    public GameObject qrcodePanel;
    [Tooltip("Handmenu object")]
    public GameObject handmenu;
    [Tooltip("Button to finish setup of authoring panels poses")]
    public GameObject finishSetupButton;
    [Tooltip("Button to place authoring panels at previously stored pose relative to qr-code")]
    public GameObject placePanelsButton;
    [Tooltip("Rescan button displayed on top of QR")]
    public PressableButton rescanButton;

    #endregion


    #region Unity lifecycle

    private void Awake()
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

    // Debugging
    //private void Start()
    //{
    //    InitializeQRCodeTracking();
    //}

    #endregion


    #region Methods

    // Initialize qrcode tracking
    public void InitializeQRCodeTracking()
    {
        // Reset tracking related stuff
        // Remove any existing previous markers
        //foreach (var trackable in arMarkerManager.trackables)
        //{
        //    Destroy(trackable.gameObject);
        //}
        // Reset binning
        trackingCounter = 0;
        poseVotes.Clear();
        // Reset relevant QR-code IDs
        idQr1 = new TrackableId();
        idQr2 = new TrackableId();

        // Enable QR-Code tracking if previously never enabled
        if (arMarkerManager.enabled == false)
        {
            arMarkerManager.enabled = true;
        }

        // Store time of initialization in seconds since startup
        initTrackingTime = Time.realtimeSinceStartup;

        // Subscribe to marker change events
        arMarkerManager.markersChanged += OnQRCodesChanged;

        // Hide/Disable rescan button (if previously active)
        if (rescanButton != null)
        {
            rescanButton.transform.parent.gameObject.SetActive(false);
        }

        // Set visualizer material to bad tracking material by default (if previously scanned)
        if (visRenderer.Count != 0)
        {
            foreach (var rend in visRenderer)
            {
                rend.material = badTrackingMat;
            }
        }

        //Log.Msg($"Initialized qrcode tracking. Currently {arMarkerManager.trackables.count} markers tracked.");
    }

    // Callback for change events
    // Only entered if TrackingState -> Tracking
    void OnQRCodesChanged(ARMarkersChangedEventArgs args)
    {
        foreach (ARMarker qrCode in args.added)
        {
            //Log.Msg($"Detected QR code with payload: {arMarkerManager.GetDecodedString(qrCode.trackableId)}, age: {qrCode.lastSeenTime}s");

            // Check if this is a "ghost" from before app launch or recent detection
            //if (qrCode.lastSeenTime < initTrackingTime)
            //{
            //    // This is a ghost -> disable
            //    // Disable all other tracked QR-codes
            //    qrCode.gameObject.SetActive(false);
            //    Destroy(arMarkerManager.trackables[qrCode.trackableId].gameObject);

            //    continue;
            //}

            // Check if relevant qr-code
            if (arMarkerManager.GetDecodedString(qrCode.trackableId) == payloadQr1)
            {
                // This is main anchoring code

                // Store id
                idQr1 = qrCode.trackableId;

                // Set visualizer material to bad tracking material by default
                visRenderer = qrCode.GetComponentsInChildren<MeshRenderer>().Where(t => t.gameObject.CompareTag("qrVis")).ToList();
                foreach (var rend in visRenderer)
                {
                    rend.material = badTrackingMat;
                }

                // Enable relevant qrcode
                qrCode.transform.gameObject.SetActive(true);
            }
            else
            {
                // Disable all other QR-codes with different payload than expected
                qrCode.gameObject.SetActive(false);
            }
        }

        foreach (ARMarker qrCode in args.removed)
        {
            //Log.Msg($"REMOVED QR code with ID: {qrCode.trackableId}");
        }

        foreach (ARMarker qrCode in args.updated)
        {
            //Log.Msg($"Name: {qrCode.name}, time: {qrCode.lastSeenTime}");
            
            // Check if updating relevant QR-code
            if (arMarkerManager.GetDecodedString(qrCode.trackableId) == payloadQr1)
            {
                // Store tracking pose
                qrCode.transform.GetPositionAndRotation(out var pos, out var rot);

                // Generate Key
                PoseKey key = new PoseKey(pos, rot.eulerAngles, posRes, rotRes);

                // Add to Bin
                if (!poseVotes.ContainsKey(key))
                {
                    poseVotes[key] = new PoseBin();
                }
                poseVotes[key].positions.Add(pos);
                poseVotes[key].rotations.Add(rot);

                // Increase tracking counter for eventual saturation
                trackingCounter++;

                // Check if tracking has saturated yet
                if (trackingCounter >= trackingSaturationCounter)
                {
                    FinalizeStablePose(qrCode);

                    // Set visualizer material to good tracking material
                    visRenderer = qrCode.GetComponentsInChildren<MeshRenderer>().Where(t => t.gameObject.CompareTag("qrVis")).ToList();
                    foreach (var rend in visRenderer)
                    {
                        rend.material = goodTrackingMat;
                    }
                    //Log.Msg("Update marker visuals to indicate tracked state (green)");

                    // Cache marker content object
                    markerContent = qrCode.transform.Find("MarkerContent").gameObject;
                    //Log.Msg($"MarkerContent object: {(markerContent == null ? "None" : markerContent.name)}");

                    // Only if initial tracking
                    if (qrcodePanel.activeSelf)
                    {
                        //Log.Msg("Moving on to authoring");

                        // Update UI
                        if (WebSocketClient.instance.assistedAuthMode)
                        {
                            //UIAuth.instance.aVideoAuthPanel.SetActive(true);
                            UIAuth.instance.aStepPreview.SetActive(true);
                        }
                        else
                        {
                            //UIAuth.instance.mTextAuthPanel.SetActive(true);
                            //UIAuth.instance.mVideoAuthPanel.SetActive(true);
                            //UIAuth.instance.mAuth3dPanel.SetActive(true);
                            UIAuth.instance.mStepPreview.SetActive(true);
                        }

                        // In both cases (manual and assisted)
                        qrcodePanel.SetActive(false);
                        finishSetupButton.SetActive(true);
                        placePanelsButton.SetActive(true);
                        handmenu.SetActive(true);
                    }
                }
            }
        }
    }

    // Save current authoring panel setup relative to qr-code
    public void SaveAuthoringPanelPoses()
    {
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Save authoring text panel pose (relative to qr)
            StorablePose storablePose = new StorablePose
            {
                position = qrTransform.InverseTransformPoint(UIAuth.instance.aVideoAuthPanel.transform.position),
                rotation = Quaternion.Inverse(qrTransform.rotation) * UIAuth.instance.aVideoAuthPanel.transform.rotation
            };
            string jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("aAuthVideoPose", jsonPose);
        }
        else
        {
            // Save authoring text panel pose (relative to qr)
            StorablePose storablePose = new StorablePose
            {
                position = qrTransform.InverseTransformPoint(UIAuth.instance.mTextAuthPanel.transform.position),
                rotation = Quaternion.Inverse(qrTransform.rotation) * UIAuth.instance.mTextAuthPanel.transform.rotation
            };
            string jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("mAuthTextPose", jsonPose);

            // Save authoring video panel pose (relative to qr)
            storablePose.position = qrTransform.InverseTransformPoint(UIAuth.instance.mVideoAuthPanel.transform.position);
            storablePose.rotation = Quaternion.Inverse(qrTransform.rotation) * UIAuth.instance.mVideoAuthPanel.transform.rotation;
            jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("mAuthVideoPose", jsonPose);

            // Save authoring 3d panel pose (relative to qr)
            Transform auth3dpanelT = UIAuth.instance.mAuth3dPanel.transform;
            storablePose.position = qrTransform.InverseTransformPoint(auth3dpanelT.transform.position);
            storablePose.rotation = Quaternion.Inverse(qrTransform.rotation) * auth3dpanelT.transform.rotation;
            jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("m3dPose", jsonPose);
        }
    }

    // Request to place authoring panels at previously stored pose relative to qr-code
    public void SetAuthoringPanelPoses()
    {
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Check if previous pose exists
            if (PlayerPrefs.HasKey("aAuthVideoPose"))
            {
                // Set authoring video panel to stored pose
                string jsonPose = PlayerPrefs.GetString("aAuthVideoPose");
                StorablePose pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.aVideoAuthPanel.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);
            }
        }
        else
        {
            // Check if previous pose exists
            if (PlayerPrefs.HasKey("mAuthTextPose"))
            {
                // Set authoring text panel to stored pose
                string jsonPose = PlayerPrefs.GetString("mAuthTextPose");
                StorablePose pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.mTextAuthPanel.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);

                // Set authoring video panel to stored pose
                jsonPose = PlayerPrefs.GetString("mAuthVideoPose");
                pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.mVideoAuthPanel.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);

                // Set manual authoring 3d panel to stored pose
                jsonPose = PlayerPrefs.GetString("m3dPose");
                pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.mAuth3dPanel.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);
            }
        }
    }

    // Save current guidance panel setup relative to qr-code
    public void SaveReviewPanelPoses()
    {
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Save guidance text panel pose (relative to qr)
            StorablePose storablePose = new StorablePose
            {
                position = qrTransform.InverseTransformPoint(UIAuth.instance.aStepPreview.transform.position),
                rotation = Quaternion.Inverse(qrTransform.rotation) * UIAuth.instance.aStepPreview.transform.rotation
            };
            string jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("aReviewPose", jsonPose);
        }
        else
        {
            // Save guidance text panel pose (relative to qr)
            StorablePose storablePose = new StorablePose
            {
                position = qrTransform.InverseTransformPoint(UIAuth.instance.mStepPreview.transform.position),
                rotation = Quaternion.Inverse(qrTransform.rotation) * UIAuth.instance.mStepPreview.transform.rotation
            };
            string jsonPose = JsonUtility.ToJson(storablePose);
            PlayerPrefs.SetString("mReviewPose", jsonPose);
        }
    }

    // Request to place guidance panels at previously stored pose relative to qr-code
    public void SetReviewPanelPoses()
    {
        if (WebSocketClient.instance.assistedAuthMode)
        {
            // Check if previous pose exists
            if (PlayerPrefs.HasKey("aReviewPose"))
            {
                // Set guidance text panel to stored pose
                string jsonPose = PlayerPrefs.GetString("aReviewPose");
                StorablePose pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.aStepPreview.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);
            }
        }
        else
        {
            // Check if previous pose exists
            if (PlayerPrefs.HasKey("mReviewPose"))
            {
                // Set guidance text panel to stored pose
                string jsonPose = PlayerPrefs.GetString("mReviewPose");
                StorablePose pose = JsonUtility.FromJson<StorablePose>(jsonPose);
                UIAuth.instance.mStepPreview.transform.SetPositionAndRotation(qrTransform.TransformPoint(pose.position), qrTransform.rotation * pose.rotation);
            }
        }
    }

    // Retrack qr-code if something failed or drift
    public void RetrackQR()
    {
        // Reset data
        trackingCounter = 0;

        // Disable rescan button for now
        rescanButton.transform.parent.gameObject.SetActive(false);

        // Init tracking
        InitializeQRCodeTracking();
    }

    #endregion


    #region Helpers

    // Calculate average over positions
    private Vector3 AveragePosition(List<Vector3> positions)
    {
        Vector3 avgPos = Vector3.zero;

        foreach (var pos in positions) 
        {
            avgPos += pos;
        }
        avgPos /= positions.Count;

        //Log.Msg("Calculated avg ar position");

        return avgPos;
    }

    // Calculate average over rotations
    private Quaternion AverageRotation(List<Quaternion> rotations)
    {
        Quaternion avgRot = Quaternion.identity;
        int count = 0;

        foreach (var rot in rotations)
        {
            if (count == 0)
                avgRot = rot;
            else
                avgRot = Quaternion.Slerp(avgRot, rot, 1f / (count + 1));
            count++;
        }

        //Log.Msg("Calculated avg ar rotation");

        return avgRot;
    }

    // Disable qr code after specified amount of time after successful tracking
    //private IEnumerator DisableQRVisualsAfter(float timeUntilDisable)
    //{
    //    // Wait
    //    yield return new WaitForSeconds(timeUntilDisable);

    //    // Disable qr code visuals
    //    foreach (var visual in visRenderer)
    //    {
    //        visual.enabled = false;
    //    }
    //}

    // Lock in qr-code pose
    void FinalizeStablePose(ARMarker qrCode)
    {
        // 1. Find the bin with the highest count (the consensus)
        var bestBinEntry = poseVotes.OrderByDescending(x => x.Value.positions.Count).First();
        PoseBin winner = bestBinEntry.Value;

        // 2. Average ONLY the data in that bin
        Vector3 finalPos = AveragePosition(winner.positions);
        Quaternion finalRot = AverageRotation(winner.rotations);

        // 3. Set the transform and stop
        qrCode.transform.SetPositionAndRotation(finalPos, finalRot);
        qrTransform = qrCode.transform;

        arMarkerManager.markersChanged -= OnQRCodesChanged;
        arMarkerManager.enabled = false;

        //Log.Msg($"Locked Pose! Winning bin had {winner.positions.Count} votes.");

        // Enable rescan button on QR-Code
        rescanButton = qrCode.GetComponentInChildren<PressableButton>(true);
        rescanButton.transform.parent.gameObject.SetActive(true);
        // Add rescan callback
        rescanButton.OnClicked.RemoveAllListeners();
        rescanButton.OnClicked.AddListener(RetrackQR);
    }

    #endregion
}
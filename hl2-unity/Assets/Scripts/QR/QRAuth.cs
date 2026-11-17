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
    [Tooltip("Number of initial qr-code detections discarded (detected at edge of detection range and thus less accurate)")]
    public int discardFirstSamples = 10;
    [Tooltip("Number of qr-code poses collected (after discarded ones) until pose is locked")]
    public int trackingSaturationCounter = 60;
    [Tooltip("Counter for number of times qr-code has been tracked (incl. discarded detections)")]
    private int trackingCounter = 0;
    [Tooltip("Max. distance of a sample position to median position to count as inlier (in meters)")]
    public float maxPosDeviation = 0.01f;
    [Tooltip("Max. angle between a sample rotation and median rotation to count as inlier (in degrees)")]
    public float maxRotDeviation = 3f;
    [Tooltip("Snap qr-code normal to vertical (code lying flat) or to horizontal plane (code hanging on wall) to remove tilt noise")]
    public bool correctTilt = true;
    [Tooltip("Max. deviation of measured qr-code normal from vertical/horizontal for tilt correction to be applied (in degrees, < 45)")]
    public float tiltSnapTolerance = 20f;
    [Tooltip("Timestamp of tracking initialization (in seconds since startup)")]
    private float initTrackingTime = 0f;

    [Tooltip("List of positions of qr code recorded during tracking")]
    private List<Vector3> trackingPositions = new();
    [Tooltip("List of rotations of qr code recorded during tracking")]
    private List<Quaternion> trackingRotations = new();

    [Tooltip("Reference transform of last tracked qrcode")]
    public Transform qrTransform;

    [Tooltip("Duration after successful tracking for which qr code visuals remain visible (in seconds)")]
    public float qrVisualsStayActiveFor = 3f;

    [Tooltip("Visuals outlining qr-code")]
    private List<MeshRenderer> visRenderer = new();

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
        // Reset collected samples
        ResetSamples();
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

        // Subscribe to marker change events (unsubscribe first so that callback is never subscribed twice, e.g. rescan during ongoing scan)
        arMarkerManager.markersChanged -= OnQRCodesChanged;
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
                // This is main anchoring code (id is stored on first reliable update, see below)

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

            // Restart sample collection if relevant qr-code was lost
            if (qrCode.trackableId == idQr1)
            {
                idQr1 = TrackableId.invalidId;
                ResetSamples();
            }
        }

        foreach (ARMarker qrCode in args.updated)
        {
            //Log.Msg($"Name: {qrCode.name}, time: {qrCode.lastSeenTime}");

            // Only consider reliable (non-stale) tracking
            if (qrCode.trackingState != TrackingState.Tracking)
            {
                continue;
            }

            // Only consider relevant qr-code (payload) and only samples of one single marker (id)
            if (idQr1 == TrackableId.invalidId)
            {
                // Relevant marker not known yet (also after rescan, as already known marker is only updated, not added again)
                if (arMarkerManager.GetDecodedString(qrCode.trackableId) != payloadQr1)
                {
                    continue;
                }
                idQr1 = qrCode.trackableId;
            }
            else if (qrCode.trackableId != idQr1)
            {
                continue;
            }

            // Discard first detections (at edge of detection range and thus less accurate)
            trackingCounter++;
            if (trackingCounter <= discardFirstSamples)
            {
                continue;
            }

            // Store tracking pose
            qrCode.transform.GetPositionAndRotation(out var pos, out var rot);
            trackingPositions.Add(pos);
            trackingRotations.Add(rot);

            // Check if enough samples have been collected
            if (trackingPositions.Count >= trackingSaturationCounter)
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

    // Calculate average over rotations (aligned to reference, as q and -q represent same rotation)
    private Quaternion AverageRotation(List<Quaternion> rotations, Quaternion reference)
    {
        Vector4 sum = Vector4.zero;

        foreach (var rot in rotations)
        {
            Vector4 q = new Vector4(rot.x, rot.y, rot.z, rot.w);
            if (Quaternion.Dot(rot, reference) < 0f)
            {
                q = -q;
            }
            sum += q;
        }
        sum.Normalize();

        //Log.Msg("Calculated avg ar rotation");

        return new Quaternion(sum.x, sum.y, sum.z, sum.w);
    }

    // Calculate component-wise median over positions
    private Vector3 MedianPosition(List<Vector3> positions)
    {
        return new Vector3(
            Median(positions.Select(p => p.x)),
            Median(positions.Select(p => p.y)),
            Median(positions.Select(p => p.z)));
    }

    // Calculate median of values
    private float Median(IEnumerable<float> values)
    {
        float[] sorted = values.OrderBy(v => v).ToArray();
        int mid = sorted.Length / 2;

        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2f : sorted[mid];
    }

    // Calculate median rotation (sample with smallest summed angle to all other samples)
    private Quaternion MedianRotation(List<Quaternion> rotations)
    {
        Quaternion medianRot = rotations[0];
        float minAngleSum = float.MaxValue;

        foreach (var candidate in rotations)
        {
            float angleSum = 0f;
            foreach (var other in rotations)
            {
                angleSum += Quaternion.Angle(candidate, other);
            }

            if (angleSum < minAngleSum)
            {
                minAngleSum = angleSum;
                medianRot = candidate;
            }
        }

        return medianRot;
    }

    // Estimate robust pose from collected samples: Reject outliers relative to median pose, then average remaining inliers
    private void EstimateRobustPose(out Vector3 finalPos, out Quaternion finalRot)
    {
        // Robust reference pose
        Vector3 medianPos = MedianPosition(trackingPositions);
        Quaternion medianRot = MedianRotation(trackingRotations);

        // Collect inliers (close to median in both position and rotation)
        List<Vector3> inlierPositions = new();
        List<Quaternion> inlierRotations = new();
        for (int i = 0; i < trackingPositions.Count; i++)
        {
            if (Vector3.Distance(trackingPositions[i], medianPos) <= maxPosDeviation &&
                Quaternion.Angle(trackingRotations[i], medianRot) <= maxRotDeviation)
            {
                inlierPositions.Add(trackingPositions[i]);
                inlierRotations.Add(trackingRotations[i]);
            }
        }

        // Average inliers if at least half of the samples remain, otherwise fall back to median pose
        if (inlierPositions.Count * 2 >= trackingPositions.Count)
        {
            finalPos = AveragePosition(inlierPositions);
            finalRot = AverageRotation(inlierRotations, medianRot);
        }
        else
        {
            finalPos = medianPos;
            finalRot = medianRot;
        }
    }

    // Remove tilt noise using gravity: Snap qr-code normal (local z-axis) to vertical if code is lying flat or to horizontal plane if code is hanging on wall
    private Quaternion CorrectTilt(Quaternion rot)
    {
        Vector3 normal = rot * Vector3.forward;
        float angleToUp = Vector3.Angle(normal, Vector3.up);

        Vector3 target;
        if (angleToUp <= tiltSnapTolerance || angleToUp >= 180f - tiltSnapTolerance)
        {
            // Code lying flat (e.g. on table): Normal points straight up or down
            target = angleToUp <= 90f ? Vector3.up : Vector3.down;
        }
        else if (Mathf.Abs(angleToUp - 90f) <= tiltSnapTolerance)
        {
            // Code hanging (e.g. on wall): Normal lies in horizontal plane, keeping its direction
            target = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
        }
        else
        {
            // Neither lying nor hanging (e.g. slanted surface): No correction
            return rot;
        }

        // Minimal rotation aligning measured normal with target (rotation around normal is preserved)
        return Quaternion.FromToRotation(normal, target) * rot;
    }

    // Reset collected tracking samples
    private void ResetSamples()
    {
        trackingCounter = 0;
        trackingPositions.Clear();
        trackingRotations.Clear();
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
        // 1. Robust estimate of pose from collected samples
        EstimateRobustPose(out Vector3 finalPos, out Quaternion finalRot);

        // 2. Remove tilt noise using gravity
        if (correctTilt)
        {
            finalRot = CorrectTilt(finalRot);
        }

        // 3. Set the transform and stop
        qrCode.transform.SetPositionAndRotation(finalPos, finalRot);
        qrTransform = qrCode.transform;

        arMarkerManager.markersChanged -= OnQRCodesChanged;
        arMarkerManager.enabled = false;

        // Enable rescan button on QR-Code
        rescanButton = qrCode.GetComponentInChildren<PressableButton>(true);
        rescanButton.transform.parent.gameObject.SetActive(true);
        // Add rescan callback
        rescanButton.OnClicked.RemoveAllListeners();
        rescanButton.OnClicked.AddListener(RetrackQR);
    }

    #endregion
}
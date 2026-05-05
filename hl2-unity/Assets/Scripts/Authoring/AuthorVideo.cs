/// <summary>
/// This script is used to capture/author videos using the Windows MediaCapture API
/// </summary>


using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using MixedReality.Toolkit.UX;
using UnityEngine.Video;
using MixedReality.Toolkit;
using System.Collections;


#if ENABLE_WINMD_SUPPORT
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Devices.Enumeration;
using Windows.Media.Editing;
using Windows.Storage.FileProperties;
using System.Collections.Generic;
using System.Linq;
using System;
using Msg;
#endif

public class AuthorVideo : MonoBehaviour
{
    public static AuthorVideo instance;

    #region Class properties

#if ENABLE_WINMD_SUPPORT
    [Tooltip("MediaCapture object required to capture an video")]
    private MediaCapture mediaCapture = null;
    [Tooltip("Device id for media capture initialization using specific resolution")]
    private string deviceId = null;

    [Tooltip("LowLagMediaRecording class used for capturing video")]
    private LowLagMediaRecording mediaRecording = null;
#endif
    // Some file attributes
    public string vidPath = null;
    public string trimmedVidPath = null;
    //[Tooltip("List of paths to video instructions in they order they were authored")]
    //public List<string> trimmedVidPaths = new List<string>();
    private string vidName = null;
    private string trimmedVidName = null;
#if ENABLE_WINMD_SUPPORT
    private StorageFile vidFile;
    private StorageFile trimmedVidFile;
#endif

    [Tooltip("Duration in seconds cut/trimmed from video by default to combat delayed capture stop of voice command (Smaller for not streaming as fps are high)")]
    public float trimLengthNoStreaming = 0.5f;
    [Tooltip("Duration in seconds cut/trimmed from video by default to combat delayed capture stop of voice command (Larger for streaming as fps are reduced)")]
    public float trimLengthStreaming = 0.8f;
    [Tooltip("Minimum video length in seconds to be eligible from trimming")]
    public float minVidLength = 3f;

    [Tooltip("Flag whether currently capturing or not")]
    public bool isCapturing = false;
    [Tooltip("Current resolution")]
    public Vector2Int currentRes;
    [Tooltip("Maximum capture time in seconds")]
    public float maxCaptureTime;
    [Tooltip("Coroutine timing max capture duration")]
    private Coroutine maxCaptureCoroutine = null;

    [Tooltip("Flag indicating if video authored")]
    public bool videoAuthored = false;

    #endregion


    #region UI elements

    // Both manual and assisted authoring modes
    [Tooltip("Speech Interactable for stopping capture in both authoring modes")]
    public StatefulInteractable stopCaptureInteractable;
    [Tooltip("Texture capture video is rendered into for both authoring modes")]
    public Texture videoTexture;
    [Tooltip("Texture for no video available")]
    public Texture2D noVidAvailableTexture;

    // Assisted authoring mode
    [Tooltip("Video player on authoring panel in assisted authoring mode")]
    public VideoPlayer aVideoPlayer;
    [Tooltip("Thumbnail texture for video panel in assisted authoring mode")]
    public RawImage aVideoThumbnail;
    [Tooltip("Text shown if no video available yet in assisted authoring mode")]
    public GameObject aNoVideoText;
    [Tooltip("Text shown after video has been captured to ensure high-quality capture in assisted authoring mode")]
    public GameObject aCheckVideoText;
    //[Tooltip("Checklist for capturing high-quality videos")]
    //public GameObject aCheckVideoText;
    [Tooltip("Button to play captured video in assisted authoring mode")]
    public GameObject aPlayButton;
    // Step preview
    [Tooltip("Video player on step preview in assisted authoring mode")]
    public VideoPlayer aVideoPlayerReview;
    [Tooltip("Thumbnail texture vor video panel when reviewing (in-situ authoring)")]
    public RawImage aVideoThumbnailReview;
    [Tooltip("Play button when reviewing (in-situ authoring)")]
    public GameObject aPlayButtonReview;

    // Manual authoring mode
    [Tooltip("Video player on authoring panel in manual authoring mode")]
    public VideoPlayer mVideoPlayer;
    [Tooltip("Text shown if no video available yet in manual authoring mode")]
    public GameObject mNoVideoText;
    //[Tooltip("Checklist for capturing high-quality videos")]
    //public GameObject mCheckVideoText;
    [Tooltip("Button to play captured video in manual authoring mode")]
    public GameObject mPlayButton;
    // Step preview
    [Tooltip("Video player when reviewing (in-situ authoring)")]
    public VideoPlayer mVideoPlayerReview;
    [Tooltip("Play button when reviewing (in-situ authoring)")]
    public GameObject mPlayButtonReview;



    //[Tooltip("Preview image for capture video")]
    //public RawImage previewVid;
    //[Tooltip("Target texture for captured video")]
    //public Texture targetVideoTexture;
    //[Tooltip("Video player on video preview object during authoring")]
    //public VideoPlayer videoPlayer;
    //[Tooltip("Text shown after video capture to ensure high-quality capture")]
    //public GameObject checkVidText;
    //[Tooltip("Texture for no video available")]
    //public Texture2D noVidAvailableTexture;
    //[Tooltip("Play button (authoring)")]
    //public GameObject playButtonAuthoring;


    // Menu
    [Tooltip("Button to request finishing of authoring video")]
    public PressableButton doneButton;
    [Tooltip("Button for starting video capture")]
    public GameObject startCaptureButton;

    //[Tooltip("Far ray interaction (left)")]
    //public GameObject farRayLeft;
    //[Tooltip("Far ray interaction (right)")]
    //public GameObject farRayRight;

    // Overlay message
    [Tooltip("Text informing user that video is currently being processed")]
    public GameObject vidProcessingText;

    #endregion


    #region Capturing video

    // Set default media capture settings and expose properties to user in UI for configuration
    public async Task InitializeMediaCaptureVideo(bool shared = true, int resX = 1920, int resY = 1080)
    {
#if ENABLE_WINMD_SUPPORT
        // Create (new) media capture object
        mediaCapture = new MediaCapture();

        // Create settings object for this capture object
        var mediaInitSettings = new MediaCaptureInitializationSettings
        {
            SharingMode = shared ? MediaCaptureSharingMode.SharedReadOnly : MediaCaptureSharingMode.ExclusiveControl,
            StreamingCaptureMode = StreamingCaptureMode.Video,
        };

        // Check if accessing in shared mode or not
        if (shared == false)
        {
            // Get media capture device id (if none determined already)
            if (deviceId == null)
            {
                // Find all capture devices
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                foreach (var device in devices)
                {
                    // Check if the device on the requested panel supports Video Profile
                    if (MediaCapture.IsVideoProfileSupported(device.Id) /*&& device.EnclosureLocation.Panel == Panel.Front*/)
                    {
                        // Found a device that supports Video Profiles on expected panel -> Store device id
                        deviceId = device.Id;
                        mediaInitSettings.VideoDeviceId = deviceId;

                        //Log.Msg("Found device id supporting video profiles");

                        break;
                    }
                }
            }
            else
            {
                //Log.Msg("Device id already known");
            }

            // Get available profiles and select the one which matches desired (here: default) framerate
            IReadOnlyList<MediaCaptureVideoProfile> profiles = MediaCapture.FindAllVideoProfiles(deviceId);
            var match = (from profile in profiles
                         from desc in profile.SupportedRecordMediaDescription
                         where desc.Width == resX && desc.Height == resY && Math.Round(desc.FrameRate) == 30
                         select new { profile, desc }).FirstOrDefault();

            // Set media capture settings based on matching profile
            if (match != null)
            {
                mediaInitSettings.VideoProfile = match.profile;
                mediaInitSettings.RecordMediaDescription = match.desc;
            }
            else
            {
                // Could not locate a WVGA 30FPS profile. Using default video recording profile
                mediaInitSettings.VideoProfile = profiles[0];

                //Log.Msg("Could not find matching profile for resolution. Selected default profile");
            }

            // Save resolution
            currentRes = new Vector2Int(resX, resY);
            //Log.Msg("Capturing to resolution: " + resX + "x" + resY);
        }

        try
        {
            // Initialize media capturing using media capture settings
            await mediaCapture.InitializeAsync(mediaInitSettings);

            //Log.Msg($"Initialized media capture in {mediaInitSettings.SharingMode} mode");

            // Create storage location for captured video
            StorageFolder vidFolder = WorkflowManager.instance.currentStepFolder;
            vidName = "video.mp4";
            vidFile = await vidFolder.CreateFileAsync(vidName, CreationCollisionOption.ReplaceExisting);
            vidPath = vidFile.Path;

            // Prepare media recording
            mediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto), vidFile);

            //Log.Msg("Prepared storage and media recording");
        }
        catch (UnauthorizedAccessException e)
        {
            //Log.Msg("No permission to access camera");
        }

        // Callback for failed media capture
        mediaCapture.Failed += MediaCapture_Failed;
#endif
    }

    // Start capturing video
    public async Task StartCapturingVideo()
    {
        // Allow voice based stopping of capture
        stopCaptureInteractable.enabled = true;

#if ENABLE_WINMD_SUPPORT
        try
        {
            // Start capturing video
            await mediaRecording.StartAsync();
        }
        catch
        {
            //Log.Msg("Failed to start media recording");
            UIAuth.instance.StopVideoCaptureRequested();

            return;
        }

        // Flag state as currently capturing
        isCapturing = true;

        // Start timer for maximum capture time
        maxCaptureCoroutine = StartCoroutine(TimeMaxCaptureDuration());
#endif
    }

    // Stop capturing video
    public async Task StopCapturingVideo()
    {
        // Disable further stop requests
        stopCaptureInteractable.enabled = false;

#if ENABLE_WINMD_SUPPORT
        // Stop recording
        await mediaRecording.StopAsync();

        // Finish media recording
        await mediaRecording.FinishAsync();

        // Inform server that capture is terminated
        if (WebSocketClient.instance.connected)
        {
            WebSocketClient.instance.SendMsg(MsgType.CAPTUREEND, string.Empty);
        }

        // Flag as not capturing anymore
        isCapturing = false;

        // Communicate to user that video is being processed
        vidProcessingText.SetActive(true);

        // Stop timer for max duration if not stopped due to max duration
        if (maxCaptureCoroutine != null)
        {
            // Stopped capturing before max duration reached
            StopCoroutine(maxCaptureCoroutine);

            maxCaptureCoroutine = null;

            //Log.Msg("User stopped capture");
        }
        else
        {
            //Log.Msg("Stopped capture due to max. duration reached");
        }

        // Trim video (only in manual authoring mode, otherwise wait for client response)
        if (WebSocketClient.instance.assistedAuthMode == false)
        {
            await TrimVideo();
        }
#endif
    }

    // Trim captured video (e.g. remove last second). Only passed argument if client used hand-detection to detemrine relevant frames
    public async Task TrimVideo(float trimFromStart = 0f, float trimFromEnd = 0f)
    {
#if ENABLE_WINMD_SUPPORT
        // Check if captured video is longer than minimum video length
        VideoProperties videoProps = await vidFile.Properties.GetVideoPropertiesAsync();
        TimeSpan vidDuration = videoProps.Duration;
        if (TimeSpan.FromSeconds(minVidLength) > vidDuration)
        {
            // Don't trim then if video too short
            //Log.Msg("Did not trim video because video not long enough");
            
            return;
        }

        // Create MediaClip from captured video file
        var clip = await MediaClip.CreateFromFileAsync(vidFile);

        // Calculate new video duration (depending on whether we are streaming or not)
        TimeSpan trimDurationStart = TimeSpan.FromSeconds(0f);
        TimeSpan trimDurationEnd = TimeSpan.FromSeconds(0f);

        if (trimFromEnd == 0f)
        {
            // No or 0s trim length specified by client. Trim default
            trimDurationEnd = WebSocketClient.instance.connected ? TimeSpan.FromSeconds(trimLengthStreaming) : TimeSpan.FromSeconds(trimLengthNoStreaming);
        }
        else
        {
            // Client provided trim lengths
            trimDurationStart = TimeSpan.FromSeconds(trimFromStart);
            trimDurationEnd = TimeSpan.FromSeconds(trimFromEnd);
        }

        // Set trim times
        clip.TrimTimeFromEnd = trimDurationEnd;
        clip.TrimTimeFromStart = trimDurationStart;
        //Log.Msg($"Original duration: {clip.OriginalDuration}");
        //Log.Msg($"Trimmed duration: {clip.TrimmedDuration}");

        // Create MediaComposition and add trimmed clip
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        //Log.Msg("Added clip to composition");

        // Create new output file
        StorageFolder vidFolder = WorkflowManager.instance.currentStepFolder;
        trimmedVidName = "trimmed_video.mp4";
        trimmedVidFile = await vidFolder.CreateFileAsync(trimmedVidName, CreationCollisionOption.ReplaceExisting);
        trimmedVidPath = trimmedVidFile.Path;
        //Log.Msg("Created file for trimmed video");

        // Add path to list of instruction paths
        //trimmedVidPaths.Add(trimmedVidPath);

        // Render trimmed video to that file
        await composition.RenderToFileAsync(trimmedVidFile, MediaTrimmingPreference.Precise);
        //Log.Msg("Rendered trimmed video to file");

        // Finished processing video
        vidProcessingText.SetActive(false);

        // Clean up capture
        CleanUpMediaCapture();

        // Display captured video to user
        DisplayCapturedVideo();
#endif
    }

    // Display video to user
    private void DisplayCapturedVideo()
    {
        // Disable processing text
        vidProcessingText.SetActive(false);

#if ENABLE_WINMD_SUPPORT
        // Make sure video exists
        string pathToVidToDisplay = trimmedVidPath ?? vidPath;

        if (pathToVidToDisplay != null)
        {
            // Differentiate between assisted and manual authoring (different authoring panels)
            if (WebSocketClient.instance.assistedAuthMode)
            {
                // Prepare video preview
                PopulateVideoPreview(pathToVidToDisplay, aVideoPlayer);

                // Update UI
                UIAuth.instance.ResetCaptureUI();
                aNoVideoText.SetActive(false);
                aCheckVideoText.SetActive(true);
                //aCheckVideoText.SetActive(true);
            }
            else
            {
                // Prepare video preview
                PopulateVideoPreview(pathToVidToDisplay, mVideoPlayer);

                // Update UI
                UIAuth.instance.ResetCaptureUI();
                mNoVideoText.SetActive(false);
                //mCheckVideoText.SetActive(true);

                //Log.Msg("Assigned captured video to video player");
            }

            // Flag that video authored
            videoAuthored = true;
        }
#else
        Debug.Log("On HL2: Displaying captured video to user");
#endif
    }

    // Clean up media capture resources
    private void CleanUpMediaCapture()
    {
#if ENABLE_WINMD_SUPPORT
        mediaCapture.Dispose();
        mediaCapture = null;
        mediaRecording = null;
#endif
    }

#if ENABLE_WINMD_SUPPORT
    // Callback for failed media capture
    private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
        //Log.Msg("Media capture failed");


        throw new NotImplementedException();
    }
#endif

    #endregion


    #region Manual authoring

    // Reset video related data and UI in assisted authoring mode
    public void ResetVideoAuthManual()
    {
        // Clear file refs
        vidPath = null;
        trimmedVidPath = null;
        vidName = null;
        trimmedVidName = null;
        videoAuthored = false;

        // Clear and reset previews (text and video)
        mVideoPlayer.GetComponent<RawImage>().texture = noVidAvailableTexture;
        mVideoPlayerReview.GetComponent<RawImage>().texture = noVidAvailableTexture;
        mNoVideoText.SetActive(true);
        //mCheckVideoText.SetActive(false);
        mPlayButton.SetActive(false);

        // Untrack videoplayer events for video players
        mVideoPlayer.sendFrameReadyEvents = false;
        mVideoPlayer.loopPointReached -= VideoEnded;

        // Free resources occupied by videoplayers
        mVideoPlayer.Stop();

        //Log.Msg("Reset video data (manual authoring mode)");
    }

    // Stop any ongoing video playback 
    public void ResetOngoingPlaybackManual(VideoPlayer vp)
    {
        // Check if video playing at all
        if (vp.isPlaying)
        {
            // Reset videoplayer to first frame
            vp.Play();
            vp.frame = 1;
            vp.Pause();

            // Enable play button
            //vp.transform.GetChild(0).gameObject.SetActive(true);
        }
    }

    // Callback for playing video during authoring
    public void ManualPlayVideoInAuthoring()
    {
        // Hide play button
        mPlayButton.SetActive(false);

        // Replay captured video
        mVideoPlayer.Play();
    }

    // Callback for playing video during review
    public void ManualPlayVideoInReview()
    {
        // Hide play button
        mPlayButtonReview.SetActive(false);

        // Replay captured video
        mVideoPlayerReview.Play();
    }

    #endregion


    #region Assisted authoring

    // Reset video related data and UI in assisted authoring mode
    public void ResetVideoAuthAssisted()
    {
        // Clear file refs
        vidPath = null;
        trimmedVidPath = null;
        vidName = null;
        trimmedVidName = null;

        // Clear and reset previews (text and video)
        aVideoThumbnail.texture = noVidAvailableTexture;
        aNoVideoText.SetActive(true);
        aCheckVideoText.SetActive(false);
        //aCheckVideoText.SetActive(false);
        aPlayButton.SetActive(false);

        // Untrack videoplayer events for video players
        aVideoPlayer.sendFrameReadyEvents = false;
        aVideoPlayer.loopPointReached -= VideoEnded;

        // Free resources occupied by videoplayers
        aVideoPlayer.Stop();

        //Log.Msg("Reset video data (assisted authoring mode)");
    }

    // Stop any ongoing video playback
    public void ResetOngoingPlaybackAssisted(VideoPlayer vp)
    {
        // Check if video playing at all
        if (vp.isPlaying)
        {
            // Reset videoplayer to first frame
            vp.Play();
            vp.frame = 1;
            vp.Pause();

            // Enable play button
            //vp.transform.GetChild(0).gameObject.SetActive(true);
        }
    }

    // Callback for playing video during authoring
    public void AssistedPlayVideoInAuthoring()
    {
        // Hide play button
        aPlayButton.SetActive(false);

        // Replay captured video
        aVideoPlayer.Play();
    }

    // Callback for playing video during in-situ review
    public void AssistedPlayVideoInReview()
    {
        // Hide play button
        aPlayButtonReview.SetActive(false);

        // Replay captured video
        aVideoPlayerReview.Play();
    }

    #endregion


    #region Both manual and assisted

    // Fill specified video player with specified video path (used for preview during manual and assisted authoring)
    public void PopulateVideoPreview(string pathToVideo, VideoPlayer videoPlayer)
    {
        if (pathToVideo != null)
        {
            // Assign video texture to raw image component on preview object
            videoPlayer.GetComponent<RawImage>().texture = videoTexture;

            // Display captured video thumbnail
            videoPlayer.url = pathToVideo;

            // Prepare video player for playback
            StartCoroutine(PrepareVideoForPlayback(videoPlayer));

            // Direct user's attention to that video)
            UIAuth.instance.visualCue.DirectionalTarget = videoPlayer.gameObject.transform;
        }
    }

    // Fill specified video player with specified video clip (used for showing demo video before authoring)
    public void PopulateDemoVideo(VideoClip demoClip, VideoPlayer videoPlayer)
    {
        if (demoClip != null)
        {
            // Assign video texture to raw image component on preview object
            videoPlayer.GetComponent<RawImage>().texture = videoTexture;

            // Display demo video
            videoPlayer.clip = demoClip;

            // Prepare video player for playback
            StartCoroutine(PrepareVideoForPlayback(videoPlayer));

            // Direct user's attention to that video)
            UIAuth.instance.visualCue.DirectionalTarget = videoPlayer.gameObject.transform;
        }
    }

    // Callback for when video ended (both authoring and review)
    public void VideoEnded(VideoPlayer vp)
    {
        // Reset preview to first frame
        vp.Play();
        vp.frame = 1;
        vp.Pause();

        // Show play button (is first and only child)
        vp.transform.GetChild(0).gameObject.SetActive(true);
    }

    #endregion


    #region Coroutines

    // Timer coroutine for maximum capture duration
    private IEnumerator TimeMaxCaptureDuration()
    {
        // Wait for specified amount of time
        yield return new WaitForSeconds(maxCaptureTime);

        // Free reference
        maxCaptureCoroutine = null;

        // Terminate capture if this point reached
        UIAuth.instance.StopVideoCaptureRequested();
    }

    // Prepare playback of video (independent of authoring mode, provided video player)
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

        // Configure video player for synchronous replay with hand motions
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = false;
        //vp.loopPointReached += VideoEnded;
        vp.isLooping = true;
        vp.playOnAwake = true;

        // Make sure play button is active and enabled
        //vp.transform.GetChild(0).gameObject.SetActive(true);
        //vp.transform.GetChild(0).GetComponent<PressableButton>().enabled = true;
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

    #endregion
}
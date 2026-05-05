///<summary>
/// This script is used to capture/author images using the Windows Media Capture framework (Since PhotoCapture only allows legacy resolution)
/// </summary>


using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using MixedReality.Toolkit.UX;
using System.Runtime.InteropServices.WindowsRuntime;
using static UnityEngine.Analytics.IAnalytic;

#if ENABLE_WINMD_SUPPORT
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.Media;
//using MediaCaptureHelper;
#endif

public class AuthorImage : MonoBehaviour
{
    public static AuthorImage instance;

    #region Class properties

#if ENABLE_WINMD_SUPPORT
    [Tooltip("MediaCapture object required to capture an image")]
    private MediaCapture mediaCapture = null;
    [Tooltip("Flag whether camera is currently being previewed")]
    private bool isPreviewing;
    [Tooltip("Device id for media capture initialization using specific resolution")]
    private string deviceId = null;

    // Preview stuff
    [Tooltip("Video frame defining frame settings for preview frames")]
    VideoFrame videoFrame;
    [Tooltip("Preview frame retrived during preview")]
    VideoFrame previewFrame;
#endif
    // Some file attributes
    public string imgPath;
    private string imgName;
#if ENABLE_WINMD_SUPPORT
    private StorageFile imgFile;
#endif

    [Tooltip("Current resolution")]
    public Vector2Int currentRes;

    // UI
    [Tooltip("Preview of capture image")]
    public RawImage previewImg;
    [Tooltip("List item prefab for listing resolutions")]
    public GameObject resItemPrefab;
    [Tooltip("List displaying available resolutions")]
    public GameObject resList;
    [Tooltip("Button to open resolutions options")]
    public GameObject resButton;
    [Tooltip("Text shown if no image available yet")]
    public GameObject noImgText;
    [Tooltip("Frustum outline object")]
    public GameObject frustumOutline;

    #endregion


    #region Capturing image

    // Set default media capture settings and expose properties to user in UI for configuration
    public async Task InitializeMediaCaptureImage(int resX = 0, int resY = 0)
    {
#if ENABLE_WINMD_SUPPORT
        //Log.Msg("Initializing media capture now...");

        // Create (new) media capture object
        if (mediaCapture != null)
        {
            //Log.Msg("Previous media capture object exists");

            // Dispose previous object
            mediaCapture.Dispose();
            mediaCapture = null;
        }
        mediaCapture = new MediaCapture();
        //Log.Msg("Created new mediacapture object");

        // Get media capture device id (if none determined already)
        if (deviceId == null)
        {
            //Log.Msg("Looking for device id");

            // Find all capture devices
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            foreach (var device in devices)
            {
                // Check if the device on the requested panel supports Video Profile
                if (MediaCapture.IsVideoProfileSupported(device.Id) /*&& device.EnclosureLocation.Panel == Panel.Front*/)
                {
                    // Localted a device that supports Video Profiles on expected panel -> Store device id
                    deviceId = device.Id;

                    ////Log.Msg("Device id: " + deviceId);

                    break;
                }
            }
        } 
        else
        {
            //Log.Msg("Device id already known");
        }

        // Create profile settings for this capture object
        var mediaInitSettings = new MediaCaptureInitializationSettings { VideoDeviceId = deviceId};

        // If no resolution passed, use current res
        resX = currentRes.x;
        resY = currentRes.y;

        // Get available profiles and select the one which matches desired (here: default) framerate
        IReadOnlyList<MediaCaptureVideoProfile> profiles = MediaCapture.FindAllVideoProfiles(deviceId);
        var match = (from profile in profiles
                     from desc in profile.SupportedRecordMediaDescription
                     where desc.Width == resX && desc.Height == resY && Math.Round(desc.FrameRate) == 30
                     select new { profile, desc }).FirstOrDefault();

        //Log.Msg("Set resolution settings to " + resX.ToString() + "x" + resY.ToString());

        // Set media capture settings based on matching profile
        if (match != null)
        {
            mediaInitSettings.VideoProfile = match.profile;
            mediaInitSettings.RecordMediaDescription = match.desc;

            //Log.Msg("Selected profile to match desired resolution");
        }
        else
        {
            // Could not locate a WVGA 30FPS profile. Using default video recording profile
            mediaInitSettings.VideoProfile = profiles[0];

            //Log.Msg("Could not find matching profile for resolution. Selected default profile");
        }

        try
        {
            // Initialize media capturing using media capture settings
            await mediaCapture.InitializeAsync(mediaInitSettings);
        }
        catch (UnauthorizedAccessException e)
        {
            //Log.Msg("No permission to access camera");
        }

        // Callback for failed media capture
        mediaCapture.Failed += MediaCapture_Failed;

        currentRes = new Vector2Int(resX, resY);
        //Log.Msg("Initialized capturing to resolution: " + resX + "x" + resY);
#else
        Debug.Log("On HL2: Initialized media capture object with resolution: " + resX + "x" + resY);
#endif
    }

    // Capture and save image
    public async Task CaptureImage()
    {
#if ENABLE_WINMD_SUPPORT
        // Storage location
        StorageFolder imgFolder = WorkflowManager.instance.currentStepFolder;

        // Filename
        imgName = string.Format("image.jpg", WorkflowManager.instance.stepNumber);

        // Create file
        imgFile = await imgFolder.CreateFileAsync(imgName, CreationCollisionOption.ReplaceExisting);

        //Log.Msg("Created file for image");

        // Store path
        imgPath = imgFile.Path;

        //Log.Msg("Storage path for image: " + imgPath);

        // Capture image logic
        using (var captureStream = new InMemoryRandomAccessStream())
        {
            // Capture image
            await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

            // Create filestream for writing image
            using var fileStream = await imgFile.OpenAsync(FileAccessMode.ReadWrite);
            var decoder = await BitmapDecoder.CreateAsync(captureStream);
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

            var properties = new BitmapPropertySet {
                    { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
                };
            await encoder.BitmapProperties.SetPropertiesAsync(properties);

            // Transfer from memory to file on disk
            await encoder.FlushAsync();
        }

        //Log.Msg("Captured and stored image");

        // Display captured image to user
        await DisplayCapturedImg();
#else
        Debug.Log("On HL2: Just captured an image");
#endif

    }

    // Display last captured image to user
    private async Task DisplayCapturedImg()
    {
#if ENABLE_WINMD_SUPPORT
        // Load image data
        byte[] imgData = await File.ReadAllBytesAsync(imgFile.Path);

        // Create texture for image data (will be resized)
        Texture2D imgTexture = new Texture2D(2, 2);

        // Transfer image data into texture
        bool debug = imgTexture.LoadImage(imgData);
        //Log.Msg(debug ? "Successfully loaded image into texture" : "Could not load image into texture");

        // Assign texture to preview
        previewImg.texture = imgTexture;

        //Log.Msg("Assigned captured image to texture and displayed to user");

        // Disable no-image text
        noImgText.SetActive(false);

        // Allow authoring of next step as this step now has data
        //UIAuth.instance.nextStepButton.enabled = true;
#else
        Debug.Log("On HL2: Displaying captured image to user");
#endif
    }

    // Clean up media capture resources
    public void TerminateMediaCapture()
    {
#if ENABLE_WINMD_SUPPORT
        mediaCapture.Dispose();
        mediaCapture = null;

        // Update UI
        resList.SetActive(false);
        resButton.GetComponent<PressableButton>().ForceSetToggled(false);

        //Log.Msg("Terminated media capture");
#else
        Debug.Log("On HL2: Terminated capturing by freeing resources");
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


    #region UI callbacks

    // Callback for user choosing a resolution from menu (3904x2196, 1952x1100, 1920x1080, 1270x720 in that order)
    public async void UserChoseResolution(int toggleNum)
    {
#if ENABLE_WINMD_SUPPORT

        switch (toggleNum)
        {
            case 0:
                // 3904x2196
                if (currentRes.x != 3904 && currentRes.y != 2196)
                {
                    await InitializeMediaCaptureImage(3904, 2196);
                }

                break;
            case 1:
                // 1952x1100
                if (currentRes.x != 1952 && currentRes.y != 1100)
                {
                    await InitializeMediaCaptureImage(1952, 1100);
                }

                break;
            case 2:
                // 1920x1080
                if (currentRes.x != 1920 && currentRes.y != 1080)
                {
                    await InitializeMediaCaptureImage(1920, 1080);
                }

                break;
            case 3:
                // 1280x720
                if (currentRes.x != 1280 && currentRes.y != 720)
                {
                    await InitializeMediaCaptureImage(1280, 720);
                }

                break;
        }
#else
        Debug.Log("User chose new resolution");
#endif
    }

    // Callback for user previewing capture stream
    public void UserToggledPreview()
    {
        // Toggle frustum outline
        frustumOutline.SetActive(!frustumOutline.activeSelf);
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
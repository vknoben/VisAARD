using UnityEngine;

public class Hololens2SensorStreaming : MonoBehaviour
{
    [Tooltip("Enable Research Mode streams.")]
    private bool enableRM = false;

    [Tooltip("Enable Front Camera stream.")]
    private bool enablePV = false;

    [Tooltip("Enable Microphone stream.")]
    private bool enableMC = false;

    [Tooltip("Enable Spatial Input stream.")]
    private bool enableSI = false;

    [Tooltip("Enable Remote Configuration interface.")]
    private bool enableRC = false;

    [Tooltip("Enable Spatial Mapping interface.")]
    private bool enableSM = false;

    [Tooltip("Enable Scene Understanding interface.")]
    private bool enableSU = false;

    [Tooltip("Enable Voice Input interface.")]
    private bool enableVI = false;

    [Tooltip("Enable Message Queue interface.")]
    private bool enableMQ = false;

    [Tooltip("Enable Extended Eye Tracking Interface.")]
    private bool enableEET = false;

    [Tooltip("Enable Extended Audio Interface.")]
    private bool enableEA = false;

    [Tooltip("Enable Extended Video Interface.")]
    private bool enableEV = false;

    [Tooltip("Enable Guest Message Queue interface.")]
    private bool enableMQX = false;

    [Tooltip("Has hl2ss been initialized yet?")]
    private static bool isInitialized = false;

    //void Start()
    //{
    //    if (isInitialized == false)
    //    {
    //        InitializeStreaming();
    //    }
    //}

    public void InitializeStreaming()
    {
        // Only initialize if not initialized previously already (app crash)
        if (isInitialized)
        {
            return;
        }

        hl2ss.RegisterNamedMutex(hl2ss.Device.PERSONAL_VIDEO, hl2ss.MUTEX_NAME_PV);
        hl2ss.RegisterNamedMutex(hl2ss.Device.EXTENDED_VIDEO, hl2ss.MUTEX_NAME_EV);
        hl2ss.UpdateCoordinateSystem();

        // Only pv streaming needed
        enablePV = true;
        enableSI = true;
        hl2ss.Initialize(enableRM, enablePV, enableMC, enableSI, enableRC, enableSM, enableSU, enableVI, enableMQ, enableEET, enableEA, enableEV, enableMQX);
        
        isInitialized = true;

        //Log.Msg("Initialized hl2ss streaming");
    }

    void Update()
    {
        hl2ss.CheckForErrors();
    }
}
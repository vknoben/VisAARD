using MixedReality.Toolkit.UX;
using Msg;
using NativeWebSocket;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

// All possible message types (this list needs to be synced with pc)
namespace Msg
{
    // Enum containing all possible message types
    public enum MsgType
    {
        DEBUG,              // "debug message" (text)
        IPADDRESS,          // "123.234.345.456" (ip address)
        WFNAME,             // "workflow1" (name of workflow)
        NEWSTEP,            // "3" (step number)
        TEXTINSTRUCT,       // "do this" (textual instruction)
        CAPTURESTART,       // "" (empty)
        CAPTUREEND,         // "" (empty)
        GPTRESULT,          // "{"action": "pick and place", "action_frame": "3"} (json)
        GEMINIRESULT,       // "{"action": "pick and place", "action_frame": "3"} (json)
        AUTHFINISH,         // "" (empty, end of complete authoring process)
        MODE,               // "manual", "assisted" for authoring or "guidance" (mode as string)
        GUIDEFINISH,        // "" (empty)
        SAMPLERATE,         // "15" (sample interval)
        HANDUSED,           // "left/right" (hand used determined by handtracking data)
        VIDEOCONFIRMED,     // "" (empty, user confirmed final video capture)
        ROTDIRECTION,       // "cw/ccw" (rotation direction in case of rotary action)
        TRIMLENGTH,         // "{"from_start": "1.2", "from_end": "2.3"} (trim lengths from start and end)
        REGENERATE,         // "{"step_number": "3", "false_action": "press_button"}" (json)
        AUTHSTART,          // "" (empty, start of complete authoring process)
        NEWSTEPINSITU ,     // "3" (step number for which in-situ instructions are now displayed to user)
        AUTHMODAL,          // "text/video/3d" (user chose to author text, video, or 3D in manual mode)
        AUTHMODALFINISH,    // "" (empty, user finished authoring single instruction modality in manual mode)
        DIDMANIPULATE,      // "" (empty, user manipulated 3D instruction in assisted in-situ authoring)
        DIDEDITTEXT,        // "" (empty, user edited text in assisted in-situ authoring)
        DIDARRANGE          // "" (empty, user arranged instruction panel either in assisted or manual authoring)
    }

    // Json structure of message
    [Serializable]
    public class Message
    {
        // Props
        public MsgType type;
        public string message;

        // Constructor
        public Message(MsgType type, string message)
        {
            this.type = type;
            this.message = message;
        }
    }

    // Json structure of result message (independent of used model)
    [Serializable]
    public class VlmResult
    {
        // Props
        public string action;
        public string[] action_frames;
        public string[] hand_appearance;
        public string hand_used;
        public string rot_direction;
        public string[] interactable_objects;
        public string key_object;
        public string action_explanation;
        public string frame_explanation;
        public string text_instruction;

        // Constructor
        // For short response
        public VlmResult(string action, string[] actionFrame, string handUsed, string rotDirection)
        {
            this.action = action;
            action_frames = actionFrame;
            hand_appearance = null;
            hand_used = handUsed;
            rot_direction = rotDirection;
            interactable_objects = null;
            key_object = null;
            action_explanation = null;
            frame_explanation = null;
            text_instruction = null;
        }

        // For regular response
        public VlmResult(string action, string[] actionFrame, string[] handAppearance, string handUsed, string rotDirection, string[] interactableObjects, string keyObject, string actionExplanation, string frameExplanation, string generatedText)
        {
            this.action = action;
            action_frames = actionFrame;
            hand_appearance = handAppearance;
            hand_used = handUsed;
            rot_direction = rotDirection;
            interactable_objects = interactableObjects;
            key_object = keyObject;
            action_explanation = actionExplanation;
            frame_explanation = frameExplanation;
            text_instruction = generatedText;
        }
    }

    // Json structure of regeneration request
    [Serializable]
    public class RegenInstructRequestObj
    {
        // Props
        public string step_number;
        public string false_action;

        // Constructor
        public RegenInstructRequestObj(string step_number, string false_action)
        {
            this.step_number = step_number;
            this.false_action = false_action;
        }
    }

    // Json structure of trim lengths
    [Serializable]
    public class TrimLengths
    {
        // Props
        public string from_start;
        public string from_end;

        // Constructor
        public TrimLengths(string from_start, string from_end)
        {
            this.from_start = from_start;
            this.from_end = from_end;
        }
    }
}

// This class manages websocket connection with server
// Implements callbacks for connection events
public class WebSocketClient : MonoBehaviour
{
    // Singleton pattern
    public static WebSocketClient instance;

    #region Properties

    // Client/websocket
    public WebSocket websocket;
    [Tooltip("Flag whether currently connected or not (same as websocket.state)")]
    public bool connected = false;

    [Tooltip("Streaming manager containing HL2SS script")]
    public Hololens2SensorStreaming hl2ssComponent;

    [Tooltip("Flag indicating wheter authoring in manual or assisted (VLM-based) mode")]
    public bool assistedAuthMode = false;

    [Tooltip("Sampling rate specified by client. Basically interval between sampled frames.")]
    public int samplingInterval = 15;

    #endregion


    #region UI elements

    [Tooltip("Websocket dialog")]
    public GameObject wsDialog;
    [Tooltip("Information message on websocket dialog")]
    public GameObject infoBox;
    [Tooltip("Subsequent panel after conneciton has been established")]
    public GameObject nextPanel;
    [Tooltip("Connect button")]
    public PressableButton connectButton;
    [Tooltip("Cancel button")]
    public PressableButton cancelButton;

    [Tooltip("Button to start in-situ authoring loop")]
    public PressableButton startInsituButton;

    #endregion


    #region Event handler callbacks

    // Callback for message reception event
    private void OnMessageCallback(byte[] data)
    {
        //Log.Msg("Received message of length: " + data.Length + "Bytes");

        // Decode byte message to string
        string strMsg = System.Text.Encoding.UTF8.GetString(data);

        // Deserialize json
        Message jsonMsg = JsonUtility.FromJson<Message>(strMsg);

        // Process messages depending on message type
        switch (jsonMsg.type)
        {
            case MsgType.SAMPLERATE:
                // Convert string sampling rate to int and save
                if (int.TryParse(jsonMsg.message, out samplingInterval))
                {
                    //Log.Msg($"Client specified a sampling interval of {samplingInterval}");
                }
                else
                {
                    //Log.Msg($"Failed to parse sampling interval specified by client");
                }

                break;
            case MsgType.TRIMLENGTH:
                // Deserialize trim lengths json
                TrimLengths trimJson = JsonUtility.FromJson<TrimLengths>(jsonMsg.message);

                float trimFromStart = 0f;
                float trimFromEnd = 0f;
                try
                {
                    trimFromStart = float.Parse(trimJson.from_start);
                    trimFromEnd = float.Parse(trimJson.from_end);
                }
                catch
                {
                    //Log.Msg("Failed to parse recommended trim lengths. No trimming applied");
                }

                // Start trimming recently captured video
                AuthorVideo.instance.TrimVideo(trimFromStart, trimFromEnd);

                break;
            case MsgType.GPTRESULT:
                //Log.Msg("Received result from ChatGPT");

                // Deserialize vlm result message
                VlmResult gptResult = JsonUtility.FromJson<VlmResult>(jsonMsg.message);

                // Enable start review loop button (in any case for now9
                UIAuth.instance.startControlLoopButton.enabled = true;

                // Get corresponding hand tracking for used hand(s) and initialize automatic 3D instruction generation and placement
                if (UIAuth.instance.waitingForRedoResult)
                {
                    //Log.Msg("This was a redo result");
                    // Unflag
                    UIAuth.instance.waitingForRedoResult = false;

                    // Update text instruction data
                    InstructionManager.Instance.currentInstructionSet.textInstruction = gptResult.text_instruction;
                    //Log.Msg("Updated text instruction");

                    // In review (regenerated result)
                    HandtrackingManager.instance.Regenerate3DInstructions(gptResult.action, gptResult.action_frames, gptResult.hand_used, gptResult.rot_direction);
                    //Log.Msg("Regenerated 3D instructions");

                    // Show updated instructions immediately after generation
                    InstructionManager.Instance.ShowInstructionSet(false);
                    //Log.Msg("Shown instructions");

                    // Update UI
                    UIAuth.instance.aRegenInstructsButton.enabled = true;
                    UIAuth.instance.aRecaptureDemoButton.enabled = true;
                    UIAuth.instance.aNextStepInsituButton.enabled = true;
                    AuthorText.instance.aEditTextButton.GetComponent<PressableButton>().enabled = true;

                    // Enable QR-Code visuals
                    QRAuth.instance.markerContent.SetActive(true);

                    // Play sound
                    UIAuth.instance.aStepPreview.GetComponent<AudioSource>().Play();
                }
                else
                {
                    // Still authoring or somewhere at the beginning of in-situ review (initially generated result)
                    Log.Msg("OG generation");

                    // Load generated text instruction into memory
                    InstructionManager.Instance.TextInstruction = gptResult.text_instruction;
                    //Log.Msg($"Generated text instruction: {gptResult.text_instruction}");

                    // Initialize in-situ instruction based on hand-tracking
                    HandtrackingManager.instance.Initialize3DInstructions(gptResult.action, gptResult.action_frames, gptResult.hand_used, gptResult.rot_direction);
                }

                break;
            default:
                //Log.Msg("Could not identify type of received message");
                break;
        }
    }

    // Callback for connection established event
    private void OnConnectedCallback()
    {
        //Log.Msg("Websocket connection established!");

        // Flag as connected
        connected = true;

        // Update UI
        wsDialog.SetActive(false);
        nextPanel.SetActive(true);

        // Initialize hl2ss
        hl2ssComponent.InitializeStreaming();

        // Inform pc about ip address of this device
        SendMsg(MsgType.IPADDRESS, hl2ss.GetIPAddress());

        // Inform pc about usage mode (manual, assisted, guide)
        if (UIMain.authoring)
        {
            SendMsg(MsgType.MODE, assistedAuthMode ? "assisted" : "manual");
        }
        else
        {
            SendMsg(MsgType.MODE, "guidance");
        }
    }

    // Callback for connection closed event
    private void OnDisconnected(WebSocketCloseCode e)
    {
        //Log.Msg("Websocket connection closed!");

        // Flag as not connected
        connected = false;

        // Go back to main menu
        SceneManager.LoadScene("Main");
    }

    // Callback for error event
    private void OnErrorCallback(string e)
    {
        //Log.Msg("WebSocket connection error: " + e);

        // Update UI
        infoBox.SetActive(true);
        connectButton.enabled = true;
        cancelButton.enabled = true;
    }

    #endregion


    #region Class methods

    // Send json data to server
    public async void SendMsg(MsgType msgType, string msg)
    {
        // Make sure connection is open
        if (websocket.State == WebSocketState.Open)
        {
            // Create message
            Message message = new Message(msgType, msg);

            // Serialize message
            string jsonMsg = JsonUtility.ToJson(message);

            // Send json message
            await websocket.SendText(jsonMsg);
        }
    }

    // Send data message to server
    public async void SendData(byte[] data)
    {
        // Make sure connection is open
        if (websocket.State == WebSocketState.Open)
        {
            // Send data message
            await websocket.Send(data);
        }
    }

    // Send text message to server
    public async void SendText(string text)
    {
        // Make sure connection is open
        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(text);
        }
    }

    // Called if user clicks connect button
    public async void EstablishConnection(string ipAndPort)
    {
        // Update UI
        infoBox.SetActive(false);
        connectButton.enabled = false;
        cancelButton.enabled = false;

        // Server to connect to (format: "ws://ip:port")
        websocket = new WebSocket("ws://" + ipAndPort);

        // Subscribe (+=) function to an event so that it is executed everytime event fires
        websocket.OnOpen += OnConnectedCallback;
        websocket.OnError += OnErrorCallback;
        websocket.OnClose += OnDisconnected;
        websocket.OnMessage += OnMessageCallback;

        Debug.Log("Attempting to connect to websocket server with address: " + "ws://" + ipAndPort);

        // Store ip in player prefs (even if not connected, just last ip attempt)
        PlayerPrefs.SetString("lastIp", ipAndPort);

        // Waiting for messages
        await websocket.Connect();
    }

    // Disconnect client
    public async void DisconnectClient()
    {
        await websocket.Close();
        
        connected = false;
    }

    #endregion


    #region Unity lifecycle

    // Called before start (even if script deactivated)
    private void Awake()
    {
        // Make sure that always only one instance of NWSClient class exists (Singleton pattern)
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists! Destroy.");
            Destroy(this);
        }
    }

    //private void Start()
    //{
    //    // Debug
    //    Message message = new Message(MsgType.IPADDRESS, "123.234.345.456");
    //    string jsonMsg = JsonUtility.ToJson(message);
    //    Log.Msg(jsonMsg);
    //}

    // Called repeatedly
    void Update()
    {
        // Needed so that no message is skipped although it is available in DispatchQueue
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    // Called right before app is terminated
    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            // Close websocket connection if Unity app terminates
            await websocket.Close();
        }
    }

    #endregion
}
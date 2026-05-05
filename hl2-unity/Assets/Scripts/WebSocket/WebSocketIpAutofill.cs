/// <summary>
/// This script fills input field for websocket conneciton with last attempted ip to connect to
/// </summary>

using MixedReality.Toolkit.UX;
using UnityEngine;

public class WebSocketIpAutofill : MonoBehaviour
{
    #region Properties

    #endregion


    #region UI elements

    [Tooltip("Inputfield for websocket address of server (pc)")]
    public MRTKTMPInputField inputField;
    [Tooltip("Connect to websocket button")]
    public PressableButton connectButton;

    #endregion


    #region Unity lifecycle

    private void OnEnable()
    {
        // Check player prefs for any exsiting ips
        if (PlayerPrefs.HasKey("lastIp"))
        {
            // Get ip
            string lastIp = PlayerPrefs.GetString("lastIp");

            // Fill input field with last ip
            inputField.text = lastIp;

            // Enable confirm button then
            connectButton.enabled = true;

            //Log.Msg("Filled inputfield for websocket address with last attempted ip");
        }
        else
        {
            //Log.Msg("No ip entry exists yet");
        }
    }

    #endregion
}

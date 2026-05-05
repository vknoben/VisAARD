using UnityEngine;

// Used for study purposes. To check if user did or did not manipulate object in assisted in-situ authoring or panel in either manual or assisted mode
public class DidManipulate : MonoBehaviour
{
    #region Fields

    [Tooltip("Flag indicating whether user manipulated object or not")]
    private bool didManipulate = false;

    [Tooltip("Flag indicating whether user arranged panel or not")]
    public bool didArrange = false;

    #endregion


    #region Methods

    // Callback for manipulation event
    public void WasManipulated()
    {
        // Make sure to only run when script active
        if (!enabled)
        {
            return;
        }

        if (didManipulate == false)
        {
            // Flag so client only informed once
            didManipulate = true;

            // Inform client
            if (WebSocketClient.instance.connected)
            {
                WebSocketClient.instance.SendMsg(Msg.MsgType.DIDMANIPULATE, string.Empty);
            }

            // Disable this script so it won't be called again
            enabled = false;
        }
    }

    // Callback for arrange event
    public void WasArranged()
    {
        // Make sure to only run when script active
        if (!enabled)
        {
            return;
        }

        if (didArrange == false)
        {
            // Flag so client only informed once
            didArrange = true;

            // Inform client
            if (WebSocketClient.instance.connected)
            {
                WebSocketClient.instance.SendMsg(Msg.MsgType.DIDARRANGE, string.Empty);
            }

            // Disable this script so it won't be called again
            enabled = false;
        }
    }

    #endregion


    #region Unity LifeCycle

    #endregion
}

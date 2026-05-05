/// <summary>
/// Ties enabling of qr code panel to automatic intitialization of qr code tracking
/// </summary>
using UnityEngine;

public class EnableTracking : MonoBehaviour
{
    // Triggered if this object is enabled
    private void OnEnable()
    {
        if (UIMain.authoring)
        {
            // User is authoring instructions (authoring)
            QRAuth.instance.InitializeQRCodeTracking();
        }
        else
        {
            // User is using insturctions (guidance)
            QRGuidance.instance.InitializeQRCodeTracking();
        }
    }
}

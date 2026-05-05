///<summary>
/// This script's sole purpose is to switch between left and right handedness. Default: Right-handed
/// </summary>
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using UnityEngine;
using UnityEngine.Localization.Components;

public class HandednessManager : MonoBehaviour
{
    public static HandednessManager instance;

    #region UI elements

    [Tooltip("Handmenu used throughout application")]
    public GameObject handmenu;
    [Tooltip("Toggle for handedness on handmenu")]
    public GameObject handednessToggle;

    #endregion


    #region Properties

    [Tooltip("Current handedness. Right-handed by default")]
    public static bool rightHanded = true;

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

        // Set handedness based on previous settings from main scene
        if (rightHanded)
        {
            SetRightHandedness();
        }
        else
        {
            SetLeftHandedness();
        }
    }

    #endregion


    #region Methods

    public void SetRightHandedness()
    {
        //Log.Msg("Switching to right handedness");

        // Flag as righty
        rightHanded = true;

        // Set handedness to right (handmenu in left hand)
        handmenu.GetComponent<SolverHandler>().TrackedHandedness = MixedReality.Toolkit.Handedness.Left;

        // Switch logo and label
        handednessToggle.GetComponentInChildren<LocalizeStringEvent>().SetEntry("Right");
        handednessToggle.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 12";
    }

    public void SetLeftHandedness()
    {
        //Log.Msg("Switching to left handedness");

        // Flag as lefty
        rightHanded = false;

        // Set handedness to left (handemenu in right hand)
        handmenu.GetComponent<SolverHandler>().TrackedHandedness = MixedReality.Toolkit.Handedness.Right;

        // Switch logo and label
        handednessToggle.GetComponentInChildren<LocalizeStringEvent>().SetEntry("Left");
        handednessToggle.GetComponentInChildren<FontIconSelector>().CurrentIconName = "Icon 13";
    }

    #endregion
}
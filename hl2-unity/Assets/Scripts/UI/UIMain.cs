/// <summary>
/// This class manages most stuff regarding UI and its events
/// </summary>

using UnityEngine;
using UnityEngine.SceneManagement;

public class UIMain : MonoBehaviour
{
    // Singleton
    public static UIMain instance;

    // Static variable denoting whether authoring or guiding
    public static bool authoring = true;

    #region UI elements

    // Handmenu left
    [Tooltip("General handmenu")]
    public GameObject handmenu;
    [Tooltip("Handmenu canvas which is toggled by handtracking detection")]
    public GameObject handmenuCanvas;
    [Tooltip("Toggle debug panel")]
    public GameObject debugToggle;
    [Tooltip("Language toggle")]
    public GameObject languageToggle;
    [Tooltip("Toggle for left or right handedness")]
    public GameObject handednessToggle;

    // Debug panels
    [Tooltip("Panel displaying debug messages")]
    public GameObject debugPanel;

    // System pop ups
    [Tooltip("Dialog for selecting mode (authoring or guidance)")]
    public GameObject modeDialog;

    // Tutorial UI elements
    [Tooltip("Panel for starting tutorial")]
    public GameObject startTutorialDialog;

    #endregion


    #region Class properties


    #endregion


    #region UI callbacks

    // User chose authoring mode
    public void AuthoringSelected()
    {
        // Flag as authoring
        authoring = true;

        // Load corresponding scene
        SceneManager.LoadScene("Authoring");
    }

    // User chose guidance mode
    public void GuidanceSelected()
    {
        // Flag as guiding
        authoring = false;

        SceneManager.LoadScene("Guidance");
    }

    // User chose tutorial
    public void TutorialSelected()
    {
        // Update UI
        modeDialog.SetActive(false);
        startTutorialDialog.SetActive(true);
    }

    #endregion


    #region Debug stuff

    public void DebugWindowToggled()
    {
        //Log.Msg("Showing debug panel now");

        // Position debug panel right in front of user
        Transform mainCamTransform = Camera.main.transform;
        Vector3 userHeadPos = mainCamTransform.position;
        Vector3 start = userHeadPos + mainCamTransform.transform.forward;
        Vector3 end = new Vector3(start.x, userHeadPos.y, start.z);
        Vector3 parallelToGroundInGazeDirection = (end - userHeadPos).normalized;
        debugPanel.transform.position = userHeadPos + parallelToGroundInGazeDirection;
        debugPanel.transform.LookAt(2 * debugPanel.transform.position - mainCamTransform.transform.position);

        // Show debug panel
        debugPanel.SetActive(true);

        // Enable untoggle for debug panel
        debugToggle.SetActive(false);
    }

    public void DebugWindowUntoggled()
    {
        //Log.Msg("Debug panel hidden");

        // Hide debug panel
        debugPanel.SetActive(false);

        // Enable toggle for debug panel
        debugToggle.SetActive(true);
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

        HandednessManager.rightHanded = true;
    }

    // Start is called before first Update
    void Start()
    {
        // Set up UI in general (which elements (in-)active at the beginning)
        handmenuCanvas.SetActive(false);
        startTutorialDialog.SetActive(false);
        modeDialog.SetActive(true);
        handmenu.SetActive(true);
    }

    #endregion
}
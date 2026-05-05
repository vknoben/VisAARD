using TMPro;
using UnityEngine;

public class Log : MonoBehaviour
{
    // Singleton
    public static Log instance;

    #region Class properties

    [Tooltip("Custom logger content window")]
    public GameObject contentWindow;
    [Tooltip("Custom logger text element template")]
    public GameObject textPrefab;

    [Tooltip("Custom logger content window")]
    private static GameObject content;
    [Tooltip("Custom logger text element template")]
    private static GameObject text;

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

    // Start is called before Update
    void Start()
    {
        // Cache content window & text prefab
        content = contentWindow;
        text = textPrefab;
    }

    #endregion


    #region Class methods

    // Custom logging method
    public static void Msg(string msg)
    {
#if ENABLE_WINMD_SUPPORT
        // Create new debug text element
        GameObject debugTextObj = Instantiate(text);

        // Configure text object
        debugTextObj.GetComponent<TMP_Text>().text = msg;

        // Add new debug text element to (top of) content window
        debugTextObj.transform.SetParent(content.transform, false);
        debugTextObj.transform.SetAsFirstSibling();
#else
        Debug.Log(msg);
#endif
    }

#endregion
}

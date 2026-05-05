///<summary>
/// This script's sole purpose is to switch between languages. For now: English, German
/// </summary>
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LocaleManager : MonoBehaviour
{
    public static LocaleManager instance;

    #region Properties

    // Possible locales
    public enum LocaleTypes
    {
        German,
        English
    }

    [Tooltip("Currently selected locale")]
    public static LocaleTypes currentLocale = LocaleTypes.German;

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

        // Set German locale by default
        SetLocaleGerman();
    }

    #endregion


    #region Methods

    public void SetLocaleEnglish()
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[(int)LocaleTypes.English];
        currentLocale = LocaleTypes.English;
    }

    public void SetLocaleGerman()
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[(int)LocaleTypes.German];
        currentLocale = LocaleTypes.German;
    }

    #endregion
}
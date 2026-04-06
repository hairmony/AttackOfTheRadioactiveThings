using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// On load (e.g. stealth hub): if <see cref="TDProgress.AllTDComplete"/>, loads the credits scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class TDCompleteRedirect : MonoBehaviour
{
    const string PrefsRedirectDoneKey = "TD_AllComplete_CreditsRedirectOnce";

    [Tooltip("Must match a scene in Build Settings.")]
    [SerializeField] string creditsSceneName = "CreditsMenu";

    [Tooltip("If true, auto-redirect to credits only the first time all TD levels are complete.")]
    [SerializeField] bool redirectOnlyOnce = true;

    void Start()
    {
        if (!TDProgress.AllTDComplete)
            return;

        if (redirectOnlyOnce && PlayerPrefs.GetInt(PrefsRedirectDoneKey, 0) != 0)
            return;

        if (redirectOnlyOnce)
        {
            PlayerPrefs.SetInt(PrefsRedirectDoneKey, 1);
            PlayerPrefs.Save();
        }

        if (string.IsNullOrEmpty(creditsSceneName))
            return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(creditsSceneName);
    }
}

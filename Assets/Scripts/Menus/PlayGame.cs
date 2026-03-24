using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayGame : MonoBehaviour
{
    public void PlayGamne()
    {
        SceneManager.LoadSceneAsync("StealthConcept");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

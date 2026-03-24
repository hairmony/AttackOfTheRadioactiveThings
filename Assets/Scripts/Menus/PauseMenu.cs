using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;
    bool isPaused = false;

    public void Pause()
    {
        pauseMenu.SetActive(true);
        isPaused = true;
        Time.timeScale=0;
    }

    public void Resume()
    {
        pauseMenu.SetActive(false);
        isPaused = false;
        Time.timeScale=1;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Time.timeScale=1;
    }

    public void Exit()
    {
        SceneManager.LoadScene("MainMenu");
        Time.timeScale=1;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
            Debug.Log("ESC PRESSED");
    }
}

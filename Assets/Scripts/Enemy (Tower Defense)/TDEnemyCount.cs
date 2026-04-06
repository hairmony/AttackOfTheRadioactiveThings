using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TDEnemyCount : MonoBehaviour
{
    //Set to static Instance so that scripts referencing these variables don't need to create a local script variable
    //Must include gameObject in scene with this script attachted; No clue if loading scenes breaks this counter
    public static TDEnemyCount Instance { get; set; }

    [SerializeField] private GameObject sceneTransition;

    [Header("Progress (TDLevel1–4)")]
    [Tooltip("1–4 = this stage index. 0 = infer from scene name TDLevelN.")]
    [SerializeField] [Range(0, 4)] int tdLevelNumber;
    [Tooltip("Mirrors saved completion for this stage (updated on load and on victory).")]
    [SerializeField] bool levelComplete;

    private int eCount = 0; //Tracks current # of enemies
    private int eSpawned = 0; //Tracks how many spawned
    private int eTotal = 0; //Tracks how many will spawn
    private int eDefeat = 0; //Tracks how many were defeated

    private void Awake()
    {
        //Simpleton stuff
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        //DontDestroyOnLoad(gameObject); //Allows for objects to persist between scenes
        SyncLevelCompleteFromSave();
    }

    void SyncLevelCompleteFromSave()
    {
        int n = ResolveTdLevelNumber();
        if (n >= 1 && n <= 4)
            levelComplete = TDProgress.IsComplete(n);
    }

    int ResolveTdLevelNumber()
    {
        if (tdLevelNumber >= 1 && tdLevelNumber <= 4)
            return tdLevelNumber;
        return TDProgress.TryGetTdLevelNumberFromSceneName(SceneManager.GetActiveScene().name, out int fromName)
            ? fromName
            : 0;
    }

    void RecordVictoryProgress()
    {
        int n = ResolveTdLevelNumber();
        if (n < 1 || n > 4)
            return;
        TDProgress.MarkComplete(n);
        levelComplete = true;
    }

    public void IncrementCount() {  eCount++; }
    public void DecrementCount() { eCount--; }
    public void IncrementSpawnCount() {  eSpawned++; }
    public void IncrementDefeat() { eDefeat++; }
    public void SetTotal(int total) { eTotal = total; }
    public int GetCount() { return eCount; }
    public int GetDefeatCount() { return eDefeat; }
    public void CheckVictory()
    {
        //All enemies defeated
        if (eDefeat == eTotal)
        {
            Debug.Log("All enemies cleared");
            RecordVictoryProgress();
            TryGoBack();
            return;
        }
        //All enemies spawned but NOT all defeated
        if (eCount == 0 && eSpawned == eTotal)
        {
            Debug.Log("All enemies managed"); //This message will always print
            RecordVictoryProgress();
            TryGoBack();
        }
    }

    void TryGoBack()
    {
        if (sceneTransition == null)
            return;
        var nav = sceneTransition.GetComponent<SceneNavigator>();
        if (nav != null)
            nav.GoBackToPreviousScene();
    }
}

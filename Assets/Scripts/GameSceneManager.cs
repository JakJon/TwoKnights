using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string campSceneName = "Camp";
    [SerializeField] private string gameSceneName = "Main";
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDelay = 2f; // Delay before scene transition
    [SerializeField] private bool showDeathMessage = true;

    [Header("Victory Settings")]
    [SerializeField] private float victoryDelay = 5f; // Banner time before returning to camp
    [SerializeField] private int gateVictoryGold = 100;
    [SerializeField] private int trueVictoryGold = 250;

    private bool isTransitioningToCamp;
    public bool IsTransitioningToCamp => isTransitioningToCamp;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when the player dies - shows the death screen (or falls back to camp)
    /// </summary>
    public void OnPlayerDeath()
    {
        OnPlayerDeath(null, null);
    }

    /// <summary>
    /// Called when the player dies. knightName e.g. "Left Knight",
    /// causeOfDeath e.g. "Wolf" - both feed the death screen.
    /// </summary>
    public void OnPlayerDeath(string knightName, string causeOfDeath)
    {
        if (isTransitioningToCamp)
        {
            return;
        }

        isTransitioningToCamp = true;

        Debug.Log($"Player died! {knightName ?? "A Knight"} killed by {causeOfDeath ?? "unknown"}.");

        if (WaveManager.ActiveInstance != null)
        {
            int reached = WaveManager.ActiveInstance.CurrentWaveNumber;
            SaveManager.Data.furthestWave = Mathf.Max(SaveManager.Data.furthestWave, reached);
            SaveManager.Save();
        }

        HideUpgradeMenuIfNeeded();

        StartCoroutine(HandlePlayerDeath(knightName, causeOfDeath));
    }

    /// <summary>
    /// Called when a run ends in victory (first gate clear or true-boss kill):
    /// banner, gold reward, then back to camp.
    /// </summary>
    public void OnVictory(MapDefinition map, bool trueVictory, int wavesCompleted)
    {
        if (isTransitioningToCamp)
        {
            return;
        }

        isTransitioningToCamp = true;

        SaveManager.Data.furthestWave = Mathf.Max(SaveManager.Data.furthestWave, wavesCompleted);
        SaveManager.Save();

        GoldManager.Instance?.AddGold(trueVictory ? trueVictoryGold : gateVictoryGold);

        HideUpgradeMenuIfNeeded();

        string banner;
        if (trueVictory)
        {
            string mapName = map != null ? map.DisplayName : "The map";
            banner = $"VICTORY!\n{mapName} is cleansed";
        }
        else
        {
            string bossName = map != null && map.GateBoss != null ? map.GateBoss.WaveName : "The boss";
            banner = $"VICTORY!\n{bossName} has fallen";
        }

        StartCoroutine(HandleVictory(banner));
    }

    private IEnumerator HandleVictory(string banner)
    {
        var waveNameDisplay = FindFirstObjectByType<WaveName>(FindObjectsInactive.Include);
        if (waveNameDisplay != null)
        {
            waveNameDisplay.DisplayWaveName(banner);
        }

        yield return new WaitForSecondsRealtime(victoryDelay);

        LoadCampScene();
    }

    /// <summary>
    /// Load the camp scene directly
    /// </summary>
    public void LoadCampScene()
    {
        Time.timeScale = 1f; // Reset time scale in case it was paused
        SceneManager.LoadScene(campSceneName);
    }

    /// <summary>
    /// Load the main game scene
    /// </summary>
    public void LoadGameScene()
    {
        Time.timeScale = 1f; // Reset time scale in case it was paused
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Reload the current scene
    /// </summary>
    public void ReloadCurrentScene()
    {
        Time.timeScale = 1f; // Reset time scale in case it was paused
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator HandlePlayerDeath(string knightName, string causeOfDeath)
    {
        // Let the death moment land before covering the screen
        yield return new WaitForSecondsRealtime(transitionDelay);

        var deathScreen = showDeathMessage
            ? FindFirstObjectByType<DeathScreen>(FindObjectsInactive.Include)
            : null;

        if (deathScreen != null)
        {
            // Freeze the battlefield; the death screen buttons restore
            // timeScale via LoadCampScene/LoadGameScene
            Time.timeScale = 0f;
            int runGold = GoldManager.Instance != null ? GoldManager.Instance.RunGold : 0;
            deathScreen.Show(knightName, causeOfDeath, runGold);
        }
        else
        {
            LoadCampScene();
        }
    }

    private void HideUpgradeMenuIfNeeded()
    {
        // Ensure time scale resumes so coroutines using scaled time can finish
        Time.timeScale = 1f;

        var upgradeMenu = FindFirstObjectByType<UpgradeMenu>(FindObjectsInactive.Include);
        if (upgradeMenu != null)
        {
            upgradeMenu.SetMenuVisible(false);
        }

        var spawner = FindFirstObjectByType<Spawner>();
        if (spawner != null)
        {
            spawner.HandlePlayerDeathTransition();
        }
    }

    /// <summary>
    /// Check if a scene exists in the build settings
    /// </summary>
    public bool SceneExists(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (name == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Validate scene names on start
    /// </summary>
    private void Start()
    {
        if (!SceneExists(campSceneName))
        {
            Debug.LogWarning($"Camp scene '{campSceneName}' not found in build settings! Make sure to create it and add it to the build settings.");
        }
        
        if (!SceneExists(gameSceneName))
        {
            Debug.LogWarning($"Game scene '{gameSceneName}' not found in build settings!");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isTransitioningToCamp = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
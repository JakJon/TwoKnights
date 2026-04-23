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
    /// Called when the player dies - transitions to the camp scene
    /// </summary>
    public void OnPlayerDeath()
    {
        if (isTransitioningToCamp)
        {
            return;
        }

        isTransitioningToCamp = true;

        Debug.Log("Player died! Transitioning to camp...");

        if (WaveManager.ActiveInstance != null)
        {
            int reached = WaveManager.ActiveInstance.CurrentWaveNumber;
            SaveManager.Data.furthestWave = Mathf.Max(SaveManager.Data.furthestWave, reached);
            SaveManager.Save();
        }

        HideUpgradeMenuIfNeeded();

        StartCoroutine(HandlePlayerDeath());
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

    private IEnumerator HandlePlayerDeath()
    {
        // Optional: Show death message or play death animation
        if (showDeathMessage)
        {
            Debug.Log("You have fallen in battle...");
            // You can add UI elements here later for death screen
        }

        // Wait for the specified delay
        yield return new WaitForSecondsRealtime(transitionDelay);

        // Load the camp scene
        LoadCampScene();
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
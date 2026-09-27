using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string campSceneName = "Camp";
    [SerializeField] private string gameSceneName = "Main";
    
    [Header("Transition Settings")]
    [SerializeField] private bool showDeathMessage = true;

    [Header("Victory Settings")]
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

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.deathSting);

        Debug.Log($"Player died! {knightName ?? "A Knight"} killed by {causeOfDeath ?? "unknown"}.");

        int waveReached = 1;
        string waveName = null;
        if (WaveManager.ActiveInstance != null)
        {
            waveReached = WaveManager.ActiveInstance.CurrentWaveNumber;
            waveName = WaveManager.ActiveInstance.CurrentWave != null
                ? WaveManager.ActiveInstance.CurrentWave.WaveName
                : null;
            if (!IsTestRun())
            {
                SaveManager.Data.furthestWave = Mathf.Max(SaveManager.Data.furthestWave, waveReached);
                SaveManager.Save();

                // Per map, and in waves CLEARED rather than reached: the wave the
                // knight died on was not beaten, and the level select's star is a
                // claim about what this map has actually been taken through.
                var diedOn = WaveManager.ActiveInstance.CurrentMap;
                if (diedOn != null)
                {
                    MapProgressStore.RecordWavesCleared(diedOn.MapId,
                        WaveManager.ActiveInstance.CompletedWavesCount);
                }
            }
        }

        // Commit anything counted since the last wave boundary — the run is over
        // and the camp is about to read these back
        PlayerStats.Flush();

        HideUpgradeMenuIfNeeded();

        HandlePlayerDeath(knightName, causeOfDeath, waveReached, waveName);
    }

    /// <summary>
    /// Called when a run ends in victory (first gate clear or true-boss kill):
    /// fanfare, gold reward, then straight back to camp.
    /// </summary>
    public void OnVictory(MapDefinition map, bool trueVictory, int wavesCompleted)
    {
        if (isTransitioningToCamp)
        {
            return;
        }

        isTransitioningToCamp = true;

        // Freeze the backdrop on the stage the run was actually played in. The
        // boss kill already advanced CurrentWaveNumber past the gate, so without
        // this the NEXT stage's backdrop (forest -> deep forest) would fade up
        // behind the victory banner — spoiling, on the very run that earned it,
        // the reveal that belongs to the first run brave enough to go deeper.
        BackgroundController.Instance?.Hold();

        if (!IsTestRun())
        {
            SaveManager.Data.furthestWave = Mathf.Max(SaveManager.Data.furthestWave, wavesCompleted);
            SaveManager.Save();

            if (map != null) MapProgressStore.RecordWavesCleared(map.MapId, wavesCompleted);
        }

        // The boss kill that ended the run is counted but not yet on disk
        PlayerStats.Flush();

        GoldManager.Instance?.AddGold(trueVictory ? trueVictoryGold : gateVictoryGold);

        HideUpgradeMenuIfNeeded();

        // Straight to the camp: the fanfare plays over the transition, and the
        // banner that used to need holding time is gone.
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.victoryFanfare);
        LoadCampScene();
    }

    // A wave jumped to via Test Mode shouldn't count as legitimate progress
    private static bool IsTestRun()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return TestRunConfig.ActiveRun;
#else
        return false;
#endif
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

    // No pause before the screen goes up. The two seconds this used to hold were
    // meant to let the death land, but the knight is already dead and the arena is
    // already still — all the wait did was make the player sit through it.
    private void HandlePlayerDeath(string knightName, string causeOfDeath, int waveReached, string waveName)
    {
        var deathScreen = showDeathMessage
            ? FindFirstObjectByType<DeathScreen>(FindObjectsInactive.Include)
            : null;

        if (deathScreen != null)
        {
            // Freeze the battlefield; the death screen buttons restore
            // timeScale via LoadCampScene/LoadGameScene
            Time.timeScale = 0f;
            int runGold = GoldManager.Instance != null ? GoldManager.Instance.RunGold : 0;
            deathScreen.Show(knightName, causeOfDeath, runGold, waveReached, waveName);
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

        // The BOOT scene never comes through OnSceneLoaded — this object is created
        // as part of that load, so it subscribes after the event it would want. Every
        // later camp load is the handler's; this first one is ours. PlayMusic no-ops
        // on a repeat, so the two paths overlapping costs nothing.
        if (SceneManager.GetActiveScene().name == campSceneName) PlayCampMusic();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isTransitioningToCamp = false;

        // The camp owns its track from here. The GAME scene deliberately does not
        // start anything: which track a run opens on depends on the map and the
        // wave, and only the Spawner knows both — see Spawner.UpdateWaveMusic. It
        // asks on the first wave, which is the same beat the backdrop comes up on.
        if (scene.name == campSceneName) PlayCampMusic();
    }

    /// <summary>
    /// Called on every camp load, including the return from a run, and again by the
    /// file select as it closes. PlayMusic no-ops when the track is already on, so
    /// the walk from camp to camp via the level select never restarts it mid-phrase.
    /// </summary>
    public void PlayCampMusic()
    {
        var catalog = MapCatalog.Instance;
        if (catalog == null || AudioManager.Instance == null) return;

        // The file select is silent. Both halves of this matter and they cover
        // different moments: IsShowing is the reopen from the camp's Exit button,
        // while ShouldShowAtBoot covers the boot, where the panel may not have run
        // its Show() yet — Start() order between this object and CampMenuController
        // is undefined, so asking only whether the panel is visible would win or
        // lose the race depending on the day. FileSelectPanel.Hide() is what turns
        // the music on once a file has actually been chosen.
        if (FileSelectPanel.ShouldShowAtBoot || FileSelectPanel.IsShowing) return;

        AudioManager.Instance.PlayMusic(catalog.CampMusic);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
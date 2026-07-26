using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Spawner : MonoBehaviour
{
    public enum ArcDirection { Clockwise, CounterClockwise }

    [SerializeField] private WaveManager waveManager;
    [SerializeField] private WaveName waveNameDisplay;
    [SerializeField] private UpgradeMenu upgradeMenu; // New reference to upgrade menu
    [SerializeField] private UpgradeManager upgradeManager; // Reference to upgrade manager
    [SerializeField] private QuestCompletePanel questCompletePanel; // Shown between wave end and upgrade menu
    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] public GameObject brownRat;
    [SerializeField] public GameObject greyRat;
    [SerializeField] public GameObject blackRat;
    [SerializeField] public GameObject slimePrefab;
    [SerializeField] public GameObject bat;
    [SerializeField] public GameObject greyWolfPrefab;
    [SerializeField] public GameObject brownWolfPrefab;
    [SerializeField] public GameObject blackWolfPrefab;
    [SerializeField] public GameObject darkBat;
    [Tooltip("Every Nth bat of a wave spawns as a dark bat once past the gate boss. Deterministic — wave content must never roll dice")]
    [SerializeField] private int darkBatInterval = 4;
    private int _batCallCount; // bat spawn calls this wave, in wave-script order
    [SerializeField] public GameObject healthOrbPrefab;
    [SerializeField] public GameObject manaOrbPrefab;
    [Tooltip("Mine-map track builder. Waves ask for a RailLayout through Spawner.Rails.")]
    [SerializeField] private RailNetwork railNetwork;

    private Transform _leftPlayer;
    private Transform _rightPlayer;
    private bool _isWaveInProgress;
    private bool _isUpgradeMenuActive; // Track if upgrade menu is showing

    #region common references
    // Public methods for spawning that can be used by wave classes
    public Transform LeftPlayer => _leftPlayer;
    public Transform RightPlayer => _rightPlayer;
    public Vector2 aboveLeftPlayer => new Vector2(-2, 7);
    public Vector2 aboveRightPlayer => new Vector2(2, 7);
    public Vector2 belowLeftPlayer => new Vector2(-2, -7);
    public Vector2 belowRightPlayer => new Vector2(2, -7);
    public Vector2 leftOfLeftPlayer => new Vector2(-12, 0);
    public Vector2 rightOfRightPlayer => new Vector2(12, 0);
    public Vector2 topLeftCorner => new Vector2(-12, 6);
    public Vector2 topRightCorner => new Vector2(12, 6);
    public Vector2 bottomLeftCorner => new Vector2(-12, -6);
    public Vector2 bottomRightCorner => new Vector2(12, -6);

    // Null on maps with no track in the scene — wave scripts must null-check
    public RailNetwork Rails
    {
        get
        {
            if (railNetwork == null)
                railNetwork = FindFirstObjectByType<RailNetwork>(FindObjectsInactive.Include);
            return railNetwork;
        }
    }
    #endregion

    void Awake()
    {
        // UpgradeManager is a ScriptableObject, so owned/applied upgrade state
        // survives scene reloads; clear it so each run starts fresh
        if (upgradeManager != null)
        {
            upgradeManager.ResetRunState();
        }
    }

    void Start()
    {
        _leftPlayer = GameObject.FindWithTag("PlayerLeft").transform;
        _rightPlayer = GameObject.FindWithTag("PlayerRight").transform;
        
        // Setup upgrade menu callback
        if (upgradeMenu != null)
        {
            upgradeMenu.OnUpgradeConfirmed += OnUpgradeConfirmed;
        }

        if (questCompletePanel == null)
        {
            questCompletePanel = FindFirstObjectByType<QuestCompletePanel>(FindObjectsInactive.Include);
        }

        // Fresh per-run state (wave count, boss flags, wave pool) even without
        // a domain reload — matters for restarting runs in builds
        waveManager.BeginRun();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ApplyTestRunConfigIfPending();
#endif

        StartNextWave();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Dev-only Test Mode: consume a setup authored in the camp — jump the wave
    // counter and pre-apply the chosen upgrades before the first wave spawns.
    private void ApplyTestRunConfigIfPending()
    {
        TestRunConfig.ActiveRun = TestRunConfig.Pending;
        if (!TestRunConfig.Pending) return;

        waveManager.ApplyTestStart(TestRunConfig.StartWave);

        // Interleave L/R picks so UpgradeManager's turn alternation ends up in
        // a natural state; each list is already ordered lowest tier first.
        var left = TestRunConfig.LeftUpgrades;
        var right = TestRunConfig.RightUpgrades;
        int most = Mathf.Max(left.Count, right.Count);
        for (int i = 0; i < most; i++)
        {
            if (i < left.Count && left[i] != null && upgradeManager != null)
                upgradeManager.ApplyUpgrade(left[i], KnightTarget.LeftKnight);
            if (i < right.Count && right[i] != null && upgradeManager != null)
                upgradeManager.ApplyUpgrade(right[i], KnightTarget.RightKnight);
        }

        Debug.Log($"[TestMode] Starting at wave {TestRunConfig.StartWave} with " +
                  $"{left.Count} left / {right.Count} right upgrade levels.");
        TestRunConfig.Clear();
    }
#endif

    public void StartNextWave()
    {
        if (_isWaveInProgress || _isUpgradeMenuActive)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test runs choose every wave by hand (or via the AutoPickWave hook)
        if (TestRunConfig.ActiveRun)
        {
            if (_isWavePickerActive) return;
            StartCoroutine(PickNextWaveThenStart());
            return;
        }
#endif

        BeginWave(waveManager.SelectNextWave());
    }

    private void BeginWave(BaseWave nextWave)
    {
        if (nextWave == null) return;
        if (waveNameDisplay != null)
            waveNameDisplay.DisplayWaveName(nextWave.GetFormattedWaveName(waveManager.CurrentWaveNumber));
        _isWaveInProgress = true;
        _batCallCount = 0; // dark-bat cadence restarts every wave
        // Last wave's track comes down as this one starts, so rails stay up
        // through the wave-complete beat and the upgrade menu
        if (Rails != null) Rails.ClearAll();
        StartCoroutine(RunWave(nextWave));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _isWavePickerActive;

    // Dev-only Test Mode: before each wave, surface everything the selector
    // could pick (scheduled boss + playable pool) and let the tester decide.
    private IEnumerator PickNextWaveThenStart()
    {
        _isWavePickerActive = true;

        // Death-screen retry: force the wave that killed you, once
        var retryWave = TestRunConfig.RetryWave;
        if (retryWave != null)
        {
            TestRunConfig.RetryWave = null;
            _isWavePickerActive = false;
            waveManager.ForceSelectWave(retryWave);
            BeginWave(retryWave);
            yield break;
        }

        var scheduledBoss = waveManager.PeekScheduledBoss();
        var pool = waveManager.GetPlayableCandidates();
        var locked = waveManager.GetLockedCandidates();

        // Automation hook: a named wave (or "*" for weighted random) skips the
        // UI entirely so MCP-driven validation never stalls on input
        string auto = TestRunConfig.AutoPickWave;
        if (!string.IsNullOrEmpty(auto))
        {
            BaseWave match = null;
            if (auto != "*")
            {
                if (scheduledBoss != null && string.Equals(scheduledBoss.name, auto, System.StringComparison.OrdinalIgnoreCase))
                    match = scheduledBoss;
                foreach (var wave in pool)
                {
                    if (match != null) break;
                    if (string.Equals(wave.name, auto, System.StringComparison.OrdinalIgnoreCase))
                        match = wave;
                }
                // Locked waves are fair game for automation — forcing a wave
                // outside its unlock window is a legitimate test
                foreach (var wave in locked)
                {
                    if (match != null) break;
                    if (string.Equals(wave.name, auto, System.StringComparison.OrdinalIgnoreCase))
                        match = wave;
                }
                if (match == null)
                    Debug.LogWarning($"[TestMode] AutoPickWave '{auto}' matched no candidate; using weighted random.");
            }
            _isWavePickerActive = false;
            if (match != null)
            {
                waveManager.ForceSelectWave(match);
                BeginWave(match);
            }
            else
            {
                BeginWave(waveManager.SelectNextWave());
            }
            yield break;
        }

        var picker = TestWavePicker.GetOrCreate();
        if (picker == null)
        {
            // No UI to piggyback on — behave like a normal run
            _isWavePickerActive = false;
            BeginWave(waveManager.SelectNextWave());
            yield break;
        }

        BaseWave chosen = null;
        bool decided = false;

        Time.timeScale = 0f;
        picker.Show(scheduledBoss, pool, locked, waveManager.CurrentWaveNumber, wave =>
        {
            chosen = wave;
            decided = true;
        });
        while (!decided) yield return null;
        Time.timeScale = 1f;

        _isWavePickerActive = false;

        if (chosen != null)
        {
            waveManager.ForceSelectWave(chosen);
            BeginWave(chosen);
        }
        else
        {
            BeginWave(waveManager.SelectNextWave()); // "Random" row / B button
        }
    }
#endif

    private IEnumerator RunWave(BaseWave wave)
    {
        // Start wave tracking
        wave.StartWaveTracking();
        
        // Run the wave spawn logic
        yield return StartCoroutine(wave.SpawnWave(this));
        
        // Wait for all enemies to be killed (if enemy tracking is enabled)
        yield return StartCoroutine(wave.WaitForAllEnemiesDead());
        
        // End wave tracking
        wave.EndWaveTracking();
        
        _isWaveInProgress = false;
        waveManager.WaveCompleted();

        // Ember fire dies with the wave, not the run. Scorched Earth zones never
        // expire on their own, so without this the next wave would begin inside
        // the last one's inferno and the difficulty curve would invert.
        FireField.ClearAll();

        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp)
        {
            yield break;
        }

        // Boss outcomes: a first gate clear or a true-boss kill ends the run in victory
        var outcome = waveManager.ConsumePendingOutcome();
        if (outcome != RunOutcome.None && GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnVictory(waveManager.CurrentMap, outcome == RunOutcome.TrueVictory,
                waveManager.CompletedWavesCount);
            yield break;
        }

        // Let the wave-complete fanfare ring over the cleared arena before any
        // menus appear
        yield return new WaitForSeconds(2f);

        // Celebrate quests completed during the wave, one panel each,
        // before the upgrade menu appears
        if (questCompletePanel != null && questCompletePanel.HasPending)
        {
            Time.timeScale = 0f;
            yield return StartCoroutine(questCompletePanel.ShowPendingPanels());
            Time.timeScale = 1f;
        }

        // Show upgrade menu and pause game instead of immediately starting next wave
        ShowUpgradeMenu();
    }

    private void ShowUpgradeMenu()
    {
        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp)
        {
            _isUpgradeMenuActive = false;
            return;
        }

        _isUpgradeMenuActive = true;
        
        // Pause the game
        Time.timeScale = 0f;
        
        // Show the upgrade menu
        if (upgradeMenu != null)
        {
            upgradeMenu.SetMenuVisible(true);
        }
    }

    private void OnUpgradeConfirmed(int upgradeIndex, KnightTarget selectedKnight)
    {
        BaseUpgrade selectedUpgrade = upgradeMenu.GetChosenUpgrade();
        
        if (selectedUpgrade != null && upgradeManager != null)
        {
            upgradeManager.ApplyUpgrade(selectedUpgrade, selectedKnight);
        }
        else
        {
            Debug.Log($"Upgrade {upgradeIndex} was confirmed for {selectedKnight}");
        }
        
        // Hide upgrade menu
        if (upgradeMenu != null)
        {
            upgradeMenu.SetMenuVisible(false);
        }
        
        // Resume game
        Time.timeScale = 1f;
        _isUpgradeMenuActive = false;
        
        // Start next wave
        StartNextWave();
    }

    public void HandlePlayerDeathTransition()
    {
        _isUpgradeMenuActive = false;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // Clean up event subscription
        if (upgradeMenu != null)
        {
            upgradeMenu.OnUpgradeConfirmed -= OnUpgradeConfirmed;
        }
    }

    // Brown/black rats and black wolves are deep-forest enemies: they only
    // spawn once the run is past the map's gate boss (the rat king on wave 10).
    // Earlier waves get the grey stand-in instead, so wave assets whose unlock
    // windows straddle the boss stay playable on both sides of it.
    private bool EliteTierUnlocked
    {
        get
        {
            if (waveManager == null) return true;
            var map = waveManager.CurrentMap;
            int gateBossWave = map != null ? map.GateBossWaveNumber : 10;
            return waveManager.CurrentWaveNumber > gateBossWave;
        }
    }

    public void SpawnRat(Vector2 targetPosition, GameObject ratType, float delay, Transform playerTarget, bool bypassTierGate = false, Vector2? entryPoint = null)
    {
        StartCoroutine(SpawnRatAfterDelay(targetPosition, ratType, delay, playerTarget, bypassTierGate, entryPoint));
    }

    private IEnumerator SpawnRatAfterDelay(Vector2 targetPosition, GameObject ratType, float delay, Transform playerTarget, bool bypassTierGate, Vector2? entryPoint)
    {
        yield return new WaitForSeconds(delay);
        // bypassTierGate lets the rat king summon his brown brood mid-fight
        if (!bypassTierGate && !EliteTierUnlocked && (ratType == brownRat || ratType == blackRat))
        {
            ratType = greyRat;
        }
        GameObject enemy = Instantiate(ratType);
        enemy.transform.position = targetPosition;

        EnemyRat enemyRat = enemy.GetComponent<EnemyRat>();
        if (enemyRat != null)
        {
            enemyRat.InitializeTarget(playerTarget);
            // entryPoint makes the rat scurry in from there (e.g. out of the
            // rat king) instead of walking in from the nearest screen edge
            if (entryPoint.HasValue)
            {
                enemyRat.SetEntryPoint(entryPoint.Value);
            }
        }
    }

    public void SpawnSlime(int size, Vector2 spawnPosition, float delay, Transform targetPlayer)
    {
        StartCoroutine(SpawnSlimeAfterDelay(size, spawnPosition, delay, targetPlayer));
    }

    private IEnumerator SpawnSlimeAfterDelay(int size, Vector2 spawnPosition, float delay, Transform targetPlayer)
    {
        yield return new WaitForSeconds(delay);
        GameObject slime = Instantiate(slimePrefab);
        slime.transform.position = spawnPosition;

        EnemySlime slimeScript = slime.GetComponent<EnemySlime>();
        if (slimeScript != null)
        {
            slimeScript.size = size;
            slimeScript.targetPlayer = targetPlayer;
            slimeScript.InitializeSlime();
        }
    }

    public void SpawnBat(Vector2 spawnPosition, float delay)
    {
        // Decide dark-vs-normal at CALL time, in wave-script order: coroutine
        // wake-up order ties on equal delays, so deciding after the wait would
        // make the pattern non-deterministic. Same wave = same bats, always.
        _batCallCount++;
        GameObject prefab = bat;
        if (EliteTierUnlocked && darkBat != null && darkBatInterval > 0
            && _batCallCount % darkBatInterval == 0)
        {
            prefab = darkBat;
        }
        StartCoroutine(SpawnBatAfterDelay(prefab, spawnPosition, delay));
    }

    private IEnumerator SpawnBatAfterDelay(GameObject prefab, Vector2 spawnPosition, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject enemy = Instantiate(prefab);
        enemy.transform.position = spawnPosition;
    }

    public void SpawnWolf(List<Vector2> waypoints, Transform targetKnight, WolfType wolfType, float delay = 0f)
    {
        StartCoroutine(SpawnWolfAfterDelay(waypoints, targetKnight, wolfType, delay));
    }

    private IEnumerator SpawnWolfAfterDelay(List<Vector2> waypoints, Transform targetKnight, WolfType wolfType, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (wolfType == WolfType.Black && !EliteTierUnlocked)
        {
            wolfType = WolfType.Grey;
        }

        GameObject prefabToUse = null;
        switch (wolfType)
        {
            case WolfType.Grey:  prefabToUse = greyWolfPrefab;  break;
            case WolfType.Brown: prefabToUse = brownWolfPrefab; break;
            case WolfType.Black: prefabToUse = blackWolfPrefab; break;
        }
        if (prefabToUse == null)
        {
            Debug.LogWarning($"Spawner: Missing prefab for wolf type {wolfType}. Please assign it in the Spawner inspector.");
            yield break;
        }
        GameObject wolf = Instantiate(prefabToUse);

        // Convert Vector2 waypoints to Vector3 for EnemyWolf API
        List<Vector3> wp3 = null;
        if (waypoints != null)
        {
            wp3 = new List<Vector3>(waypoints.Count);
            for (int i = 0; i < waypoints.Count; i++)
                wp3.Add(new Vector3(waypoints[i].x, waypoints[i].y, 0f));
        }

        // If we have at least one waypoint, place the wolf there before initialization
        if (wp3 != null && wp3.Count > 0)
        {
            wolf.transform.position = wp3[0];
        }

        EnemyWolf wolfScript = wolf.GetComponent<EnemyWolf>();
        if (wolfScript != null)
        {
            wolfScript.SetWaypoints(wp3);
            wolfScript.SetTarget(targetKnight);
            wolfScript.SetWolfType(wolfType);
        }
        else
        {
            Debug.LogWarning("Spawner: EnemyWolf component not found on wolf instance.");
        }
    }

    public void SpawnProjectile(Transform targetPlayer, Vector2 spawnPosition, float delay = 0f)
    {
        StartCoroutine(SpawnProjectileAfterDelay(targetPlayer, spawnPosition, delay));
    }

    private IEnumerator SpawnProjectileAfterDelay(Transform targetPlayer, Vector2 spawnPosition, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        GameObject projectile = Instantiate(projectilePrefab);
        ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();
        pm.Initialize(targetPlayer, spawnPosition);
    }

    private Vector2 ArcCenterFor(Transform targetPlayer)
    {
        return (targetPlayer == _leftPlayer) ? new Vector2(-2, 0) : new Vector2(2, 0);
    }

    // Seconds an arc-volley projectile takes to reach its knight from arcStart.
    // Mirrors SpawnProjectileArc's geometry so bosses can pace their volleys.
    public float ProjectileArcFlightSeconds(Transform targetPlayer, Vector2 arcStart)
    {
        float radius = Vector2.Distance(arcStart, ArcCenterFor(targetPlayer));
        return radius / projectilePrefab.GetComponent<ProjectileMovement>().Speed;
    }

    public void SpawnProjectileArc(Transform targetPlayer, ArcDirection direction, Vector2 arcStart, float arcDegrees, int projectileCount,
        float delayBetweenProjectiles, int arcCount = 1, float delayBetweenArcs = 0f)
    {
        Vector2 arcCenter = ArcCenterFor(targetPlayer);
        float radius = Vector2.Distance(arcStart, arcCenter);
        StartCoroutine(SpawnProjectileArcCoroutine(targetPlayer, direction, arcCenter, radius, arcStart, arcDegrees, projectileCount, 
            delayBetweenProjectiles, arcCount, delayBetweenArcs));
    }

    private IEnumerator SpawnProjectileArcCoroutine(Transform targetPlayer, ArcDirection direction, Vector2 arcCenter, float radius, Vector2 arcStart, float arcDegrees, 
        int projectileCount, float delayBetweenProjectiles, int arcCount, float delayBetweenArcs)
    {
        float startAngle = Mathf.Atan2(arcStart.y - arcCenter.y, arcStart.x - arcCenter.x) * Mathf.Rad2Deg;
        float angleStep = arcDegrees / (projectileCount - 1);
        if (direction == ArcDirection.Clockwise) angleStep = -angleStep;

        for (int arc = 0; arc < arcCount; arc++)
        {
            if (arc > 0 && delayBetweenArcs > 0)
                yield return new WaitForSeconds(delayBetweenArcs);

            for (int i = 0; i < projectileCount; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector2 spawnPos = arcCenter + new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad)) * radius;
                SpawnProjectile(targetPlayer, spawnPos, delayBetweenProjectiles * i);
            }
        }
    }

    public void SpawnProjectileStraight(Vector2 spawnPosition, Transform targetPlayer, float projectileAmount, float projectileDelay, float initialDelay = 0f)
    {
        StartCoroutine(SpawnProjectileStraightCoroutine(spawnPosition, targetPlayer, projectileAmount, projectileDelay, initialDelay));
    }

    private IEnumerator SpawnProjectileStraightCoroutine(Vector2 spawnPosition, Transform targetPlayer, float projectileAmount, float projectileDelay, float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
        for (int i = 0; i < projectileAmount; i++)
        {
            SpawnProjectile(targetPlayer, spawnPosition, projectileDelay * i);
        }
        yield return null;
    }

    public void SpawnOrb(Vector2 startPos, Vector2 endPos, bool isHealthOrb, float delay = 0f)
    {
        StartCoroutine(SpawnOrbAfterDelay(startPos, endPos, isHealthOrb, delay));
    }

    private IEnumerator SpawnOrbAfterDelay(Vector2 startPos, Vector2 endPos, bool isHealthOrb, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        GameObject orbPrefab = isHealthOrb ? healthOrbPrefab : manaOrbPrefab;
        GameObject orb = Instantiate(orbPrefab);
        CollectibleOrb collectibleOrb = orb.GetComponent<CollectibleOrb>();
        if (collectibleOrb != null)
        {
            collectibleOrb.Initialize(startPos, endPos);
        }
    }
}

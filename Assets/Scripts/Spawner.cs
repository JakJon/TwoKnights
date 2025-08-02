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
    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] public GameObject brownRat;
    [SerializeField] public GameObject greyRat;
    [SerializeField] public GameObject blackRat;
    [SerializeField] public GameObject slimePrefab;
    [SerializeField] public GameObject bat;
    [SerializeField] public GameObject healthOrbPrefab;
    [SerializeField] public GameObject manaOrbPrefab;

    private Transform _leftPlayer;
    private Transform _rightPlayer;
    private bool _isWaveInProgress;
    private bool _isUpgradeMenuActive; // Track if upgrade menu is showing

    #region common references
    // Public methods for spawning that can be used by wave classes
    public Transform LeftPlayer => _leftPlayer;
    public Transform RightPlayer => _rightPlayer;
    public Vector2 aboveLeftPLayer => new Vector2(-2, 7);
    public Vector2 aboveRightPlayer => new Vector2(2, 7);
    public Vector2 belowLeftPlayer => new Vector2(-2, -7);
    public Vector2 belowRightPlayer => new Vector2(2, -7);
    public Vector2 leftOfLeftPlayer => new Vector2(-12, 0);
    public Vector2 rightOfRightPlayer => new Vector2(12, 0);
    public Vector2 topLeftCorner => new Vector2(-12, 6);
    public Vector2 topRightCorner => new Vector2(12, 6);
    public Vector2 bottomLeftCorner => new Vector2(-12, -6);
    public Vector2 bottomRightCorner => new Vector2(12, -6);
    #endregion

    void Awake()
    {
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
        
        StartNextWave();
    }

    public void StartNextWave()
    {
        if (_isWaveInProgress || _isUpgradeMenuActive)
            return;

        var nextWave = waveManager.SelectNextWave();
        if (nextWave != null)
        {
            if (waveNameDisplay != null)
                waveNameDisplay.DisplayWaveName(nextWave.GetFormattedWaveName(waveManager.CurrentWaveNumber));
            _isWaveInProgress = true;
            StartCoroutine(RunWave(nextWave));
        }
    }

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

        // Show upgrade menu and pause game instead of immediately starting next wave
        ShowUpgradeMenu();
    }

    private void ShowUpgradeMenu()
    {
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

    void OnDestroy()
    {
        // Clean up event subscription
        if (upgradeMenu != null)
        {
            upgradeMenu.OnUpgradeConfirmed -= OnUpgradeConfirmed;
        }
    }

    public void SpawnRat(Vector2 targetPosition, GameObject ratType, float delay, Transform playerTarget)
    {
        StartCoroutine(SpawnRatAfterDelay(targetPosition, ratType, delay, playerTarget));
    }

    private IEnumerator SpawnRatAfterDelay(Vector2 targetPosition, GameObject ratType, float delay, Transform playerTarget)
    {
        yield return new WaitForSeconds(delay);
        GameObject enemy = Instantiate(ratType);
        enemy.transform.position = targetPosition;

        EnemyRat enemyRat = enemy.GetComponent<EnemyRat>();
        if (enemyRat != null)
        {
            enemyRat.InitializeTarget(playerTarget);
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
        StartCoroutine(SpawnBatAfterDelay(spawnPosition, delay));
    }

    private IEnumerator SpawnBatAfterDelay(Vector2 spawnPosition, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject enemy = Instantiate(bat);
        enemy.transform.position = spawnPosition;
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

    public void SpawnProjectileArc(Transform targetPlayer, ArcDirection direction, Vector2 arcStart, float arcDegrees, int projectileCount, 
        float delayBetweenProjectiles, int arcCount = 1, float delayBetweenArcs = 0f)
    {
        Vector2 arcCenter = (targetPlayer == _leftPlayer) ? new Vector2(-2, 0) : new Vector2(2, 0);
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

    public void SpawnProjectileStraight(Vector2 spawnPosition, Transform targetPlayer, int projectileAmount, float projectileDelay, float initialDelay = 0f)
    {
        StartCoroutine(SpawnProjectileStraightCoroutine(spawnPosition, targetPlayer, projectileAmount, projectileDelay, initialDelay));
    }

    private IEnumerator SpawnProjectileStraightCoroutine(Vector2 spawnPosition, Transform targetPlayer, int projectileAmount, float projectileDelay, float initialDelay)
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

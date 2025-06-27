using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Spawner : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] public GameObject brownRat;
    [SerializeField] public GameObject greyRat;
    [SerializeField] public GameObject blackRat;
    [SerializeField] public GameObject slimePrefab;
    [SerializeField] public GameObject bat;

    private Transform _leftPlayer;
    private Transform _rightPlayer;
    private bool _isWaveInProgress;

    void Start()
    {
        _leftPlayer = GameObject.FindWithTag("PlayerLeft").transform;
        _rightPlayer = GameObject.FindWithTag("PlayerRight").transform;
        StartNextWave();
    }

    public void StartNextWave()
    {
        if (_isWaveInProgress)
            return;

        var nextWave = waveManager.SelectNextWave();
        if (nextWave != null)
        {
            _isWaveInProgress = true;
            StartCoroutine(RunWave(nextWave));
        }
    }

    private IEnumerator RunWave(BaseWave wave)
    {
        yield return StartCoroutine(wave.SpawnWave(this));
        
        _isWaveInProgress = false;
        waveManager.WaveCompleted();
        StartNextWave(); // Automatically start next wave
    }

    // Public methods for spawning that can be used by wave classes
    public Transform LeftPlayer => _leftPlayer;
    public Transform RightPlayer => _rightPlayer;

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

    public void SpawnProjectile(Transform targetPlayer, Vector2 spawnPosition)
    {
        GameObject projectile = Instantiate(projectilePrefab);
        ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();
        pm.Initialize(targetPlayer, spawnPosition);
    }
}

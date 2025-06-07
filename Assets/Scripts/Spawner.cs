using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class Spawner : MonoBehaviour
{
    [System.Serializable]
    private struct ProjectileSpawnData
    {
        public Transform targetPlayer;
        public float angle; // In degrees
        public float radius;
        public float delay;
    }

    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject brownRat;
    [SerializeField] private GameObject greyRat;
    [SerializeField] private GameObject blackRat;
    [SerializeField] private GameObject slime;

    private Transform _leftPlayer;
    private Transform _rightPlayer;
    private List<ProjectileSpawnData> _spawns = new List<ProjectileSpawnData>();

    void Start()
    {
        _leftPlayer = GameObject.FindWithTag("PlayerLeft").transform;
        _rightPlayer = GameObject.FindWithTag("PlayerRight").transform;

        SpawnSlime(3, new Vector2(2, 2), slime, 0, _leftPlayer);

        // Generate spiral data for both players
        #region spiral data
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.2f, false, 2f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.2f, true, 4f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.1f, false, 5f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.1f, true, 6f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.1f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.1f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.01f, false, 5.85f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.01f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 4f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.005f, false, 5f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.005f, true, 6f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.001f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.00000000001f, false, 5.85f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.00000000001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.01f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 4f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.005f, false, 5f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.005f, true, 6f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.001f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.00000000001f, false, 5.85f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.00000000001f, true, 2f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.01f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 4f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.005f, false, 5f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.005f, true, 6f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.001f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.00000000001f, false, 5.85f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.00000000001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.01f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.01f, true, 4f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.005f, false, 5f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.005f, true, 6f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.001f, false, 7f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.001f, true, 2f);
        //GenerateSpiralData(_leftPlayer, 8, 30f, 0.00000000001f, false, 5.85f);
        //GenerateSpiralData(_rightPlayer, 8, 30f, 0.00000000001f, true, 2f);

        SpawnRat(new Vector2(-6, 3), blackRat, 0f, _rightPlayer);
        SpawnRat(new Vector2(2, 1), brownRat, 3f, _leftPlayer);
        SpawnRat(new Vector2(-2, -2), greyRat, 20f, _leftPlayer);
        SpawnRat(new Vector2(-6, 3), blackRat, 30f, _rightPlayer);
        SpawnRat(new Vector2(1, 2), brownRat, 33, _rightPlayer);
        SpawnRat(new Vector2(-2, -2), greyRat, 37f, _leftPlayer);
        SpawnRat(new Vector2(-6, 3), blackRat, 50f, _rightPlayer);
        SpawnRat(new Vector2(2, 1), brownRat, 50f, _leftPlayer);
        SpawnRat(new Vector2(-2, -2), greyRat, 50f, _leftPlayer);
        SpawnRat(new Vector2(-6, 3), blackRat, 65f, _rightPlayer);
        SpawnRat(new Vector2(2, 1), brownRat, 65f, _leftPlayer);
        SpawnRat(new Vector2(-2, -2), greyRat, 65f, _leftPlayer);
        SpawnRat(new Vector2(-6, 3), blackRat, 85f, _rightPlayer);
        SpawnRat(new Vector2(2, 1), brownRat, 85f, _leftPlayer);
        SpawnRat(new Vector2(-2, -2), greyRat, 85f, _leftPlayer);


        //SpawnDevlogIntroSwarm();
        //StartCoroutine(SpawnAll());
        #endregion
    }

    private void GenerateSpiralData(Transform player, int count, float angleStep, float delayStep, bool clockwise, float startupDelay)
    {
        float currentAngle = 90f; // Start at top (90 degrees)
        float currentRadius = 10f;
        float currentDelay = 0f;

        for (int i = 0; i < count; i++)
        {
            if (i == 0) // use startup delay for the first projectile
            {
                currentDelay = startupDelay;
            }
            else if (i == 1) // Add reset the delay for the second projectile
            {
                currentDelay = delayStep;
            }
            else // Increment delay for subsequent projectiles
            {
                currentDelay += delayStep;
            }

            _spawns.Add(new ProjectileSpawnData
            {
                targetPlayer = player,
                angle = currentAngle,
                radius = currentRadius,
                delay = currentDelay
            });

            if (clockwise)
            {
                currentAngle -= angleStep; // Rotate clockwise
            }
            else
            {
                currentAngle += angleStep; // Rotate counter-clockwise
            }

            currentRadius += 0.5f; // Expand spiral

        }
    }

    // Add this to your spawning code
    void SpawnDevlogIntroSwarm()
    {
        // Positions around screen edges (adjust values to match your camera bounds)
        Vector2[] swarmPositions = {
        // Brown/Grey Positions (X range -6 to 6, Y range -5 to 5)
        new(-6, 4),     // Top-left start
        new(6, -4),     // Bottom-right start
        new(-3, 3),     // Mid-left
        new(3, -3),     // Mid-right
        new(0, 4),      // Top-center
        
        // Black Rat Positions (X range -10 to 10, Y range -5 to 5)
        new(-10, 4),    // Far left
        new(10, -4),    // Far right
        new(-8, 2),     // Left-middle
        new(8, -2),     // Right-middle
        new(0, -4),     // Bottom-center
        
        // Edge Cases
        new(-6, -4),    // Bottom-left
        new(6, 4),      // Top-right
        new(-10, -4),   // Far bottom-left
        new(10, 4),     // Far top-right
        new(0, 0)       // Center
    };

        GameObject[] ratWave = {
        brownRat, greyRat, brownRat, greyRat, brownRat,
        blackRat, blackRat, blackRat, blackRat, blackRat,
        greyRat, brownRat, greyRat, brownRat, greyRat
    };

        Transform[] playerTargets =
        {
            _leftPlayer, _rightPlayer, _leftPlayer, _rightPlayer, _leftPlayer,
            _leftPlayer, _rightPlayer, _leftPlayer, _rightPlayer, _leftPlayer,
            _rightPlayer, _leftPlayer, _rightPlayer, _leftPlayer, _rightPlayer
     };

        // Spawn all rats simultaneously with 0 delay
        for (int i = 0; i < swarmPositions.Length; i++)
        {
            SpawnRat(swarmPositions[i], ratWave[i], 0f, playerTargets[i]);
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

        // Set chase target
        EnemyRat enemyRat = enemy.GetComponent<EnemyRat>();
        if (enemyRat != null)
        {
            enemyRat.InitializeTarget(playerTarget);
        }
    }

    public void SpawnSlime(int size, Vector2 spawnPosition, GameObject slimePrefab, float delay, Transform targetPlayer)
    {
        StartCoroutine(SpawnSlimeAfterDelay(size, spawnPosition, slimePrefab, delay, targetPlayer));
    }

    private IEnumerator SpawnSlimeAfterDelay(int size, Vector2 spawnPosition, GameObject slimePrefab, float delay, Transform targetPlayer)
    {
        yield return new WaitForSeconds(delay);
        GameObject slime = Instantiate(slimePrefab);
        slime.transform.position = spawnPosition;

        // Set slime size, target, and initialize
        EnemySlime slimeScript = slime.GetComponent<EnemySlime>();
        if (slimeScript != null)
        {
            slimeScript.size = size;
            slimeScript.targetPlayer = targetPlayer;
            slimeScript.InitializeSlime();
        }
    }

    private IEnumerator SpawnAll()
    {
        foreach (ProjectileSpawnData data in _spawns)
        {
            yield return new WaitForSeconds(data.delay);

            // Convert polar coordinates (angle/radius) to Cartesian
            Vector2 spawnOffset = new Vector2(
                Mathf.Cos(data.angle * Mathf.Deg2Rad),
                Mathf.Sin(data.angle * Mathf.Deg2Rad)
            ) * data.radius;

            Vector2 spawnPosition = (Vector2)data.targetPlayer.position + spawnOffset;

            GameObject projectile = Instantiate(projectilePrefab);
            ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();
            pm.Initialize(data.targetPlayer, spawnPosition);
        }
    }
}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseWave : ScriptableObject
{
    [SerializeField] private string waveName;
    [SerializeField] private float weight = 1f; // Higher weight = more likely to be selected
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private string waveDescription;
    
    [Header("Optional Wave Unlock Conditions")]
    [SerializeField] private int unlockedAfterXWaves = -1; // -1 means not set
    [SerializeField] private int lockedAfterXWaves = -1; // -1 means not set

    [Header("Wave Configuration")]
    [SerializeField] private bool useEnemyTracking = true; // Whether to wait for all enemies to be killed
    
    // Static reference to the currently active wave for enemy tracking
    private static BaseWave _currentWave;
    private HashSet<GameObject> _trackedEnemies = new HashSet<GameObject>();
    private bool _waveSpawningComplete = false;

    public string WaveName => waveName;
    public float Weight => weight;
    public bool IsUnlocked => isUnlocked;
    public string Description => waveDescription;
    public bool UseEnemyTracking => useEnemyTracking;
    
    // Get formatted wave name with wave number
    public string GetFormattedWaveName(int waveNumber)
    {
        string romanNumeral = NumberConverter.ToRoman(waveNumber);
        return $"{romanNumeral}: {waveName}";
    }

    // Called to check if wave can be played (beyond just being unlocked)
    public virtual bool CanPlay()
    {
        return CanPlay(0); // Default to 0 completed waves for backward compatibility
    }
    
    // Overloaded version that takes completed waves count
    public virtual bool CanPlay(int completedWavesCount)
    {
        // Check lock condition first (can override everything)
        if (lockedAfterXWaves >= 0 && completedWavesCount >= lockedAfterXWaves)
        {
            return false;
        }
        
        // Check unlock condition
        if (unlockedAfterXWaves >= 0)
        {
            return completedWavesCount >= unlockedAfterXWaves;
        }
        
        // Fall back to base unlock state if no conditional fields are set
        return isUnlocked;
    }

    // The main wave spawn logic - implemented by each wave
    public abstract IEnumerator SpawnWave(Spawner spawner);

    // Called when wave is complete
    public virtual void OnWaveComplete() { }
    
    // Enemy tracking methods
    public static void RegisterEnemy(GameObject enemy)
    {
        if (_currentWave != null && _currentWave.useEnemyTracking)
        {
            _currentWave._trackedEnemies.Add(enemy);
            // Debug.Log($"Enemy registered with wave. Total enemies: {_currentWave._trackedEnemies.Count}");
        }
    }
    
    public static void UnregisterEnemy(GameObject enemy)
    {
        if (_currentWave != null && _currentWave.useEnemyTracking)
        {
            _currentWave._trackedEnemies.Remove(enemy);
            // Debug.Log($"Enemy unregistered from wave. Remaining enemies: {_currentWave._trackedEnemies.Count}");
        }
    }
    
    // Call this when spawning is complete
    protected void MarkSpawningComplete()
    {
        _waveSpawningComplete = true;
        // Debug.Log("Wave spawning marked as complete");
    }
    
    // Check if all enemies are dead
    public bool AreAllEnemiesDead()
    {
        if (!useEnemyTracking) return true;
        
        // Clean up any null references (destroyed objects)
        _trackedEnemies.RemoveWhere(enemy => enemy == null);
        
        return _waveSpawningComplete && _trackedEnemies.Count == 0;
    }
    
    // Call this at the start of wave execution
    public void StartWaveTracking()
    {
        _currentWave = this;
        _trackedEnemies.Clear();
        _waveSpawningComplete = false;
        // Debug.Log($"Started tracking for wave: {waveName}");
    }
    
    // Call this at the end of wave execution
    public void EndWaveTracking()
    {
        if (_currentWave == this)
        {
            _currentWave = null;
        }
        _trackedEnemies.Clear();
        _waveSpawningComplete = false;
        // Debug.Log($"Ended tracking for wave: {waveName}");
    }
    
    // Coroutine that waits for all enemies to be killed
    public IEnumerator WaitForAllEnemiesDead()
    {
        if (!useEnemyTracking)
        {
            yield break; // Don't wait if tracking is disabled
        }
        
        // Debug.Log("Waiting for all enemies to be killed...");
        
        while (!AreAllEnemiesDead())
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
        }
        
        // Debug.Log("All enemies have been killed!");
    }
}
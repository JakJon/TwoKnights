using UnityEngine;
using System.Collections;

public abstract class BaseWave : ScriptableObject
{
    [SerializeField] private string waveName;
    [SerializeField] private float weight = 1f; // Higher weight = more likely to be selected
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private string waveDescription;
    
    [Header("Optional Wave Unlock Conditions")]
    [SerializeField] private int unlockedAfterXWaves = -1; // -1 means not set
    [SerializeField] private int lockedAfterXWaves = -1; // -1 means not set

    public string WaveName => waveName;
    public float Weight => weight;
    public bool IsUnlocked => isUnlocked;
    public string Description => waveDescription;
    
    // Get formatted wave name with wave number
    public string GetFormattedWaveName(int waveNumber)
    {
        return $"{waveNumber}: {waveName}";
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
}
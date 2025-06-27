using UnityEngine;
using System.Collections;

public abstract class BaseWave : ScriptableObject
{
    [SerializeField] private string waveName;
    [SerializeField] private float weight = 1f; // Higher weight = more likely to be selected
    [SerializeField] private bool isUnlocked = true;
    [SerializeField] private string waveDescription;

    public string WaveName => waveName;
    public float Weight => weight;
    public bool IsUnlocked => isUnlocked;
    public string Description => waveDescription;

    // Called to check if wave can be played (beyond just being unlocked)
    public virtual bool CanPlay()
    {
        return isUnlocked;
    }

    // The main wave spawn logic - implemented by each wave
    public abstract IEnumerator SpawnWave(Spawner spawner);

    // Called when wave is complete
    public virtual void OnWaveComplete() { }
}
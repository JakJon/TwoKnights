using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "WaveManager", menuName = "Waves/Wave Manager")]
public class WaveManager : ScriptableObject
{
    [SerializeField] private List<BaseWave> availableWaves;
    
    private BaseWave currentWave;
    private float totalWeight;

    private void OnEnable()
    {
        RecalculateWeights();
    }

    private void RecalculateWeights()
    {
        totalWeight = availableWaves.Where(w => w.CanPlay()).Sum(w => w.Weight);
    }

    public BaseWave SelectNextWave()
    {
        if (availableWaves == null || availableWaves.Count == 0)
            return null;

        RecalculateWeights();
        
        // Get playable waves
        var playableWaves = availableWaves.Where(w => w.CanPlay()).ToList();
        if (playableWaves.Count == 0)
            return null;

        // Random selection based on weights
        float random = Random.Range(0, totalWeight);
        float current = 0;

        foreach (var wave in playableWaves)
        {
            current += wave.Weight;
            if (random <= current)
            {
                currentWave = wave;
                return wave;
            }
        }

        // Fallback
        currentWave = playableWaves[0];
        return currentWave;
    }

    public void WaveCompleted()
    {
        if (currentWave != null)
        {
            currentWave.OnWaveComplete();
            currentWave = null;
        }
    }
}
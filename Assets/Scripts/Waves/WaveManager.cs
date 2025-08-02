using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "WaveManager", menuName = "Waves/Wave Manager")]
public class WaveManager : ScriptableObject
{
    [SerializeField] private List<BaseWave> availableWaves;
    private List<BaseWave> _remainingWaves;
    private BaseWave currentWave;
    private int _completedWavesCount = 0;
    
    public int CompletedWavesCount => _completedWavesCount;
    public int CurrentWaveNumber => _completedWavesCount + 1;

    private void OnEnable()
    {
        RecalculateWeights();
        _remainingWaves = new List<BaseWave>(availableWaves);
    }

    private void RecalculateWeights()
    {
        // Each wave asset is considered independently
        if (availableWaves == null || availableWaves.Count == 0)
            return;

        // Remove any null entries that might have been created
        availableWaves.RemoveAll(w => w == null);
    }

    public BaseWave SelectNextWave()
    {
        if (_remainingWaves == null || _remainingWaves.Count == 0)
            return null;

        // Get playable waves (now passing completed waves count)
        var playableWaves = _remainingWaves.Where(w => w.CanPlay(_completedWavesCount)).ToList();
        if (playableWaves.Count == 0)
            return null;

        // Calculate total weight for all playable waves
        float totalWeight = playableWaves.Sum(w => w.Weight);
        
        // Random selection based on weights
        float random = Random.Range(0f, totalWeight);
        float current = 0f;

        foreach (var wave in playableWaves)
        {
            current += wave.Weight;
            if (random <= current)
            {
                currentWave = wave;
                _remainingWaves.Remove(wave); // Remove so it can't be selected again
                return wave;
            }
        }

        // Fallback to a random wave if something went wrong with the weight calculation
        currentWave = playableWaves[Random.Range(0, playableWaves.Count)];
        _remainingWaves.Remove(currentWave);
        return currentWave;
    }

    public void WaveCompleted()
    {
        if (currentWave != null)
        {
            currentWave.OnWaveComplete();
            currentWave = null;
            
            // Increment completed waves counter
            _completedWavesCount++;
            Debug.Log($"[WaveManager] Wave completed. Total completed waves: {_completedWavesCount}");
        }
    }
    
    public void ResetProgress()
    {
        _completedWavesCount = 0;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Find All Waves")]
    public void AutoFindAllWaves()
    {
        availableWaves = new List<BaseWave>();
        string[] guids = AssetDatabase.FindAssets("t:BaseWave");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaseWave wave = AssetDatabase.LoadAssetAtPath<BaseWave>(path);
            if (wave != null)
                availableWaves.Add(wave);
        }
        EditorUtility.SetDirty(this);
        Debug.Log($"[WaveManager] Found {availableWaves.Count} BaseWave assets.");
    }
#endif
}
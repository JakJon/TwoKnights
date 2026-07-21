using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

// How the run ended when a boss wave completes (None = keep playing)
public enum RunOutcome
{
    None,
    GateVictory, // first-ever gate boss kill on this map: run ends in victory
    TrueVictory  // the map's true final boss has fallen
}

[CreateAssetMenu(fileName = "WaveManager", menuName = "Waves/Wave Manager")]
public class WaveManager : ScriptableObject
{
    [SerializeField] private List<BaseWave> availableWaves;
    [SerializeField] private MapDefinition currentMap; // null = legacy flat pool
    private List<BaseWave> _remainingWaves;
    private BaseWave currentWave;
    private int _completedWavesCount = 0;
    private bool _gateBossDefeatedThisRun = false;

    public int CompletedWavesCount => _completedWavesCount;
    public int CurrentWaveNumber => _completedWavesCount + 1;
    public BaseWave CurrentWave => currentWave;
    public MapDefinition CurrentMap => currentMap;
    public RunOutcome PendingOutcome { get; private set; } = RunOutcome.None;

    public static WaveManager ActiveInstance { get; private set; }

    private void OnEnable()
    {
        ActiveInstance = this;
        RecalculateWeights();
        BeginRun();
    }

    // Reset all per-run state. Called from OnEnable AND by the Spawner on scene
    // start, so runs restart cleanly even without a domain reload (builds).
    public void BeginRun()
    {
        _completedWavesCount = 0;
        _gateBossDefeatedThisRun = false;
        PendingOutcome = RunOutcome.None;
        var pool = (currentMap != null && currentMap.Waves.Count > 0)
            ? currentMap.Waves
            : (IReadOnlyList<BaseWave>)availableWaves;
        _remainingWaves = pool != null ? new List<BaseWave>(pool) : new List<BaseWave>();
        _remainingWaves.RemoveAll(w => w == null);
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
        // Boss scheduling: the gate boss owns its wave number until beaten this
        // run; the true boss ends every run that reaches it
        BaseWave scheduledBoss = GetScheduledBoss();
        if (scheduledBoss != null)
        {
            currentWave = scheduledBoss;
            return currentWave;
        }

        BaseWave picked = PickFromPool();
        if (picked == null && currentMap != null)
        {
            // Pool ran dry mid-map: refill with the map's setlist (a repeat of a
            // handcrafted wave beats a silent stall) and try once more
            BeginRunPoolOnly();
            picked = PickFromPool();

            if (picked == null)
            {
                // Even the refilled pool has nothing playable at this wave count
                // (unlock windows) — bring the next boss forward instead
                picked = _gateBossDefeatedThisRun ? currentMap.TrueBoss : currentMap.GateBoss;
                currentWave = picked;
                return currentWave;
            }
        }

        return picked;
    }

    private BaseWave GetScheduledBoss()
    {
        if (currentMap == null) return null;

        if (!_gateBossDefeatedThisRun && currentMap.GateBoss != null
            && CurrentWaveNumber >= currentMap.GateBossWaveNumber)
        {
            return currentMap.GateBoss;
        }

        if (_gateBossDefeatedThisRun && currentMap.TrueBoss != null
            && CurrentWaveNumber >= currentMap.TrueBossWaveNumber)
        {
            return currentMap.TrueBoss;
        }

        return null;
    }

    // Refill only the wave pool, keeping wave count and boss state
    private void BeginRunPoolOnly()
    {
        var pool = (currentMap != null && currentMap.Waves.Count > 0)
            ? currentMap.Waves
            : (IReadOnlyList<BaseWave>)availableWaves;
        _remainingWaves = pool != null ? new List<BaseWave>(pool) : new List<BaseWave>();
        _remainingWaves.RemoveAll(w => w == null);
    }

    private BaseWave PickFromPool()
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
        if (currentWave == null) return;

        BaseWave finished = currentWave;
        currentWave.OnWaveComplete();
        currentWave = null;

        // Increment completed waves counter
        _completedWavesCount++;
        Debug.Log($"[WaveManager] Wave completed. Total completed waves: {_completedWavesCount}");

        if (currentMap == null) return;

        if (finished == currentMap.GateBoss && currentMap.GateBoss != null)
        {
            _gateBossDefeatedThisRun = true;
            bool firstClear = !MapProgressStore.IsGateCleared(currentMap.MapId);
            MapProgressStore.MarkGateCleared(currentMap);
            if (firstClear)
            {
                PendingOutcome = RunOutcome.GateVictory;
            }
            // Repeat kills: no outcome — the run continues into the deep waves
        }
        else if (finished == currentMap.TrueBoss && currentMap.TrueBoss != null)
        {
            MapProgressStore.MarkTrueCleared(currentMap);
            PendingOutcome = RunOutcome.TrueVictory;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Dev-only Test Mode: jump the run to the given wave number. Starting past
    // the gate boss wave treats the gate as already beaten this run (like a real
    // run that got there); starting ON the boss wave still spawns the boss.
    public void ApplyTestStart(int startWave)
    {
        _completedWavesCount = Mathf.Max(0, startWave - 1);
        if (currentMap != null && currentMap.GateBoss != null && startWave > currentMap.GateBossWaveNumber)
        {
            _gateBossDefeatedThisRun = true;
        }
    }

    // What SelectNextWave would be forced to pick right now (null = pool draw)
    public BaseWave PeekScheduledBoss()
    {
        return GetScheduledBoss();
    }

    // Every pool wave the weighted draw could currently land on. Mirrors
    // SelectNextWave's dry-pool refill so the picker sees the same candidates
    // the real selector would.
    public List<BaseWave> GetPlayableCandidates()
    {
        var playable = _remainingWaves != null
            ? _remainingWaves.Where(w => w != null && w.CanPlay(_completedWavesCount)).ToList()
            : new List<BaseWave>();
        if (playable.Count == 0 && currentMap != null)
        {
            BeginRunPoolOnly();
            playable = _remainingWaves.Where(w => w != null && w.CanPlay(_completedWavesCount)).ToList();
        }
        return playable;
    }

    // Pool waves whose unlock windows currently exclude them. The picker shows
    // these under a collapsed "locked" section so a dev can still force one.
    // Call after GetPlayableCandidates so a dry-pool refill has already run.
    public List<BaseWave> GetLockedCandidates()
    {
        return _remainingWaves != null
            ? _remainingWaves.Where(w => w != null && !w.CanPlay(_completedWavesCount)).ToList()
            : new List<BaseWave>();
    }

    // Test Mode picker chose a wave: install it exactly as PickFromPool would
    // (bosses aren't pool entries, so removing is a no-op for them)
    public void ForceSelectWave(BaseWave wave)
    {
        if (wave == null) return;
        currentWave = wave;
        _remainingWaves?.Remove(wave);
    }
#endif

    // Read-and-clear so a victory can't fire twice
    public RunOutcome ConsumePendingOutcome()
    {
        var outcome = PendingOutcome;
        PendingOutcome = RunOutcome.None;
        return outcome;
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
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
    [Tooltip("Fallback map when the run started without a level-select pick (direct editor play)")]
    [SerializeField] private MapDefinition currentMap; // null = legacy flat pool
    // The level-select pick for THIS run. Kept off the serialized field so
    // choosing a map in camp never dirties the shared WaveManager asset.
    private MapDefinition _activeMap;
    private List<BaseWave> _remainingWaves;
    private BaseWave currentWave;
    private int _completedWavesCount = 0;
    private bool _gateBossDefeatedThisRun = false;

    public int CompletedWavesCount => _completedWavesCount;
    public int CurrentWaveNumber => _completedWavesCount + 1;
    public BaseWave CurrentWave => currentWave;
    public MapDefinition CurrentMap => _activeMap != null ? _activeMap : currentMap;
    public RunOutcome PendingOutcome { get; private set; } = RunOutcome.None;

    public static WaveManager ActiveInstance { get; private set; }

    private void OnEnable()
    {
        ActiveInstance = this;
        RecalculateWeights();
        // Pool only — resolving the level-select pick touches Resources and the
        // save file, neither of which is safe during an editor domain reload.
        // The real run start (Spawner.Start -> BeginRun) does that.
        ResetRunState();
    }

    // Reset all per-run state. Called from OnEnable AND by the Spawner on scene
    // start, so runs restart cleanly even without a domain reload (builds).
    public void BeginRun()
    {
        // Honour the level select; falls back to the serialized map (and then
        // the flat pool) when the run didn't come through the camp menu
        _activeMap = MapSelection.Resolve();
        ResetRunState();
    }

    private void ResetRunState()
    {
        _completedWavesCount = 0;
        _gateBossDefeatedThisRun = false;
        PendingOutcome = RunOutcome.None;
        BeginRunPoolOnly();
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

        var map = CurrentMap;
        BaseWave picked = PickFromPool();
        if (picked == null && map != null)
        {
            // Pool ran dry mid-map: refill with the map's setlist (a repeat of a
            // handcrafted wave beats a silent stall) and try once more
            BeginRunPoolOnly();
            picked = PickFromPool();

            if (picked == null)
            {
                // Even the refilled pool has nothing playable at this wave count
                // (unlock windows) — bring the next boss forward instead
                picked = _gateBossDefeatedThisRun ? map.TrueBoss : map.GateBoss;
                currentWave = picked;
                return currentWave;
            }
        }

        return picked;
    }

    private BaseWave GetScheduledBoss()
    {
        var map = CurrentMap;
        if (map == null) return null;

        if (!_gateBossDefeatedThisRun && map.GateBoss != null
            && CurrentWaveNumber >= map.GateBossWaveNumber)
        {
            return map.GateBoss;
        }

        if (_gateBossDefeatedThisRun && map.TrueBoss != null
            && CurrentWaveNumber >= map.TrueBossWaveNumber)
        {
            return map.TrueBoss;
        }

        return null;
    }

    // Refill only the wave pool, keeping wave count and boss state
    private void BeginRunPoolOnly()
    {
        var map = CurrentMap;
        var pool = (map != null && map.Waves.Count > 0)
            ? map.Waves
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

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.waveComplete);

        BaseWave finished = currentWave;
        currentWave.OnWaveComplete();
        currentWave = null;

        // Increment completed waves counter
        _completedWavesCount++;
        Debug.Log($"[WaveManager] Wave completed. Total completed waves: {_completedWavesCount}");

        var map = CurrentMap;
        if (map == null) return;

        if (finished == map.GateBoss && map.GateBoss != null)
        {
            _gateBossDefeatedThisRun = true;
            bool firstClear = !MapProgressStore.IsGateCleared(map.MapId);
            MapProgressStore.MarkGateCleared(map);
            if (firstClear)
            {
                PendingOutcome = RunOutcome.GateVictory;
            }
            // Repeat kills: no outcome — the run continues into the deep waves
        }
        else if (finished == map.TrueBoss && map.TrueBoss != null)
        {
            MapProgressStore.MarkTrueCleared(map);
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
        var map = CurrentMap;
        if (map != null && map.GateBoss != null && startWave > map.GateBossWaveNumber)
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
        if (playable.Count == 0 && CurrentMap != null)
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
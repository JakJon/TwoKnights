using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    // LEGACY, NO LONGER READ. Every wave in here is also on Camp Fields, which is
    // where the forest setlist actually lives — this list predates maps and was a
    // flat pool of every wave in the game, forest and mine mixed together.
    //
    // It is kept, unread, as a backup copy of that old setlist and NOTHING else.
    // Do not wire it back into selection: a pool that spans every map is exactly
    // how a mine wave ends up eligible in the forest. The pool comes from the
    // active map's own list, and only from there — see BeginRunPoolOnly.
    [Tooltip("LEGACY BACKUP — not used. The pool comes from the active map's own wave list. Kept only as a copy of the pre-map flat setlist.")]
    [SerializeField] private List<BaseWave> availableWaves;

    [Tooltip("Fallback map when the run started without a level-select pick (direct editor play)")]
    [SerializeField] private MapDefinition currentMap;
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
        // Pool only — resolving the level-select pick touches Resources and the
        // save file, neither of which is safe during an editor domain reload.
        // The real run start (Spawner.Start -> BeginRun) does that.
        //
        // Silent on purpose: a domain reload can run this before the map's own
        // references have been resolved, so a pool that looks broken here is
        // usually just early. Only a real run start is worth shouting about.
        ResetRunState(report: false);
    }

    // Reset all per-run state. Called from OnEnable AND by the Spawner on scene
    // start, so runs restart cleanly even without a domain reload (builds).
    public void BeginRun()
    {
        // Honour the level select; falls back to the serialized map when the run
        // didn't come through the camp menu
        _activeMap = MapSelection.Resolve();
        ResetRunState(report: true);
    }

    private void ResetRunState(bool report)
    {
        _completedWavesCount = 0;
        _gateBossDefeatedThisRun = false;
        PendingOutcome = RunOutcome.None;
        BeginRunPoolOnly(report);
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

    // Refill only the wave pool, keeping wave count and boss state.
    //
    // The active map's own setlist is the ONLY source. There used to be a fallback
    // to the flat availableWaves list here, and it was worse than having no pool:
    // a map whose list failed to load did not fail loudly, it quietly played the
    // whole game's waves instead — forest waves in the mine included.
    //
    // A null entry is now REPORTED rather than silently dropped. A wave reference
    // reads as null when the map asset was imported before the wave asset it
    // points at existed: the .asset on disk is correct, the wave compiles, and it
    // simply never turns up in the pool with nothing said. That is exactly how
    // Near and Far went missing (2026-08-04), and a stripped entry should never
    // be invisible again.
    private void BeginRunPoolOnly(bool report = true)
    {
        _remainingWaves = new List<BaseWave>();

        var map = CurrentMap;
        if (map == null)
        {
            if (report)
            {
                Debug.LogError("[WaveManager] No map for this run, so there is no pool and nothing can be " +
                               "picked. Set Current Map on the WaveManager asset, or start through level select.");
            }
            return;
        }

        var setlist = map.Waves;
        int missing = 0;

        for (int i = 0; i < setlist.Count; i++)
        {
            if (setlist[i] == null)
            {
                missing++;
                continue;
            }
            _remainingWaves.Add(setlist[i]);
        }

        if (!report) return;

        if (missing > 0)
        {
            Debug.LogError($"[WaveManager] {map.name} lists {setlist.Count} waves but {missing} of them could " +
                           $"not be loaded, so they are absent from the pool. Right-click the map asset and " +
                           $"Reimport: a reference reads as null when the map was imported before the wave " +
                           $"asset it points at existed.");
        }
        else
        {
            Debug.Log($"[WaveManager] {map.name} pool: {_remainingWaves.Count} waves.");
        }

        if (_remainingWaves.Count == 0)
        {
            Debug.LogError($"[WaveManager] {map.name} has no usable waves — no wave can be selected.");
        }
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

        // High-water mark, not a count — a later worse run must not erase it.
        // Skipped for test runs because ApplyTestStart FABRICATES the wave
        // counter, so a run started at wave 20 would hand out depth quests
        // that were never actually played.
        if (!IsTestRun())
        {
            PlayerStats.Raise($"maps.{map.MapId}.furthest_wave", _completedWavesCount);
        }

        if (finished == map.GateBoss && map.GateBoss != null)
        {
            _gateBossDefeatedThisRun = true;
            // Taken down carrying nothing and never firing a special. Test runs
            // are excluded for the same reason furthest_wave is.
            if (map.MapId == "camp_fields" && RunPurity.Bare && !IsTestRun())
            {
                Feats.Record(Feats.RatKingBare);
            }
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
            if (map.MapId == "camp_fields" && RunPurity.Bare && !IsTestRun())
            {
                Feats.Record(Feats.TwinsBare);
            }
            MapProgressStore.MarkTrueCleared(map);
            PendingOutcome = RunOutcome.TrueVictory;
        }
    }

    private static bool IsTestRun()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return TestRunConfig.ActiveRun;
#else
        return false;
#endif
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

    // NOTE: the old "Auto-Find All Waves" context menu lived here. It filled the
    // flat list from a project-wide `t:BaseWave` search, which is precisely what
    // mixed every map's waves into one pool. Its replacement is per-map and lives
    // on MapDefinition ("Find Waves In Folders"), so a map can only ever pull in
    // waves from the folders it names.
}
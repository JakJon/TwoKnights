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
    private HashSet<GameObject> _trackedProjectiles = new HashSet<GameObject>();
    private bool _waveSpawningComplete = false;

    // --- Ambushes ---------------------------------------------------------
    // An ambush is a GROUP of enemies inside a wave that comes out together. It
    // is a timing tool and nothing else: a wave opens one, releases into it, and
    // waits on it to decide when the next group arrives.
    //
    // "Ambush clear" and "wave over" are DIFFERENT QUESTIONS and must not be
    // confused. A wave is over when it has finished spawning and everything it
    // ever put on the field — enemies and projectiles alike — is gone. An ambush
    // clears while the wave is very much still running, counts only the enemies
    // of that one group, and ignores projectiles entirely: rocks still falling
    // out of the shafts are the wave's business, not the group's.
    private readonly HashSet<GameObject> _ambushEnemies = new HashSet<GameObject>();
    private bool _ambushOpen;
    private bool _ambushReleased;
    private int _ambushSeen;
    private int _ambushExpected;
    private int _ambushIndex = -1;

    public string WaveName => waveName;
    public float Weight => weight;
    public bool IsUnlocked => isUnlocked;
    public string Description => waveDescription;
    public bool UseEnemyTracking => useEnemyTracking;
    
    // Get formatted wave name with wave number (white numeral via TMP rich text;
    // the rest inherits the banner's gold)
    public string GetFormattedWaveName(int waveNumber)
    {
        string romanNumeral = NumberConverter.ToRoman(waveNumber);
        return $"<color=#FFFFFF>{romanNumeral}</color> {waveName}";
    }

    // Called to check if wave can be played (beyond just being unlocked)
    public virtual bool CanPlay()
    {
        return CanPlay(0); // Default to 0 completed waves for backward compatibility
    }
    
    // Overloaded version that takes completed waves count
    public virtual bool CanPlay(int completedWavesCount)
    {
        // The lock window is a hard cutoff and beats everything, including
        // isUnlocked — an early wave must never resurface deep in a run just
        // because it was flagged always-available
        if (lockedAfterXWaves >= 0 && completedWavesCount >= lockedAfterXWaves)
        {
            return false;
        }

        // isUnlocked = available from the start (skips the unlock window only)
        if (isUnlocked) return true;

        // Check unlock condition
        if (unlockedAfterXWaves >= 0)
        {
            return completedWavesCount >= unlockedAfterXWaves;
        }

        return false;
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

            // Membership is decided at REGISTRATION time, which is the enemy's
            // Awake. Anything that arrives while a group is open belongs to it —
            // including a boss's summons, which is what makes an ambush an honest
            // "is this fight over yet" rather than a spawn count.
            if (_currentWave._ambushOpen && _currentWave._ambushEnemies.Add(enemy))
            {
                _currentWave._ambushSeen++;
            }
        }
    }

    public static void UnregisterEnemy(GameObject enemy)
    {
        if (_currentWave != null && _currentWave.useEnemyTracking)
        {
            _currentWave._trackedEnemies.Remove(enemy);
            _currentWave._ambushEnemies.Remove(enemy);
            // Debug.Log($"Enemy unregistered from wave. Remaining enemies: {_currentWave._trackedEnemies.Count}");
        }
    }
    
    // Projectile tracking methods
    public static void RegisterProjectile(GameObject projectile)
    {
        if (_currentWave != null && _currentWave.useEnemyTracking)
        {
            _currentWave._trackedProjectiles.Add(projectile);
            // Debug.Log($"Projectile registered with wave. Total projectiles: {_currentWave._trackedProjectiles.Count}");
        }
    }
    
    public static void UnregisterProjectile(GameObject projectile)
    {
        if (_currentWave != null && _currentWave.useEnemyTracking)
        {
            _currentWave._trackedProjectiles.Remove(projectile);
            // Debug.Log($"Projectile unregistered from wave. Remaining projectiles: {_currentWave._trackedProjectiles.Count}");
        }
    }
    
    // Call this when spawning is complete
    protected void MarkSpawningComplete()
    {
        _waveSpawningComplete = true;
        // Debug.Log("Wave spawning marked as complete");
    }
    
    /// <summary>
    /// Open a new ambush. Every enemy that registers from here until the next
    /// BeginAmbush belongs to this group.
    /// </summary>
    /// <param name="expectedMembers">
    /// How many enemies this group will put on the field, when — and only when —
    /// some of them are spawned with a Spawner DELAY. Delayed spawns instantiate
    /// after the wait, so they register late; without a declared count a group
    /// whose first member dies before its second exists reads as clear and the
    /// wave runs straight over the top of it. Immediate spawns need nothing here.
    /// </param>
    protected void BeginAmbush(int expectedMembers = 0)
    {
        _ambushEnemies.Clear();
        _ambushOpen = true;
        _ambushReleased = false;
        _ambushSeen = 0;
        _ambushExpected = Mathf.Max(0, expectedMembers);
        _ambushIndex++;
    }

    /// <summary>
    /// The group is all out — nothing more is coming. The ambush counterpart of
    /// MarkSpawningComplete, and just as load-bearing: without it the group can
    /// never read as clear, so a wave that forgets it waits forever.
    /// </summary>
    protected void MarkAmbushReleased()
    {
        _ambushReleased = true;
    }

    /// <summary>
    /// Is the current group down? True once it has finished being released and
    /// every enemy in it is dead — the cue to send the next one. Enemies that do
    /// not gate wave completion (an empty mine cart, a gnome's wreck) are never
    /// in a group either, so a ring still full of rolling iron reads as clear.
    ///
    /// A group that never registered anyone — all scenery, no kills — is clear
    /// the moment it is released. There is nothing to wait for, and hanging on it
    /// would be a stall with no way out of it.
    /// </summary>
    public bool IsAmbushClear()
    {
        if (!useEnemyTracking || !_ambushOpen) return true;

        _ambushEnemies.RemoveWhere(enemy => enemy == null);

        return _ambushReleased
            && _ambushSeen >= _ambushExpected
            && _ambushEnemies.Count == 0;
    }

    /// <summary>Live members of the current group.</summary>
    public int AmbushEnemiesRemaining
    {
        get
        {
            _ambushEnemies.RemoveWhere(enemy => enemy == null);
            return _ambushEnemies.Count;
        }
    }

    /// <summary>Which group the wave is on, counting from 0. -1 before the first.</summary>
    public int AmbushIndex => _ambushIndex;

    /// <summary>
    /// Hold until the current group is down. Deliberately has NO timeout: the
    /// next ambush is a reward for clearing this one, and a wave that sends it
    /// anyway after N seconds is just a schedule wearing an ambush's clothes.
    /// Waves that lean on this owe the player standing pressure while they work
    /// — otherwise a group left alive is a free rest instead of a fight.
    /// </summary>
    protected IEnumerator WaitForAmbushClear()
    {
        if (!useEnemyTracking || !_ambushOpen)
        {
            if (!_ambushOpen)
            {
                Debug.LogWarning($"[{name}] WaitForAmbushClear with no ambush open — " +
                                 "BeginAmbush is missing, and the wait does nothing.");
            }
            yield break;
        }

        // Per frame rather than on a poll: the handover between groups is the
        // beat the player feels, and half a second of dead air after the last
        // kill reads as the wave hesitating.
        while (!IsAmbushClear()) yield return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Ambush] {name} cleared ambush {_ambushIndex} at t={Time.time:F2}");
#endif

        // Closed, not merely clear: anything that turns up now (a delayed spawn
        // that outlived its group, a split) belongs to no ambush rather than
        // retroactively re-opening one that has already paid out.
        _ambushOpen = false;
    }

    // Check if all enemies and projectiles are cleared
    public bool AreAllEnemiesDead()
    {
        if (!useEnemyTracking) return true;
        
        // Clean up any null references (destroyed objects)
        _trackedEnemies.RemoveWhere(enemy => enemy == null);
        _trackedProjectiles.RemoveWhere(projectile => projectile == null);
        
        return _waveSpawningComplete && _trackedEnemies.Count == 0 && _trackedProjectiles.Count == 0;
    }
    
    // Call this at the start of wave execution
    public void StartWaveTracking()
    {
        _currentWave = this;
        _trackedEnemies.Clear();
        _trackedProjectiles.Clear();
        _waveSpawningComplete = false;
        ResetAmbush();
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
        _trackedProjectiles.Clear();
        _waveSpawningComplete = false;
        ResetAmbush();
        // Debug.Log($"Ended tracking for wave: {waveName}");
    }

    // Wave assets are ScriptableObjects, so their state outlives the run that
    // set it. A group left open by a wave that was cut short would otherwise be
    // inherited by the next play of the same asset.
    private void ResetAmbush()
    {
        _ambushEnemies.Clear();
        _ambushOpen = false;
        _ambushReleased = false;
        _ambushSeen = 0;
        _ambushExpected = 0;
        _ambushIndex = -1;
    }
    
    // Coroutine that waits for all enemies and projectiles to be cleared
    public IEnumerator WaitForAllEnemiesDead()
    {
        if (!useEnemyTracking)
        {
            yield break; // Don't wait if tracking is disabled
        }
        
        // Debug.Log("Waiting for all enemies to be killed and projectiles to be cleared...");
        
        while (!AreAllEnemiesDead())
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
        }
        
        // Debug.Log("All enemies have been killed and projectiles cleared!");
    }
}
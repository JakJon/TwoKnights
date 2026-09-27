using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Spawner : MonoBehaviour
{
    public enum ArcDirection { Clockwise, CounterClockwise }

    [SerializeField] private WaveManager waveManager;
    [SerializeField] private WaveName waveNameDisplay;
    [SerializeField] private UpgradeMenu upgradeMenu; // New reference to upgrade menu
    [SerializeField] private UpgradeManager upgradeManager; // Reference to upgrade manager
    // Shown between wave end and the upgrade menu. FormerlySerializedAs keeps the
    // scene's existing reference: Unity serializes by FIELD NAME, so renaming this
    // from questCompletePanel would otherwise silently null it out.
    [UnityEngine.Serialization.FormerlySerializedAs("questCompletePanel")]
    [SerializeField] private WaveSurvivedPanel waveSurvivedPanel;
    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] public GameObject brownRat;
    [SerializeField] public GameObject greyRat;
    [SerializeField] public GameObject blackRat;
    [SerializeField] public GameObject slimePrefab;
    [SerializeField] public GameObject bat;
    [SerializeField] public GameObject greyWolfPrefab;
    [SerializeField] public GameObject brownWolfPrefab;
    [SerializeField] public GameObject blackWolfPrefab;
    [SerializeField] public GameObject darkBat;
    [Tooltip("The Camp Fields' deep roster. Moonlit bat: 35hp, three-quarter size, half again as fast.")]
    [SerializeField] public GameObject moonlitBat;
    [Tooltip("Deep roster slime. Half size, double hp per tier, double speed - the numbers live on the prefab.")]
    [SerializeField] public GameObject moonlitSlimePrefab;
    [Tooltip("Deep roster rat. Grey's movement exactly, at 40hp. Fills the Moonlit slot of a map's rat cadence.")]
    [SerializeField] public GameObject moonlitRat;
    [SerializeField] public GameObject ogrePrefab;
    [SerializeField] public GameObject fireOgrePrefab;
    [Tooltip("Every Nth bat of a wave spawns as a dark bat once past the gate boss. Deterministic — wave content must never roll dice")]
    [SerializeField] private int darkBatInterval = 4;
    private int _batCallCount; // bat spawn calls this wave, in wave-script order
    private int _ratCallCount; // rat spawn calls this wave, in wave-script order
    [SerializeField] public GameObject healthOrbPrefab;
    [SerializeField] public GameObject manaOrbPrefab;
    [Tooltip("Mine-map track builder. Waves ask for a RailLayout through Spawner.Rails.")]
    [SerializeField] private RailNetwork railNetwork;
    [Tooltip("Castle-map mirror builder. Waves ask for a MirrorLayout through Spawner.Mirrors. Left empty on purpose — MirrorNetwork builds itself on first use.")]
    [SerializeField] private MirrorNetwork mirrorNetwork;

    private Transform _leftPlayer;
    private Transform _rightPlayer;
    private bool _isWaveInProgress;
    private bool _isUpgradeMenuActive; // Track if upgrade menu is showing
    private bool _isTransitionActive;  // Curtain is closing or opening
    // An NPC is on the board. Holds off StartNextWave exactly as the other three do.
    private bool _isQuestSceneActive;
    // Prose for the stage the run is about to enter, shown over black after the
    // upgrade pick. Non-empty also means "this gap gets the long ceremony".
    private string _pendingVentureLine;

    #region common references
    // Public methods for spawning that can be used by wave classes
    public Transform LeftPlayer => _leftPlayer;
    public Transform RightPlayer => _rightPlayer;
    public Vector2 aboveLeftPlayer => new Vector2(-2, 7);
    public Vector2 aboveRightPlayer => new Vector2(2, 7);
    public Vector2 belowLeftPlayer => new Vector2(-2, -7);
    public Vector2 belowRightPlayer => new Vector2(2, -7);
    public Vector2 leftOfLeftPlayer => new Vector2(-12, 0);
    public Vector2 rightOfRightPlayer => new Vector2(12, 0);
    public Vector2 topLeftCorner => new Vector2(-12, 6);
    public Vector2 topRightCorner => new Vector2(12, 6);
    public Vector2 bottomLeftCorner => new Vector2(-12, -6);
    public Vector2 bottomRightCorner => new Vector2(12, -6);

    // ---- the frame, and staying out of it -------------------------------
    //
    // The camera is orthographic at size 5.625, parked at the origin, and the game
    // is authored for 16:9 — so the visible world is exactly x ± 10, y ± 5.625.
    // Hardcoded rather than read off Camera.main on purpose: the aspect follows the
    // window, and a spawn point that moved with somebody's window size would make
    // the same wave play differently on two machines. OgreBand has carried these
    // same two numbers for a while; they live here now so everything can use them.
    public const float ViewHalfWidth = 10f;
    public const float ViewHalfHeight = 5.625f;

    // How far past the edge a walk-on starts. Enough that the body and its shadow
    // are fully clear of the frame before it begins moving in.
    private const float OffscreenMargin = 1.5f;

    /// <summary>
    /// Is this point somewhere the player can actually see?
    /// </summary>
    public static bool IsInView(Vector2 point)
    {
        return Mathf.Abs(point.x) < ViewHalfWidth && Mathf.Abs(point.y) < ViewHalfHeight;
    }

    /// <summary>
    /// The point just off the edge of the frame that <paramref name="destination"/>
    /// should be walked on from. Already-offscreen points are handed straight back,
    /// so this is safe to run over every spawn.
    ///
    /// Pushed out along whichever edge is NEAREST, which keeps the walk-on short and
    /// keeps the direction the enemy arrives from close to the direction it was
    /// authored to be at — a point in the middle of the board is far more often
    /// "just above the arena" than "miles off to the left".
    /// </summary>
    public static Vector2 OffscreenEntryFor(Vector2 destination)
    {
        if (!IsInView(destination)) return destination;

        float outX = ViewHalfWidth + OffscreenMargin;
        float outY = ViewHalfHeight + OffscreenMargin;

        // Ties go to the vertical edge. Arbitrary, but FIXED — the one thing this
        // must not do is pick a different side on different runs.
        return (outY - Mathf.Abs(destination.y)) <= (outX - Mathf.Abs(destination.x))
            ? new Vector2(destination.x, destination.y >= 0f ? outY : -outY)
            : new Vector2(destination.x >= 0f ? outX : -outX, destination.y);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Shouts when something is put down where the player can see it appear.
    ///
    /// Bats, slimes and ogres are placed exactly where the wave asks and never move
    /// themselves afterwards, so an in-frame spawn point IS an enemy popping into
    /// existence in front of the player. (Rats are the exception — EnemyRat.Start
    /// walks itself out to the nearest edge first — and wolves are handled outright
    /// in SpawnWolf.) A warning rather than a correction because the deliberate
    /// cases are real: a delivery cart unloads its passenger on screen by design,
    /// and the rat king summons bats out of his own body. Those say so at the call
    /// site with entersFromOffscreen: false.
    /// </summary>
    private static void WarnIfOnScreen(string what, Vector2 at, bool entersFromOffscreen)
    {
        if (!entersFromOffscreen || !IsInView(at)) return;
        Debug.LogWarning($"[Spawner] {what} spawned at {at}, which is INSIDE the view " +
                         $"(x +/-{ViewHalfWidth}, y +/-{ViewHalfHeight}). It will pop into " +
                         "existence in front of the players instead of walking in from " +
                         "off-frame. Move the spawn point out, or pass " +
                         "entersFromOffscreen: false if appearing on screen is the point.");
    }
#else
    private static void WarnIfOnScreen(string what, Vector2 at, bool entersFromOffscreen) { }
#endif

    // Null on maps with no track in the scene — wave scripts must null-check
    public RailNetwork Rails
    {
        get
        {
            if (railNetwork == null)
                railNetwork = FindFirstObjectByType<RailNetwork>(FindObjectsInactive.Include);
            return railNetwork;
        }
    }

    // Never null, unlike Rails: the castle's panes are built at runtime rather
    // than authored into the arena scene, so asking for them is what creates
    // them. A map that never asks never gets one — see MirrorNetwork.Ensure.
    public MirrorNetwork Mirrors
    {
        get
        {
            if (mirrorNetwork == null) mirrorNetwork = MirrorNetwork.Ensure();
            return mirrorNetwork;
        }
    }
    #endregion

    void Awake()
    {
        // UpgradeManager is a ScriptableObject, so owned/applied upgrade state
        // survives scene reloads; clear it so each run starts fresh
        if (upgradeManager != null)
        {
            upgradeManager.ResetRunState();
        }
    }

    void Start()
    {
        _leftPlayer = GameObject.FindWithTag("PlayerLeft").transform;
        _rightPlayer = GameObject.FindWithTag("PlayerRight").transform;

        // Equipment chosen in camp lands before anything spawns. Nothing to reset
        // the way UpgradeManager needs — Loadout reads the save fresh each run and
        // EquipmentBoost is a scene component that dies with the scene.
        Loadout.ApplyToKnight(_leftPlayer.gameObject, Loadout.LeftKnight);
        Loadout.ApplyToKnight(_rightPlayer.gameObject, Loadout.RightKnight);

        // Static state survives scene reloads, so this must run every run
        RunPurity.BeginRun(_leftPlayer.gameObject, _rightPlayer.gameObject);

        // Belt and braces for the same static: a run that ended without OnDestroy
        // running (a domain reload mid-wave) must not start this one mid-journal.
        PlayerStats.DiscardWave();

        // Static and therefore survives a scene reload, exactly like RunPurity above
        GuardianAwakening.BeginRun();
        // Per-wave and per-run quest records. Static, so it must be cleared at the
        // START of a run rather than trusted to be empty from the last one.
        QuestTally.BeginRun();
        // The queue is deliberately NOT cleared here. A quest can open while the
        // player is standing in camp — the tutorial run ending is the obvious case,
        // since the freeze lifting is what opens them — and camp has no arena to
        // play a scene in. Dropping the queue at the start of the next run meant
        // those quests never got introduced at all. The save's record of what has
        // been announced is what stops anything being introduced twice.
        GuidedAim.BeginRun();
        // Live-body tallies are static and survive the scene load, so a run that
        // ended with things still poisoned would start the next one mid-count.
        EnemyBase.ResetLivePoisonedCount();
        DawnVigil.BeginRun();


        // Setup upgrade menu callback
        if (upgradeMenu != null)
        {
            upgradeMenu.OnUpgradeConfirmed += OnUpgradeConfirmed;
        }

        if (waveSurvivedPanel == null)
        {
            waveSurvivedPanel = FindFirstObjectByType<WaveSurvivedPanel>(FindObjectsInactive.Include);
        }

        // Fresh per-run state (wave count, boss flags, wave pool) even without
        // a domain reload — matters for restarting runs in builds
        waveManager.BeginRun();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ApplyTestRunConfigIfPending();
#endif

        // Whetstone: a knight carrying it drafts once before anything spawns.
        // If nobody has one this falls straight through to wave one.
        QueueHeadStarts();

        // A brand-new file learns the controls before it fights anything. The
        // tutorial owns the arena until it is done, then calls BeginFirstWave
        // itself — so wave one starts the same way either way.
        // Assigned every run, not just the tutorial one — the flag is static and
        // outlives the scene load, so leaving it alone on an ordinary run would
        // keep quests frozen for the rest of the session.
        // Whether this is the tutorial run is a fact about the SAVE, not about how
        // the player got here. Asking TutorialDirector instead meant a file that
        // reached the arena by any route other than the file select — wiping from
        // the camp menu, most obviously — reported false and had every quest live
        // on what was still its first run.
        //
        // Read BEFORE TryBegin: the director writes tutorialCompleted true and then
        // hands straight off to wave one, so afterwards the save already says the
        // tutorial is behind us.
        TutorialRun.NoteRunStarted(!SaveManager.Data.tutorialCompleted);

        bool tutorial = TutorialDirector.TryBegin(this);
        if (tutorial) return;

        BeginFirstWave();
    }

    /// <summary>
    /// Opens the run: any owed head-start draft first, then wave one. Called
    /// straight from Start on an ordinary run, and by the tutorial on the far side
    /// of itself.
    /// </summary>
    public void BeginFirstWave()
    {
        if (!TryShowNextHeadStart()) StartNextWave();
    }

    // ---- Head start (Whetstone) ----

    private readonly Queue<KnightTarget> _pendingHeadStarts = new Queue<KnightTarget>();
    private bool _headStartActive;

    private void QueueHeadStarts()
    {
        _pendingHeadStarts.Clear();
        var left = _leftPlayer != null ? _leftPlayer.GetComponent<EquipmentBoost>() : null;
        if (left != null && left.GrantsHeadStart) _pendingHeadStarts.Enqueue(KnightTarget.LeftKnight);
        var right = _rightPlayer != null ? _rightPlayer.GetComponent<EquipmentBoost>() : null;
        if (right != null && right.GrantsHeadStart) _pendingHeadStarts.Enqueue(KnightTarget.RightKnight);
    }

    /// <summary>
    /// Opens the draft for the next knight owed a head start. The pick is forced
    /// to that knight rather than following the menu's wave-parity alternation —
    /// the item was bought for one of them specifically.
    /// </summary>
    private KnightTarget _preHeadStartTarget;
    private bool _headStartTargetSaved;

    private bool TryShowNextHeadStart()
    {
        if (_pendingHeadStarts.Count == 0 || upgradeMenu == null) return false;

        var target = _pendingHeadStarts.Dequeue();

        // Two different things decide "which knight": UpgradeManager picks the
        // POOL, the menu picks who it gets APPLIED to. Both have to be forced or
        // the player drafts from one knight's pool onto the other.
        if (upgradeManager != null)
        {
            if (!_headStartTargetSaved)
            {
                _preHeadStartTarget = upgradeManager.NextTarget;
                _headStartTargetSaved = true;
            }
            upgradeManager.SetNextTarget(target);
        }

        _headStartActive = true;
        ShowUpgradeMenu();
        // AFTER ShowUpgradeMenu, never before: SetMenuVisible(true) re-derives
        // the target from wave parity, which at wave zero is always the right
        // knight — it would silently overwrite this
        upgradeMenu.SetKnightTargetForThisUpgrade(target);
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Dev-only Test Mode: consume a setup authored in the camp — jump the wave
    // counter and pre-apply the chosen upgrades before the first wave spawns.
    private void ApplyTestRunConfigIfPending()
    {
        TestRunConfig.ActiveRun = TestRunConfig.Pending;
        if (!TestRunConfig.Pending) return;

        waveManager.ApplyTestStart(TestRunConfig.StartWave);

        // Interleave L/R picks so UpgradeManager's turn alternation ends up in
        // a natural state; each list is already ordered lowest tier first.
        var left = TestRunConfig.LeftUpgrades;
        var right = TestRunConfig.RightUpgrades;
        int most = Mathf.Max(left.Count, right.Count);
        for (int i = 0; i < most; i++)
        {
            if (i < left.Count && left[i] != null && upgradeManager != null)
                upgradeManager.ApplyUpgrade(left[i], KnightTarget.LeftKnight);
            if (i < right.Count && right[i] != null && upgradeManager != null)
                upgradeManager.ApplyUpgrade(right[i], KnightTarget.RightKnight);
        }

        Debug.Log($"[TestMode] Starting at wave {TestRunConfig.StartWave} with " +
                  $"{left.Count} left / {right.Count} right upgrade levels.");
        TestRunConfig.Clear();
    }
#endif

    public void StartNextWave()
    {
        // The curtain owns the call to this while it's up — a stray one would
        // start a wave the player can't see
        if (_isWaveInProgress || _isUpgradeMenuActive || _isTransitionActive || _isQuestSceneActive
            || TrialRunner.IsRunning)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test runs choose every wave by hand (or via the AutoPickWave hook)
        if (TestRunConfig.ActiveRun)
        {
            if (_isWavePickerActive) return;
            StartCoroutine(PickNextWaveThenStart());
            return;
        }
#endif

        BeginWave(waveManager.SelectNextWave());
    }

    private static PlayerHealth FindKnightHealth(string knightTag)
    {
        GameObject knight = GameObject.FindWithTag(knightTag);
        return knight != null ? knight.GetComponent<PlayerHealth>() : null;
    }

    /// <summary>
    /// Runs whatever the wave queued, with the arena frozen and the flag up so no
    /// stray StartNextWave can spawn a wave underneath an NPC. A no-op when nothing
    /// is pending, which is the common case.
    /// </summary>
    private IEnumerator PlayQuestScenes()
    {
        if (!QuestSceneQueue.HasPending) yield break;

        _isQuestSceneActive = true;
        Time.timeScale = 0f;

        // Health, specials and streaks belong to a fight, and there is no fight
        // while somebody is standing in the middle of the arena talking. Taken down
        // for the whole queue rather than per scene, so two quests in a row do not
        // flash the bars back between them.
        yield return StartCoroutine(ArenaHud.FadeTo(0f));

        yield return StartCoroutine(QuestSceneQueue.PlayPending());

        // Here rather than at the end of each scene, for the same reason. The ward
        // ring is built once and never rebuilt, so leaving it silenced would cost
        // it for the rest of the run.
        ArenaSweep.Restore();
        yield return StartCoroutine(ArenaHud.FadeTo(1f));

        Time.timeScale = 1f;
        _isQuestSceneActive = false;
    }

    /// <summary>
    /// Takes the last wave's leftovers off the field ahead of a trial — carts and
    /// their track, the castle's mirrors, stray orbs and ordnance. See
    /// ArenaSweep.ClearPlayingField. The next wave builds its own track and panes
    /// when it starts, exactly as it does after an upgrade pick.
    /// </summary>
    public void ClearFixturesForTrial()
    {
        // Through the field as well as the singleton: the network is built lazily
        // and this spawner may be holding one that has not registered itself.
        if (mirrorNetwork != null) mirrorNetwork.Clear();
        ArenaSweep.ClearPlayingField();
    }

    private void BeginWave(BaseWave nextWave)
    {
        if (nextWave == null) return;
        if (waveNameDisplay != null)
            waveNameDisplay.DisplayWaveName(nextWave.GetFormattedWaveName(waveManager.CurrentWaveNumber));
        _isWaveInProgress = true;
        UpdateWaveMusic(nextWave);
        _batCallCount = 0; // dark-bat cadence restarts every wave
        _ratCallCount = 0; // and the rat cadence with it - see NextRatInCadence
        DawnVigil.BeginWave();
        QuestTally.BeginWave();
        // Everything this wave counts is provisional until it is survived. A wave
        // that kills a knight banks nothing — see PlayerStats.BeginWave.
        PlayerStats.BeginWave();
        // Last wave's track comes down as this one starts, so rails stay up
        // through the wave-complete beat and the upgrade menu
        if (Rails != null) Rails.ClearAll();
        // Same beat for the castle's mirrors. Asked for through the field rather
        // than the property so a map with no mirrors does not build a network
        // just to tear it down again.
        if (mirrorNetwork != null) mirrorNetwork.Clear();
        StartCoroutine(RunWave(nextWave));
    }

    /// <summary>
    /// Hand the score the wave that is starting. Called from BeginWave, so it runs
    /// for EVERY wave including the ones Test Mode forces — the map decides what
    /// should be playing and AudioManager.PlayMusic ignores a request for the track
    /// already on, so the common case (wave 4 following wave 3) costs a comparison
    /// and nothing else.
    ///
    /// Asked with the wave object rather than the wave number because that is what
    /// actually knows whether a boss is on: the gate does not come back on its wave
    /// number once it has fallen this run.
    /// </summary>
    private void UpdateWaveMusic(BaseWave wave)
    {
        if (AudioManager.Instance == null || waveManager == null) return;
        var map = waveManager.CurrentMap;
        if (map == null) return;

        AudioManager.Instance.PlayMusic(
            map.MusicForWave(waveManager.CurrentWaveNumber, map.IsBossWave(wave)));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _isWavePickerActive;

    // Dev-only Test Mode: before each wave, surface everything the selector
    // could pick (scheduled boss + playable pool) and let the tester decide.
    private IEnumerator PickNextWaveThenStart()
    {
        _isWavePickerActive = true;

        // Death-screen retry: force the wave that killed you, once
        var retryWave = TestRunConfig.RetryWave;
        if (retryWave != null)
        {
            TestRunConfig.RetryWave = null;
            _isWavePickerActive = false;
            waveManager.ForceSelectWave(retryWave);
            BeginWave(retryWave);
            yield break;
        }

        var scheduledBoss = waveManager.PeekScheduledBoss();
        var pool = waveManager.GetPlayableCandidates();
        var locked = waveManager.GetLockedCandidates();

        // Automation hook: a named wave (or "*" for weighted random) skips the
        // UI entirely so MCP-driven validation never stalls on input
        string auto = TestRunConfig.AutoPickWave;
        if (!string.IsNullOrEmpty(auto))
        {
            BaseWave match = null;
            if (auto != "*")
            {
                if (scheduledBoss != null && string.Equals(scheduledBoss.name, auto, System.StringComparison.OrdinalIgnoreCase))
                    match = scheduledBoss;
                foreach (var wave in pool)
                {
                    if (match != null) break;
                    if (string.Equals(wave.name, auto, System.StringComparison.OrdinalIgnoreCase))
                        match = wave;
                }
                // Locked waves are fair game for automation — forcing a wave
                // outside its unlock window is a legitimate test
                foreach (var wave in locked)
                {
                    if (match != null) break;
                    if (string.Equals(wave.name, auto, System.StringComparison.OrdinalIgnoreCase))
                        match = wave;
                }
                if (match == null)
                    Debug.LogWarning($"[TestMode] AutoPickWave '{auto}' matched no candidate; using weighted random.");
            }
            _isWavePickerActive = false;
            if (match != null)
            {
                waveManager.ForceSelectWave(match);
                BeginWave(match);
            }
            else
            {
                BeginWave(waveManager.SelectNextWave());
            }
            yield break;
        }

        var picker = TestWavePicker.GetOrCreate();
        if (picker == null)
        {
            // No UI to piggyback on — behave like a normal run
            _isWavePickerActive = false;
            BeginWave(waveManager.SelectNextWave());
            yield break;
        }

        BaseWave chosen = null;
        bool decided = false;

        Time.timeScale = 0f;
        picker.Show(scheduledBoss, pool, locked, waveManager.CurrentWaveNumber, wave =>
        {
            chosen = wave;
            decided = true;
        });
        while (!decided) yield return null;
        Time.timeScale = 1f;

        _isWavePickerActive = false;

        if (chosen != null)
        {
            waveManager.ForceSelectWave(chosen);
            BeginWave(chosen);
        }
        else
        {
            BeginWave(waveManager.SelectNextWave()); // "Random" row / B button
        }
    }
#endif

    private IEnumerator RunWave(BaseWave wave)
    {
        // Start wave tracking
        wave.StartWaveTracking();

        // The rock cadence is per wave, not per run: "every tenth is poisoned"
        // has to mean the same thing on the first wave of a run and the twentieth.
        RockEscalation.ResetForWave();

        // Meeting a wave type counts as having explored it, win or lose.
        // Credited to the map it belongs to — explorer quests are per map.
        var exploredMap = waveManager != null ? waveManager.CurrentMap : null;
        if (exploredMap != null) WaveExploration.RecordSeen(wave, exploredMap.MapId);
        
        // Run the wave spawn logic
        yield return StartCoroutine(wave.SpawnWave(this));
        
        // Wait for all enemies to be killed (if enemy tracking is enabled)
        yield return StartCoroutine(wave.WaitForAllEnemiesDead());
        
        // End wave tracking
        wave.EndWaveTracking();
        
        _isWaveInProgress = false;

        // The boss is down: take the music off and leave the upgrade screen quiet.
        // Asked here, before WaveCompleted moves the wave number on, and asked with
        // the wave OBJECT so it is answering "was that a boss" rather than "is this
        // a boss wave number" — which stops being the same question the moment a
        // gate that has already fallen this run is not rescheduled.
        //
        // Nothing turns it back on: the next wave's BeginWave does, with whatever
        // the map says should play at that depth. On a boss that ENDS the run the
        // silence carries through the victory banner into camp, where the camp
        // track comes up on the scene load.
        bool wasBossWave = waveManager.CurrentMap != null && waveManager.CurrentMap.IsBossWave(wave);
        if (AudioManager.Instance != null && wasBossWave)
        {
            AudioManager.Instance.StopMusic();
        }

        // Read before WaveCompleted so it is answering the wave that just ran,
        // and before the upgrade menu can heal anybody.
        DawnVigil.EndWave(
            FindKnightHealth("PlayerLeft"),
            FindKnightHealth("PlayerRight"));

        // The backdrop freezes BEFORE the wave number moves. CurrentWaveNumber is
        // what BackgroundController polls, so holding it any later than this - after
        // the "Wave Survived" panel, as it used to - let the crossing wave's new
        // backdrop pop up over the cleared arena while the player was still looking
        // at it. Released behind the black curtain further down.
        BackgroundController.Instance?.Hold();

        waveManager.WaveCompleted();

        // The wave was survived, so everything it counted really happened: replay
        // the journal down the ordinary paths (quest meters, fan-out and all), and
        // only then write to disk. A knight who died never reaches this line and
        // the wave's counting is dropped instead — see HandlePlayerDeathTransition.
        PlayerStats.CommitWave();

        // Stat writes are batched in memory rather than hitting disk per kill;
        // the wave boundary is the natural commit point, so a crash mid-run
        // costs at most this wave's counters
        PlayerStats.Flush();

        // Ember fire dies with the wave, not the run. Scorched Earth zones never
        // expire on their own, so without this the next wave would begin inside
        // the last one's inferno and the difficulty curve would invert.
        FireField.ClearAll();

        // And the ice with it. A rank three hold runs fifteen seconds, so a statue
        // made at the end of wave eleven would still be standing well into wave
        // twelve - the same inverted difficulty curve, in blue.
        FrigidBoost.ClearFieldFrost();

        // The knights' poison dies with the wave for the same reason - see
        // KnightPoison.CureAll. It is a clock to beat inside a fight, not a debt
        // to carry into the next one.
        KnightPoison.CureAll();

        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp)
        {
            // Every path out of here has to let the backdrop go, or the hold above
            // outlives the wave that took it. Release, not ReleaseAndApply: the
            // arena is still lit and the player is on their way out of it.
            BackgroundController.Instance?.Release();
            VentureCurtain.ForceClear();
            // The player is leaving. Any completion this wave queued is dropped
            // rather than left to surface over the first wave of the NEXT run — the
            // queue is static and would otherwise carry straight across the scene
            // load. Offers are not in that queue and are deliberately not dropped:
            // the save still records them as owed, and they are paid on the map
            // they belong to. See QuestSceneQueue.OwedHere.
            QuestSceneQueue.Discard();
            // Belt and braces: a run abandoned while a scene was up must not leave
            // the next one with no bars.
            ArenaHud.ShowImmediate();
            yield break;
        }

        // Boss outcomes: a first gate clear or a true-boss kill ends the run in victory
        var outcome = waveManager.ConsumePendingOutcome();
        if (outcome != RunOutcome.None && GameSceneManager.Instance != null)
        {
            // The tutorial run is over HERE, not at the scene load. The gate
            // lifting is what opens the quests the run earned, and lifting it on
            // the way into camp opened them somewhere no NPC can walk on to say
            // so — a new player's first look at the quest log was a list of things
            // they had apparently already been offered. Lifting it now means the
            // King finishes his line and the rest of the forest introduces itself
            // over the same cleared arena.
            TutorialRun.NoteRunStarted(false);

            // The scenes run FIRST. A boss kill is the most quest-laden moment in
            // the game — A Ruckus in the Wood, The Crimson Twins and The Gold Cart
            // all complete on one — and this path used to return before anything
            // was announced, leaving them queued behind a scene load that never
            // played them. The victory screen can wait for the King.
            yield return PlayQuestScenes();

            // Release WITHOUT applying. The boss kill already moved
            // CurrentWaveNumber into the next stage, so swapping here would raise
            // the deep forest behind the victory banner — on the very run that
            // earned the right to see it properly. OnVictory takes its own hold a
            // line later; this only hands the backdrop over between the two.
            BackgroundController.Instance?.Release();
            VentureCurtain.ForceClear();
            GameSceneManager.Instance.OnVictory(waveManager.CurrentMap, outcome == RunOutcome.TrueVictory,
                waveManager.CompletedWavesCount);
            yield break;
        }

        // "Wave Survived" over the cleared arena. It owns the whole pause now —
        // a clean wave holds for a beat and moves on by itself, a wave that
        // finished a quest waits for the player. The old flat two-second wait
        // ran on top of this and just made every gap feel long.
        if (waveSurvivedPanel != null)
        {
            Time.timeScale = 0f;
            yield return StartCoroutine(waveSurvivedPanel.ShowWaveSurvived());
            Time.timeScale = 1f;
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // One between-wave event at most — an Order trial or the target range —
        // over the same lit arena the NPCs use. Ranked: a quest scene beats a
        // trial beats the range, and nothing plays after a boss. See
        // BetweenWaveEvents.
        var gapMap = waveManager.CurrentMap;
        yield return BetweenWaveEvents.PlayOne(this, waveManager.CompletedWavesCount, wasBossWave,
                                               gapMap != null ? gapMap.MapId : "");

        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp)
        {
            // Walked out mid-event. Same exit as the one above.
            BackgroundController.Instance?.Release();
            VentureCurtain.ForceClear();
            QuestSceneQueue.Discard();
            ArenaHud.ShowImmediate();
            yield break;
        }

        // The NPCs, over the cleared arena while it is still lit. Before the
        // curtain deliberately: these are people walking into the field the player
        // just fought in, not prose over black. After the trial, so a trial that
        // finished its initiation is answered by the Order's own completion scene.
        yield return PlayQuestScenes();

        // Take the screen to black before the menu. Crossing into a stage that
        // carries a venture line (forest -> deep forest) earns the long close;
        // every other gap gets the short fade, so the two read as one language.
        var map = waveManager.CurrentMap;
        int entering = waveManager.CurrentWaveNumber; // already advanced by WaveCompleted()
        var stage = map != null ? map.StageForWave(entering) : null;
        bool crossing = map != null && stage != map.StageForWave(entering - 1);
        _pendingVentureLine = (crossing && stage != null) ? stage.ventureLine : null;

        _isTransitionActive = true;
        Time.timeScale = 0f;
        yield return StartCoroutine(VentureCurtain.Close(!string.IsNullOrEmpty(_pendingVentureLine)));
        // The backdrop stays HELD through the upgrade menu and is swapped later,
        // on the venture line — see RaiseCurtainThenStartWave. Swapping it here
        // put the new stage up before the line that explains it, so the crossing
        // was already over by the time the player was told it had happened.
        _isTransitionActive = false;

        // Show upgrade menu and pause game instead of immediately starting next wave
        ShowUpgradeMenu();
    }

    private void ShowUpgradeMenu()
    {
        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp)
        {
            _isUpgradeMenuActive = false;
            _pendingVentureLine = null;
            // Bailing out before RaiseCurtainThenStartWave, which is what would
            // normally have released it. Nothing to apply — the player is leaving.
            BackgroundController.Instance?.Release();
            VentureCurtain.ForceClear();
            return;
        }

        _isUpgradeMenuActive = true;
        
        // Pause the game
        Time.timeScale = 0f;
        
        // Show the upgrade menu
        if (upgradeMenu != null)
        {
            upgradeMenu.SetMenuVisible(true);
        }
    }

    private void OnUpgradeConfirmed(int upgradeIndex, KnightTarget selectedKnight)
    {
        BaseUpgrade selectedUpgrade = upgradeMenu.GetChosenUpgrade();
        
        if (selectedUpgrade != null && upgradeManager != null)
        {
            upgradeManager.ApplyUpgrade(selectedUpgrade, selectedKnight);
        }
        else
        {
            Debug.Log($"Upgrade {upgradeIndex} was confirmed for {selectedKnight}");
        }
        
        // Hide upgrade menu, uncovering the black it was sitting on
        if (upgradeMenu != null)
        {
            upgradeMenu.SetMenuVisible(false);
        }

        _isUpgradeMenuActive = false;

        // A head start happens before wave one, so there is no curtain down to
        // raise — going through RaiseCurtainThenStartWave here would try to lift
        // black that was never drawn
        if (_headStartActive)
        {
            _headStartActive = false;
            if (!TryShowNextHeadStart())
            {
                // A head start is a bonus draft, not a turn — wave one's pick
                // still belongs to whoever the alternation owed it to
                if (upgradeManager != null && _headStartTargetSaved)
                {
                    upgradeManager.SetNextTarget(_preHeadStartTarget);
                }
                StartNextWave();
            }
            return;
        }

        _isTransitionActive = true;
        StartCoroutine(RaiseCurtainThenStartWave(!string.IsNullOrEmpty(_pendingVentureLine)));
    }

    // Owns the resume: the venture line plays over the black the menu just
    // left behind, then the curtain lifts on the new backdrop. Nothing else
    // calls StartNextWave while _isTransitionActive is set, so this must.
    private IEnumerator RaiseCurtainThenStartWave(bool ceremonial)
    {
        // The stage changes HERE, under the black and behind the line that
        // announces it, rather than the moment the curtain finished closing.
        // Nothing is visible until Open() below, so this is the last possible
        // point that is still safely out of sight.
        BackgroundController.Instance?.ReleaseAndApply();

        if (!string.IsNullOrEmpty(_pendingVentureLine))
        {
            yield return StartCoroutine(VentureCurtain.ShowLine(_pendingVentureLine));
            _pendingVentureLine = null;
        }

        // Time returns before the light does, so the knights idle back to life
        // as the arena comes up rather than snapping to it
        Time.timeScale = 1f;
        yield return StartCoroutine(VentureCurtain.Open(ceremonial));

        _isTransitionActive = false;
        StartNextWave();
    }

    public void HandlePlayerDeathTransition()
    {
        // The wave was not survived, so nothing it counted is kept: no kill toward
        // an objective, and no quest finished on the blow that killed you.
        PlayerStats.DiscardWave();

        _isUpgradeMenuActive = false;
        _isTransitionActive = false;
        _pendingVentureLine = null;
        // Dying mid-curtain must never leave the player staring at black, nor at
        // a backdrop still frozen on the wave they died in.
        BackgroundController.Instance?.ReleaseAndApply();
        VentureCurtain.ForceClear();
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // The arena is going away. Any wave still open was not survived, and its
        // journal must not be left standing — PlayerStats is static, so a journal
        // left open here would swallow every write made in CAMP (quest turn-ins,
        // equipment) and then throw them away at the next wave. A legitimate
        // completion has already committed by this point, so this only ever drops
        // a wave nobody finished.
        PlayerStats.DiscardWave();

        // Clean up event subscription
        if (upgradeMenu != null)
        {
            upgradeMenu.OnUpgradeConfirmed -= OnUpgradeConfirmed;
        }
    }

    // Which enemies a map holds back, and until when, is the MAP's business and
    // not the game's. The Camp Fields staggers its stronger enemies — dark bats,
    // black wolves, brown and black rats — in behind the rat king, so earlier
    // waves get the grey stand-in instead (which is what lets a wave asset
    // straddle the boss). In the Mine they are just what lives down there, from
    // wave one, and it will stagger in its own additions on its own schedule.
    private bool StrongerEnemiesAllowed
    {
        get
        {
            if (waveManager == null) return true;
            var map = waveManager.CurrentMap;
            int fromWave = map != null ? map.StrongerEnemiesFromWave : 11;
            return waveManager.CurrentWaveNumber >= fromWave;
        }
    }

    // Is the map's deep roster live? The Camp Fields' moonlit waves, past the
    // Twins: pale bats sharing the air with the dark ones, pale slimes half the
    // size and twice the weight, and grey wolves coming out black. A map with no
    // deep roster answers false forever and nothing is ever substituted.
    //
    // Rats are NOT decided here. They run on the map's rat cadence, which turns
    // over on its own schedule and on both maps — see NextRatInCadence.
    private bool DeepRosterAllowed
    {
        get
        {
            if (waveManager == null) return false;
            var map = waveManager.CurrentMap;
            if (map == null) return false;
            int fromWave = map.DeepRosterFromWave;
            return fromWave >= 0 && waveManager.CurrentWaveNumber >= fromWave;
        }
    }

    /// <summary>
    /// Puts a rat down. WHICH rat is not the wave's decision: leave
    /// <paramref name="ratType"/> null — which is what every ordinary wave does — and
    /// the map's own cadence supplies it (MapDefinition.RatPhase). Waves choose
    /// where and when; the map chooses what, because what is a fact about how deep
    /// the run is rather than about which wave happens to be playing.
    ///
    /// Pass a prefab only when the spawn is meant to name its own rat whatever the
    /// depth — the rat king's brood, and Delivery's carts, whose cargo is authored
    /// per wave. Naming one does NOT advance the cadence, so a boss emptying itself
    /// mid-wave cannot shift the pattern the wave around it is running.
    ///
    /// `roster`, when given, collects the rat once it exists. A rat cannot be
    /// handed back from here — the spawn is a delayed coroutine — so a wave that
    /// has to know when ITS rats are dead (rather than when every enemy is) reads
    /// the roster and drops the entries Unity has nulled out.
    /// </summary>
    public void SpawnRat(Vector2 targetPosition, float delay, Transform playerTarget,
                         GameObject ratType = null, bool bypassStrengthGate = false,
                         Vector2? entryPoint = null, List<GameObject> roster = null)
    {
        // Resolved HERE rather than inside the coroutine, for the same reason the
        // dark-bat cadence is: coroutine wake-up order ties on equal delays, so
        // deciding after the wait would make the pattern non-deterministic. Read
        // at CALL time, in wave-script order, the same wave always sends the same
        // rats in the same order.
        if (ratType == null) ratType = NextRatInCadence();

        StartCoroutine(SpawnRatAfterDelay(targetPosition, ratType, delay, playerTarget, bypassStrengthGate, entryPoint, roster));
    }

    /// <summary>
    /// The next rat off the active map's cadence for the wave number the run is on.
    /// Falls back to the grey rat when a map declares no phases at all, so a map
    /// authored before this existed still spawns something.
    /// </summary>
    private GameObject NextRatInCadence()
    {
        var map = waveManager != null ? waveManager.CurrentMap : null;
        var cadence = map != null ? map.RatCadenceForWave(waveManager.CurrentWaveNumber) : null;
        if (cadence == null || cadence.Count == 0) return greyRat;

        RatType next = cadence[_ratCallCount % cadence.Count];
        _ratCallCount++;

        switch (next)
        {
            case RatType.Black:   return blackRat != null ? blackRat : greyRat;
            case RatType.Brown:   return brownRat != null ? brownRat : greyRat;
            case RatType.Moonlit: return moonlitRat != null ? moonlitRat : greyRat;
            default:              return greyRat;
        }
    }

    private IEnumerator SpawnRatAfterDelay(Vector2 targetPosition, GameObject ratType, float delay, Transform playerTarget, bool bypassStrengthGate, Vector2? entryPoint, List<GameObject> roster = null)
    {
        yield return new WaitForSeconds(delay);
        // NOTE: rats used to be downgraded to grey here when the map was holding
        // its stronger enemies back. That gate is gone, and deliberately: it was
        // what made black rats almost unreachable in the forest. Depth now decides
        // the rat BEFORE the spawn, in the map's cadence, so a shallow wave simply
        // has no black in its phase rather than asking for one and being refused.
        // bypassStrengthGate is kept on the signature for the rat king's brood,
        // which names its own prefab and so was never gated anyway.
        GameObject enemy = Instantiate(ratType);
        enemy.transform.position = targetPosition;

        EnemyRat enemyRat = enemy.GetComponent<EnemyRat>();
        if (enemyRat != null)
        {
            enemyRat.InitializeTarget(playerTarget);
            // entryPoint makes the rat scurry in from there (e.g. out of the
            // rat king) instead of walking in from the nearest screen edge
            if (entryPoint.HasValue)
            {
                enemyRat.SetEntryPoint(entryPoint.Value);
            }
        }

        if (roster != null) roster.Add(enemy);
    }

    /// <summary>
    /// Puts an ogre down at <paramref name="spawnPosition"/>. WHERE it lands is
    /// the whole authoring decision: the ogre reads the nearer knight off its own
    /// spawn point and walks at him for the rest of its life, so a wave chooses
    /// which knight is in trouble by choosing a side of the board. It picks the
    /// far knight to throw at in the same breath, which is why a fire ogre put
    /// down dead centre is a coin toss rather than a decision — place them out
    /// past a knight's shoulder, not on the midline.
    ///
    /// `roster` collects the ogre once it exists, the same contract SpawnRat
    /// uses, so a wave can hold itself open until its ogres in particular are down.
    /// </summary>
    /// <summary>
    /// Puts an ogre down at <paramref name="spawnPosition"/>, which must be off
    /// the edge of the frame. WHERE it lands is the whole authoring decision: an
    /// ogre walks a straight line from there to its knight and never deviates,
    /// so the entry point chooses which part of the board it crosses on the way.
    /// See OgreBand and OgreEntry, which is where waves say this.
    ///
    /// `mark` names the knight outright. Left null the ogre reads the nearer one
    /// off its own landing spot, which is right for anything clearly on one side
    /// and useless on the centre line, where the two are equidistant and the
    /// tie-break would hand every one of them to the same knight.
    ///
    /// `roster` collects the ogre once it exists, the same contract SpawnRat
    /// uses, so a wave can hold itself open until its ogres in particular are down.
    /// </summary>
    public void SpawnOgre(Vector2 spawnPosition, float delay = 0f, bool fire = false,
                          List<GameObject> roster = null, Transform mark = null,
                          float firstThrowDelay = 0f)
    {
        WarnIfOnScreen("Ogre", spawnPosition, true);
        StartCoroutine(SpawnOgreAfterDelay(spawnPosition, delay, fire, roster, mark, firstThrowDelay));
    }

    private IEnumerator SpawnOgreAfterDelay(Vector2 spawnPosition, float delay, bool fire,
                                            List<GameObject> roster, Transform mark,
                                            float firstThrowDelay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        GameObject prefab = fire ? fireOgrePrefab : ogrePrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"Spawner: no {(fire ? "fireOgrePrefab" : "ogrePrefab")} assigned; nothing spawned.");
            yield break;
        }

        GameObject ogre = Instantiate(prefab);
        ogre.transform.position = spawnPosition;

        // Before Start runs, the same contract EnemyRat.SetEntryPoint uses
        if (mark != null)
        {
            EnemyOgre brute = ogre.GetComponent<EnemyOgre>();
            if (brute != null) brute.AssignTarget(mark, mark == _leftPlayer ? _rightPlayer : _leftPlayer);
        }

        // Also before Start, which is where the fire ogre sets its first throw
        if (firstThrowDelay > 0f)
        {
            EnemyFireOgre thrower = ogre.GetComponent<EnemyFireOgre>();
            if (thrower != null) thrower.DelayFirstThrow(firstThrowDelay);
        }

        if (roster != null) roster.Add(ogre);
    }

    public void SpawnSlime(int size, Vector2 spawnPosition, float delay, Transform targetPlayer,
                           bool entersFromOffscreen = true)
    {
        WarnIfOnScreen("Slime", spawnPosition, entersFromOffscreen);
        StartCoroutine(SpawnSlimeAfterDelay(size, spawnPosition, delay, targetPlayer));
    }

    private IEnumerator SpawnSlimeAfterDelay(int size, Vector2 spawnPosition, float delay, Transform targetPlayer)
    {
        yield return new WaitForSeconds(delay);
        // Deep in the forest every slime is a moonlit one. The size argument still
        // means what it meant — the prefab's own multipliers are what make it half
        // the body and twice the weight, so a wave asking for a king still gets a
        // king, just a paler and meaner one.
        GameObject prefab = (DeepRosterAllowed && moonlitSlimePrefab != null)
            ? moonlitSlimePrefab
            : slimePrefab;
        GameObject slime = Instantiate(prefab);
        slime.transform.position = spawnPosition;

        EnemySlime slimeScript = slime.GetComponent<EnemySlime>();
        if (slimeScript != null)
        {
            slimeScript.size = size;
            slimeScript.targetPlayer = targetPlayer;
            slimeScript.InitializeSlime();
        }
    }

    public void SpawnBat(Vector2 spawnPosition, float delay, bool entersFromOffscreen = true)
    {
        WarnIfOnScreen("Bat", spawnPosition, entersFromOffscreen);

        // Decide dark-vs-normal at CALL time, in wave-script order: coroutine
        // wake-up order ties on equal delays, so deciding after the wait would
        // make the pattern non-deterministic. Same wave = same bats, always.
        _batCallCount++;
        StartCoroutine(SpawnBatAfterDelay(NextBatPrefab(), spawnPosition, delay));
    }

    /// <summary>
    /// Which bat this call of the wave sends. Reads _batCallCount, which the
    /// caller has already advanced, so it is the same contract NextRatInCadence
    /// has: the answer depends only on the position in the wave, never on when
    /// the coroutine happens to wake.
    ///
    /// The dark bat keeps its every-fourth slot and the moonlit one takes the
    /// other even beats, so a cycle of four reads basic, moonlit, basic, dark —
    /// half the flight ordinary, and the two ways it can be dangerous alternating
    /// behind it.
    /// </summary>
    private GameObject NextBatPrefab()
    {
        if (StrongerEnemiesAllowed && darkBat != null && darkBatInterval > 0
            && _batCallCount % darkBatInterval == 0)
        {
            return darkBat;
        }
        if (DeepRosterAllowed && moonlitBat != null && _batCallCount % 2 == 0)
        {
            return moonlitBat;
        }
        return bat;
    }

    private IEnumerator SpawnBatAfterDelay(GameObject prefab, Vector2 spawnPosition, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject enemy = Instantiate(prefab);
        enemy.transform.position = spawnPosition;
    }

    public void SpawnWolf(List<Vector2> waypoints, Transform targetKnight, WolfType wolfType, float delay = 0f)
    {
        StartCoroutine(SpawnWolfAfterDelay(waypoints, targetKnight, wolfType, delay));
    }

    private IEnumerator SpawnWolfAfterDelay(List<Vector2> waypoints, Transform targetKnight, WolfType wolfType, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (wolfType == WolfType.Black && !StrongerEnemiesAllowed)
        {
            wolfType = WolfType.Grey;
        }
        else if (wolfType == WolfType.Grey && DeepRosterAllowed)
        {
            // Nothing grey runs this deep. The two downgrade/upgrade arms can never
            // both fire: one wants the stronger enemies held back, the other wants
            // the deep roster live, and the deep wave is always the later one.
            wolfType = WolfType.Black;
        }

        GameObject prefabToUse = null;
        switch (wolfType)
        {
            case WolfType.Grey:  prefabToUse = greyWolfPrefab;  break;
            case WolfType.Brown: prefabToUse = brownWolfPrefab; break;
            case WolfType.Black: prefabToUse = blackWolfPrefab; break;
        }
        if (prefabToUse == null)
        {
            Debug.LogWarning($"Spawner: Missing prefab for wolf type {wolfType}. Please assign it in the Spawner inspector.");
            yield break;
        }
        GameObject wolf = Instantiate(prefabToUse);

        // Convert Vector2 waypoints to Vector3 for EnemyWolf API
        List<Vector3> wp3 = null;
        if (waypoints != null)
        {
            wp3 = new List<Vector3>(waypoints.Count);
            for (int i = 0; i < waypoints.Count; i++)
                wp3.Add(new Vector3(waypoints[i].x, waypoints[i].y, 0f));
        }

        // Put the wolf down OFF the frame and let it walk to the first waypoint,
        // rather than dropping it straight onto that waypoint.
        //
        // Several of the shared patterns open on a point in the middle of the
        // board — WolfMovementPatterns.CircleLeftThenRight starts at (-2, 2), which
        // is dead centre — and a wolf placed there simply appeared in front of the
        // player. Waves that knew this worked around it by hand (WolfCircles
        // prepends an entry point above the left knight before the circle); the
        // ones that did not, like About Face, spawned wolves in plain view.
        //
        // EnemyWolf does the rest for free: its Start measures the distance to
        // waypoint 0 and, finding itself somewhere else, walks there first.
        // Off-frame waypoints are handed back unchanged, so the hand-written
        // entries above are untouched.
        if (wp3 != null && wp3.Count > 0)
        {
            Vector2 entry = OffscreenEntryFor(wp3[0]);
            wolf.transform.position = new Vector3(entry.x, entry.y, 0f);
        }

        EnemyWolf wolfScript = wolf.GetComponent<EnemyWolf>();
        if (wolfScript != null)
        {
            wolfScript.SetWaypoints(wp3);
            wolfScript.SetTarget(targetKnight);
            wolfScript.SetWolfType(wolfType);
        }
        else
        {
            Debug.LogWarning("Spawner: EnemyWolf component not found on wolf instance.");
        }
    }

    /// <summary>
    /// The floor on the gap between two projectiles of the SAME group, in seconds
    /// (owner's rule, 2026-09-06): **a spawn pattern is always staggered, and two
    /// projectiles never leave on the same frame.**
    ///
    /// This is not a polish note, it is a fairness rule, and it comes straight out
    /// of the two-knight geometry. A knight has ONE guard and it covers one
    /// direction. Rock that arrives spread over time is a thing a player holds a
    /// facing through and reads; rock that arrives all at once is a number
    /// subtracted from their health with a picture attached, and no amount of
    /// aim, reaction or upgrade changes the outcome. Flight time is identical for
    /// every projectile of a group, so the gap between RELEASES is exactly the gap
    /// between ARRIVALS — which is why enforcing it here works at all.
    ///
    /// Enforced in the two group helpers below rather than left to callers,
    /// because "the wave author remembered" is not a guarantee: every wave, boss
    /// fan and shaft volley in the game funnels through SpawnProjectileStraight
    /// and SpawnProjectileArc, and a 0 passed to either used to mean "all of them,
    /// this frame". A caller asking for a bigger gap always gets the bigger gap;
    /// this only ever raises a floor.
    /// </summary>
    public const float MinProjectileStagger = 0.06f;

    // `speed` of 0 or less leaves the rock prefab's own speed alone, which is what
    // every caller but Delivery wants — see ProjectileMovement.Initialize for why
    // flight time belongs to the wave rather than to the prefab.
    public void SpawnProjectile(Transform targetPlayer, Vector2 spawnPosition, float delay = 0f, float speed = 0f)
    {
        // The variant is claimed HERE rather than inside the coroutine. A volley
        // is authored as an order — first this rock, then that one — and the
        // per-rock delays would otherwise shuffle the cadence out of that order.
        RockVariant variant = RockEscalation.NextVariant();
        BookArrival(targetPlayer, spawnPosition, delay, speed);
        StartCoroutine(SpawnProjectileAfterDelay(targetPlayer, spawnPosition, delay, speed, variant));
    }

    // A rock released later does not exist yet, so a fire ogre deciding on a
    // throw now could not see it (IncomingLedger). Its landing is booked here,
    // at the moment the wave schedules it.
    private void BookArrival(Transform targetPlayer, Vector2 spawnPosition, float delay, float speed)
    {
        if (targetPlayer == null || projectilePrefab == null) return;

        ProjectileMovement prefabMovement = projectilePrefab.GetComponent<ProjectileMovement>();
        float baseSpeed = speed > 0f ? speed : (prefabMovement != null ? prefabMovement.Speed : 0f);
        float flightSpeed = baseSpeed * Mathf.Max(1f, RockEscalation.SpeedScale);
        if (flightSpeed <= 0.0001f) return;

        Vector2 aim = (Vector2)targetPlayer.position + new Vector2(0f, 0.5f);
        float flight = Vector2.Distance(spawnPosition, aim) / flightSpeed;
        IncomingLedger.Book(targetPlayer, Time.time + Mathf.Max(0f, delay) + flight);
    }

    private IEnumerator SpawnProjectileAfterDelay(Transform targetPlayer, Vector2 spawnPosition, float delay,
                                                  float speed, RockVariant variant)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // AT the spawn point, not at the prefab's authored position. The rock's
        // trails simulate in WORLD space, so a rock that is born by the right
        // knight and only moved to its shaft a line later puffs a lungful of
        // bubbles over that knight before the throw has even started.
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

        // Dressed before it is aimed, so the trail is already alight on the
        // frame the rock first appears
        ProjectileSettings settings = projectile.GetComponent<ProjectileSettings>();
        if (settings != null) settings.Become(variant);

        ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();

        // Deep in the mine every rock hurries. Applied to whatever the wave asked
        // for; a wave that left the speed alone gets the prefab's own hurried.
        float scale = RockEscalation.SpeedScale;
        float launchSpeed = speed > 0f
            ? speed * scale
            : (scale > 1f ? pm.Speed * scale : 0f);

        pm.Initialize(targetPlayer, spawnPosition, launchSpeed);
    }

    private Vector2 ArcCenterFor(Transform targetPlayer)
    {
        return (targetPlayer == _leftPlayer) ? new Vector2(-2, 0) : new Vector2(2, 0);
    }

    // Seconds an arc-volley projectile takes to reach its knight from arcStart.
    // Mirrors SpawnProjectileArc's geometry so bosses can pace their volleys.
    public float ProjectileArcFlightSeconds(Transform targetPlayer, Vector2 arcStart)
    {
        float radius = Vector2.Distance(arcStart, ArcCenterFor(targetPlayer));
        // Must include the mine's late-run hurry, or a boss pacing its volleys
        // off this number would leave gaps it thinks are safe and are not.
        float speed = projectilePrefab.GetComponent<ProjectileMovement>().Speed * RockEscalation.SpeedScale;
        return radius / speed;
    }

    public void SpawnProjectileArc(Transform targetPlayer, ArcDirection direction, Vector2 arcStart, float arcDegrees, int projectileCount,
        float delayBetweenProjectiles, int arcCount = 1, float delayBetweenArcs = 0f, float speed = 0f)
    {
        // Never a whole arc on one frame — see MinProjectileStagger
        if (projectileCount > 1)
        {
            delayBetweenProjectiles = Mathf.Max(MinProjectileStagger, delayBetweenProjectiles);
        }

        Vector2 arcCenter = ArcCenterFor(targetPlayer);
        float radius = Vector2.Distance(arcStart, arcCenter);
        StartCoroutine(SpawnProjectileArcCoroutine(targetPlayer, direction, arcCenter, radius, arcStart, arcDegrees, projectileCount, 
            delayBetweenProjectiles, arcCount, delayBetweenArcs, speed));
    }

    private IEnumerator SpawnProjectileArcCoroutine(Transform targetPlayer, ArcDirection direction, Vector2 arcCenter, float radius, Vector2 arcStart, float arcDegrees, 
        int projectileCount, float delayBetweenProjectiles, int arcCount, float delayBetweenArcs, float speed)
    {
        float startAngle = Mathf.Atan2(arcStart.y - arcCenter.y, arcStart.x - arcCenter.x) * Mathf.Rad2Deg;
        float angleStep = arcDegrees / (projectileCount - 1);
        if (direction == ArcDirection.Clockwise) angleStep = -angleStep;

        for (int arc = 0; arc < arcCount; arc++)
        {
            if (arc > 0 && delayBetweenArcs > 0)
                yield return new WaitForSeconds(delayBetweenArcs);

            for (int i = 0; i < projectileCount; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector2 spawnPos = arcCenter + new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad)) * radius;
                SpawnProjectile(targetPlayer, spawnPos, delayBetweenProjectiles * i, speed);
            }
        }
    }

    public void SpawnProjectileStraight(Vector2 spawnPosition, Transform targetPlayer, float projectileAmount, float projectileDelay, float initialDelay = 0f, float speed = 0f)
    {
        // Never a whole file on one frame — see MinProjectileStagger
        if (projectileAmount > 1)
        {
            projectileDelay = Mathf.Max(MinProjectileStagger, projectileDelay);
        }

        StartCoroutine(SpawnProjectileStraightCoroutine(spawnPosition, targetPlayer, projectileAmount, projectileDelay, initialDelay, speed));
    }

    private IEnumerator SpawnProjectileStraightCoroutine(Vector2 spawnPosition, Transform targetPlayer, float projectileAmount, float projectileDelay, float initialDelay, float speed)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
        for (int i = 0; i < projectileAmount; i++)
        {
            SpawnProjectile(targetPlayer, spawnPosition, projectileDelay * i, speed);
        }
        yield return null;
    }

    public void SpawnOrb(Vector2 startPos, Vector2 endPos, bool isHealthOrb, float delay = 0f)
    {
        StartCoroutine(SpawnOrbAfterDelay(startPos, endPos, isHealthOrb, delay));
    }

    private IEnumerator SpawnOrbAfterDelay(Vector2 startPos, Vector2 endPos, bool isHealthOrb, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        GameObject orbPrefab = isHealthOrb ? healthOrbPrefab : manaOrbPrefab;
        GameObject orb = Instantiate(orbPrefab);
        CollectibleOrb collectibleOrb = orb.GetComponent<CollectibleOrb>();
        if (collectibleOrb != null)
        {
            collectibleOrb.Initialize(startPos, endPos);
        }
    }
}

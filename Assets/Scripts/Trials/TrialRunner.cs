using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays one Order trial in the gap between two waves, and owns everything that is
/// the same about all of them: the HUD comes down and the specials are held for the
/// length of it, an NPC says their piece first where the trial has one, the fanfare
/// that says how it went, and a clean board afterwards.
///
/// The trial itself — orbs, rocks, a ninja — is one of the bodies in
/// <see cref="OrbTrial"/>, <see cref="GuardTrial"/> and <see cref="NinjaTrial"/>. A body
/// runs until it has been passed or until <see cref="Lose"/> has been called, and
/// must hand everything it puts on the board to <see cref="Track"/> so a loss can
/// take it all off at once ("all the rocks despawn and the next wave begins").
///
/// Built the way QuestScene is: a code-created object, a static entry point and a
/// linear IEnumerator. The fight half runs on SCALED time so the pause menu stops a
/// trial the way it stops a wave; only the NPC's dialogue runs frozen.
///
/// The target range (<see cref="TargetRange"/>) plays through here too, through
/// <see cref="PlayTargets"/>: it is not an Order trial, but everything around the
/// test itself — the HUD, the held specials, the fanfare, the clean board — is the
/// same, so it shares <see cref="Open"/> and <see cref="Close"/> rather than
/// keeping a copy of them. Which of the two gets a gap is BetweenWaveEvents' call.
/// </summary>
public class TrialRunner : MonoBehaviour
{
    /// <summary>A trial is on the board. The Spawner's guard reads it.</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>An NPC is talking the players into a trial. Holds the pause menu off, exactly as a quest scene does.</summary>
    public static bool IsSpeaking { get; private set; }

    /// <summary>
    /// True while a fire, ice or venom trial is being played. PlayerShooter reads it
    /// and throws no shadow arrows and no shurikens — only the knight's own arrow
    /// (owner's call, 2026-09-25). An orb trial is a test of aim, and a fan of
    /// echoes sweeping the lane answers it for the player.
    /// </summary>
    public static bool EchoShotsHeld { get; private set; }

    /// <summary>How long the fanfare gets before the board moves on.</summary>
    private const float FanfareHoldSeconds = 1.6f;

    /// <summary>Between whatever lost the trial and the fanfare that says so.</summary>
    private const float LossBeatSeconds = 0.35f;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<KeyValuePair<PlayerSpecial, bool>> _heldSpecials =
        new List<KeyValuePair<PlayerSpecial, bool>>();

    public Spawner Spawner { get; private set; }
    public Transform LeftKnight { get; private set; }
    public Transform RightKnight { get; private set; }

    public bool Lost { get; private set; }

    /// <summary>The player walked out mid-trial (pause menu → camp). Everything just stops.</summary>
    public bool Abandoned =>
        GameSceneManager.Instance != null && GameSceneManager.Instance.IsTransitioningToCamp;

    /// <summary>Bodies poll this every frame and stop the moment it is true.</summary>
    public bool Stopped => Lost || Abandoned;

    public static IEnumerator Play(Spawner spawner, OrderTrials.Trial trial, int phase, bool practice)
    {
        if (spawner == null || trial == null) yield break;

        var runner = Create(spawner, "Order Trial");
        IsRunning = true;
        Debug.Log($"[Trials] {trial.Key} phase {phase + 1}/{trial.Phases}{(practice ? " (practice)" : "")}");
        yield return runner.Run(trial, phase, practice);
        Finish(runner);
    }

    /// <summary>
    /// Plays one target range pattern. <paramref name="practice"/> is Test Mode's:
    /// it plays and pays out exactly as the real one does, and records nothing.
    /// </summary>
    public static IEnumerator PlayTargets(Spawner spawner, TargetRange.Pattern pattern, bool practice)
    {
        if (spawner == null || pattern == null) yield break;

        var runner = Create(spawner, "Target Range");
        IsRunning = true;
        Debug.Log($"[Targets] pattern {pattern.Number}{(practice ? " (practice)" : "")}");
        yield return runner.RunTargets(pattern, practice);
        Finish(runner);
    }

    private static TrialRunner Create(Spawner spawner, string name)
    {
        var go = new GameObject(name);
        var runner = go.AddComponent<TrialRunner>();
        runner.Spawner = spawner;
        runner.LeftKnight = spawner.LeftPlayer;
        runner.RightKnight = spawner.RightPlayer;
        return runner;
    }

    private static void Finish(TrialRunner runner)
    {
        IsRunning = false;
        IsSpeaking = false;
        EchoShotsHeld = false;
        if (runner != null) Destroy(runner.gameObject);
    }

    private IEnumerator Run(OrderTrials.Trial trial, int phase, bool practice)
    {
        yield return Open();

        string line = IntroLine(trial, phase);
        if (!string.IsNullOrEmpty(line))
        {
            yield return Intro(trial, line);
        }

        if (!Abandoned)
        {
            EchoShotsHeld = IsOrbTrial(trial);
            yield return Body(trial, phase);
            EchoShotsHeld = false;
        }

        yield return Close($"[Trials] {trial.Key} phase {phase + 1}", () => PassOrderTrial(trial, practice));
    }

    private IEnumerator PassOrderTrial(OrderTrials.Trial trial, bool practice)
    {
        yield return new WaitForSeconds(FanfareHoldSeconds);
        if (!practice) OrderTrials.RecordPass(trial);
    }

    /// <summary>
    /// The target range. Nobody introduces it — the targets simply pop up — and the
    /// knights fire only their own arrows while it runs, as in the orb trials: it is
    /// a test of aim, and a fan of echoes sweeping the edge would answer it for them.
    /// A clear is paid for in orbs; see TargetRange.PayOut.
    /// </summary>
    private IEnumerator RunTargets(TargetRange.Pattern pattern, bool practice)
    {
        yield return Open();

        if (!Abandoned)
        {
            EchoShotsHeld = true;
            yield return TargetRange.Run(this, pattern);
            EchoShotsHeld = false;
        }

        yield return Close($"[Targets] pattern {pattern.Number}", () => TargetRange.PayOut(this, pattern, practice));
    }

    /// <summary>What every event here does before its test starts.</summary>
    private IEnumerator Open()
    {
        // Whatever the arena still has standing from the wave — carts still
        // circling, the castle's panes that would bend a trial's rocks into the
        // wrong knight, anything else the wave did not wait for.
        Spawner.ClearFixturesForTrial();

        // Specials are held for the length of it (owner's call): the trial measures
        // the knight, not the bar. The HUD goes with them, the same way it does for
        // a quest scene, so nobody sits there looking at a READY they cannot spend.
        HoldSpecials();
        yield return StartCoroutine(ArenaHud.FadeTo(0f));
    }

    /// <summary>
    /// What every event here does once its test is over: clear the board, play the
    /// fanfare that says how it went, and hand the arena back.
    /// <paramref name="onPassed"/> runs as the winning fanfare starts and the board
    /// waits for it — it is where a pass is recorded and whatever it pays is paid.
    /// </summary>
    private IEnumerator Close(string logLabel, Func<IEnumerator> onPassed)
    {
        ClearBoard();

        if (Abandoned)
        {
            ReleaseSpecials();
            ArenaHud.ShowImmediate();
            yield break;
        }

        bool won = !Lost;
        Debug.Log($"{logLabel}: {(won ? "PASSED" : "lost")}");

        // A loss lands on its own sound — the rock's hurt, the ninja's smoke — and
        // the womp-womp stepping on it the same frame turns both to mud.
        if (!won) yield return new WaitForSeconds(LossBeatSeconds);

        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(won ? audio.trialWon : audio.trialLost);

        if (won && onPassed != null) yield return onPassed();
        else yield return new WaitForSeconds(FanfareHoldSeconds);

        ReleaseSpecials();
        yield return StartCoroutine(ArenaHud.FadeTo(1f));
    }

    private static bool IsOrbTrial(OrderTrials.Trial trial)
    {
        return trial.Kind == OrderTrials.Kind.FireOrbs
            || trial.Kind == OrderTrials.Kind.IceOrbs
            || trial.Kind == OrderTrials.Kind.VenomOrbs;
    }

    private IEnumerator Body(OrderTrials.Trial trial, int phase)
    {
        switch (trial.Kind)
        {
            case OrderTrials.Kind.FireOrbs:     return OrbTrial.Fire(this, phase);
            case OrderTrials.Kind.IceOrbs:      return OrbTrial.Ice(this, phase);
            case OrderTrials.Kind.VenomOrbs:    return OrbTrial.Venom(this, phase);
            case OrderTrials.Kind.NinjaChase:   return NinjaTrial.Run(this, phase);
            case OrderTrials.Kind.GuardianBout: return GuardTrial.Guardian(this);
            case OrderTrials.Kind.DawnBout:     return GuardTrial.Dawn(this);
        }
        return null;
    }

    // ---------- what bodies call ----------

    /// <summary>The trial is lost. Idempotent — the second rock through does not lose it twice.</summary>
    public void Lose()
    {
        Lost = true;
    }

    /// <summary>Something the trial put on the board, to be taken off when it ends.</summary>
    public void Track(GameObject thing)
    {
        if (thing != null) _spawned.Add(thing);
    }

    /// <summary>
    /// Runs <paramref name="action"/> after <paramref name="seconds"/> of game time,
    /// unless the trial has stopped by then. How every body schedules its spawns:
    /// a loss has to stop what has not come out yet as well as clear what has.
    /// </summary>
    public void After(float seconds, Action action)
    {
        if (action == null) return;
        StartCoroutine(AfterRoutine(seconds, action));
    }

    private IEnumerator AfterRoutine(float seconds, Action action)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        if (!Stopped) action();
    }

    /// <summary>The knight on this side of the field.</summary>
    public Transform Knight(bool left) => left ? LeftKnight : RightKnight;

    // ---------- framing ----------

    /// <summary>
    /// What the NPC says as the trial opens, or null for a trial nobody introduces.
    /// Fire, ice and venom arrive unannounced — their Order explained them when it
    /// offered the initiation (owner's call). The Paladin speaks before both of
    /// his bouts, and the Ninja before every chase because he is the target.
    /// </summary>
    private static string IntroLine(OrderTrials.Trial trial, int phase)
    {
        switch (trial.Kind)
        {
            case OrderTrials.Kind.GuardianBout:
                return "Your ability as guardians will be challenged.\n"
                       + "If you show you can stand your ground without being hit a single time, "
                       + "we will accept you!\n"
                       + "Prepare yourselves!";
            case OrderTrials.Kind.DawnBout:
                return "Show the two of you can work together, and we will show you our healing powers.\n"
                       + "Do not get hit, even once.\n"
                       + "Brace yourselves!";
            case OrderTrials.Kind.NinjaChase:
                return NinjaTrial.IntroLine(phase);
        }
        return null;
    }

    private static NpcId Speaker(OrderTrials.Trial trial)
    {
        switch (trial.Kind)
        {
            case OrderTrials.Kind.GuardianBout:
            case OrderTrials.Kind.DawnBout:
                return NpcId.Paladin;
            case OrderTrials.Kind.NinjaChase:
                return NpcId.Ninja;
        }
        return NpcId.None;
    }

    /// <summary>
    /// A cut-down QuestScene: the NPC arrives, speaks, and leaves before anything is
    /// thrown. Frozen for the length of it, like every other conversation in the
    /// arena, and swept the same way — ArenaSweep also takes the knights' weapons
    /// off them while someone is talking, and Restore hands them back for the fight.
    /// </summary>
    private IEnumerator Intro(OrderTrials.Trial trial, string line)
    {
        var catalog = NpcCatalog.Instance;
        var npc = Speaker(trial);
        var entry = catalog != null ? catalog.Find(npc) : null;
        if (entry == null) yield break;

        IsSpeaking = true;
        Time.timeScale = 0f;
        ArenaSweep.Clear();

        var banner = FindFirstObjectByType<WaveName>(FindObjectsInactive.Include);
        var box = DialogueBox.Create(banner);
        var actor = NpcActor.Spawn(entry, trial.Order, NpcActor.Mark);
        if (actor != null) yield return actor.Enter(NpcActor.Mark);

        var pages = DialogueScript.Parse(line);
        for (int i = 0; i < pages.Count; i++)
        {
            yield return box.Speak(pages[i], npc, entry.voicePitch, null);
        }
        yield return box.Hide();

        if (actor != null) yield return actor.Leave();
        if (box != null) Destroy(box.gameObject);

        ArenaSweep.Restore();
        Time.timeScale = 1f;
        IsSpeaking = false;
    }

    private void HoldSpecials()
    {
        _heldSpecials.Clear();
        HoldSpecial(LeftKnight);
        HoldSpecial(RightKnight);
    }

    private void HoldSpecial(Transform knight)
    {
        var special = knight != null ? knight.GetComponent<PlayerSpecial>() : null;
        if (special == null) return;
        // Remembered rather than assumed true: the tutorial hands the special out
        // on its own schedule, and a trial must not finish that lesson early.
        _heldSpecials.Add(new KeyValuePair<PlayerSpecial, bool>(special, special.InputEnabled));
        special.InputEnabled = false;
    }

    private void ReleaseSpecials()
    {
        for (int i = 0; i < _heldSpecials.Count; i++)
        {
            if (_heldSpecials[i].Key != null) _heldSpecials[i].Key.InputEnabled = _heldSpecials[i].Value;
        }
        _heldSpecials.Clear();
    }

    private void ClearBoard()
    {
        // Nothing more comes out, whatever was still scheduled.
        StopAllCoroutines();
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null) Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }

    private void OnDestroy()
    {
        // A scene unloaded mid-trial must not leave the next run's knights without
        // their specials or the static flags up.
        ReleaseSpecials();
        IsRunning = false;
        IsSpeaking = false;
        EchoShotsHeld = false;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The target range (owner, 2026-09-26): after any wave from wave five on (odd or
/// even since 2026-10-13; it was odd waves only), in the forest only, red-and-white targets pop up round the edge of the arena. Shoot every one before the time runs out and the fanfare
/// plays, and a health orb crosses just above the knights and a mana orb just below
/// them, both inside a sword's reach.
///
/// A LOW-priority event: it only gets a gap nothing else wanted. A quest scene or an
/// Order trial that is going to play always has the gap instead — see
/// BetweenWaveEvents for the whole ladder.
///
/// It is rolled again (owner, 2026-10-13): one chance in four, and one in two once
/// four waves have gone by without it — see LowEventRoll. From 2026-09-26 until
/// then it had no roll and took every gap it was due in. It stays for the whole
/// file, quest or no quest.
///
/// Five patterns. Which one plays is never rolled: they go in batches of five,
/// each time the next pattern not yet cleared in the batch — see Next.
///
/// Clearing a pattern for the first time counts toward the Cartographer's "Target
/// Practice" quest, which is offered after the first clear of any pattern.
/// </summary>
public static class TargetRange
{
    /// <summary>The map it lives on. The range is the forest's; no other map has one.</summary>
    public const string MapId = QuestBuild.Forest;

    /// <summary>The first wave it can follow. From there, any wave.</summary>
    public const int FirstWave = 5;

    /// <summary>What <see cref="TestRunConfig.ForcedTrial"/> is set to for a forced pattern.</summary>
    public const string ForcedKey = "targets";

    // ---- progress ----

    /// <summary>
    /// How many DIFFERENT patterns have been cleared, 0 to 5. Target Practice reads
    /// it, measured over the whole file — the first clear is what opens the quest,
    /// so it has to count.
    /// </summary>
    public const string PatternsStat = "targets.patterns";

    /// <summary>Every clear, repeats included. For the Stats panel.</summary>
    public const string ClearedStat = "targets.cleared";

    /// <summary>
    /// Bookkeeping, not an achievement: which pattern was put up last (cleared or
    /// not), and a bit per pattern cleared in the current batch of five. Written
    /// with PlayerStats.Set and left out of StatsDatabase, like the trials' ".last" keys.
    /// </summary>
    private const string LastPlayedKey = "targets.last_played";
    private const string BatchKey = "targets.batch";

    /// <summary>
    /// True for the two keys that say where the range has got to in its batch. The
    /// tutorial run lets these be written (see TutorialStatGate): without them the
    /// range forgot what it had just put up and showed pattern 1 in every gap of
    /// that run (owner, 2026-10-13). The counters a quest reads are still held back.
    /// </summary>
    public static bool IsRotationKey(string key) => key == LastPlayedKey || key == BatchKey;

    /// <summary>1 once pattern <paramref name="number"/> has ever been cleared. Bookkeeping, as above.</summary>
    private static string PatternClearedKey(int number) => "targets.pattern." + number;

    // ---- the patterns ----

    /// <summary>One target: where it appears, and where it ends up when the time runs out.</summary>
    public struct Path
    {
        public Vector2 From;
        public Vector2 To;
        public Path(Vector2 from, Vector2 to) { From = from; To = to; }
        public static Path Still(Vector2 at) => new Path(at, at);
    }

    public sealed class Pattern
    {
        /// <summary>1-based, as the owner numbered them.</summary>
        public readonly int Number;
        /// <summary>Seconds to break them all, counted from the moment they can be hit.</summary>
        public readonly float Seconds;
        public readonly Path[] Targets;

        public Pattern(int number, float seconds, Path[] targets)
        {
            Number = number;
            Seconds = seconds;
            Targets = targets;
        }
    }

    // Target centres. The frame is x ±10, y ±5.625; a target is a unit across, so
    // these keep the whole disc on screen with a little air round it.
    private const float EdgeX = 9.2f;
    private const float EdgeY = 4.8f;

    private static readonly Vector2 TopLeft = new Vector2(-EdgeX, EdgeY);
    private static readonly Vector2 TopRight = new Vector2(EdgeX, EdgeY);
    private static readonly Vector2 BottomLeft = new Vector2(-EdgeX, -EdgeY);
    private static readonly Vector2 BottomRight = new Vector2(EdgeX, -EdgeY);

    /// <summary>
    /// The owner's five, in the owner's order (2026-09-26; times tightened the same
    /// day from 10 / 20 / 23 / 10 / 33).
    ///
    ///   1  one in each corner. 8 seconds.
    ///   2  five spread along the top without reaching the corners, five along the
    ///      bottom. 13.5 seconds (1.5 off, 2026-10-05).
    ///   3  one in each corner, three between them along the top and three along the
    ///      bottom. 12.5 seconds (2.5 off, 2026-10-13).
    ///   4  one in each corner, sliding to the next corner round, clockwise: top
    ///      right down to bottom right, bottom right across to bottom left, bottom
    ///      left up to top left, top left across to top right. They arrive as the
    ///      time runs out, so a shorter time is also a faster slide. 8 seconds.
    ///   5  twenty round the edge of the frame, one in each corner: seven along the
    ///      top, seven along the bottom, three down each side. 28 seconds.
    /// </summary>
    public static readonly Pattern[] Patterns =
    {
        new Pattern(1, 8f, Still(TopLeft, TopRight, BottomLeft, BottomRight)),
        new Pattern(2, 13.5f, Join(Row(EdgeY, -6f, -3f, 0f, 3f, 6f), Row(-EdgeY, -6f, -3f, 0f, 3f, 6f))),
        new Pattern(3, 12.5f, Join(Still(TopLeft, TopRight, BottomLeft, BottomRight),
                                 Row(EdgeY, -4.6f, 0f, 4.6f), Row(-EdgeY, -4.6f, 0f, 4.6f))),
        new Pattern(4, 8f, new[]
        {
            new Path(TopRight, BottomRight),
            new Path(BottomRight, BottomLeft),
            new Path(BottomLeft, TopLeft),
            new Path(TopLeft, TopRight),
        }),
        new Pattern(5, 28f, Border(7, 3)),
    };

    public static Pattern Find(int number)
    {
        for (int i = 0; i < Patterns.Length; i++)
        {
            if (Patterns[i].Number == number) return Patterns[i];
        }
        return null;
    }

    private static Path[] Still(params Vector2[] points)
    {
        var paths = new Path[points.Length];
        for (int i = 0; i < points.Length; i++) paths[i] = Path.Still(points[i]);
        return paths;
    }

    private static Path[] Row(float y, params float[] xs)
    {
        var paths = new Path[xs.Length];
        for (int i = 0; i < xs.Length; i++) paths[i] = Path.Still(new Vector2(xs[i], y));
        return paths;
    }

    private static Path[] Join(params Path[][] groups)
    {
        var all = new List<Path>();
        for (int i = 0; i < groups.Length; i++) all.AddRange(groups[i]);
        return all.ToArray();
    }

    /// <summary>
    /// Evenly round the frame: <paramref name="across"/> along the top and the
    /// bottom, corners included, and <paramref name="down"/> down each side between
    /// them.
    /// </summary>
    private static Path[] Border(int across, int down)
    {
        var all = new List<Path>();
        for (int i = 0; i < across; i++)
        {
            float x = Mathf.Lerp(-EdgeX, EdgeX, i / (float)(across - 1));
            all.Add(Path.Still(new Vector2(x, EdgeY)));
            all.Add(Path.Still(new Vector2(x, -EdgeY)));
        }
        for (int i = 1; i <= down; i++)
        {
            float y = Mathf.Lerp(-EdgeY, EdgeY, i / (float)(down + 1));
            all.Add(Path.Still(new Vector2(-EdgeX, y)));
            all.Add(Path.Still(new Vector2(EdgeX, y)));
        }
        return all.ToArray();
    }

    // ---- when it happens ----

    public static bool IsDueAfter(int wave)
    {
        return wave >= FirstWave;
    }

    /// <summary>
    /// Decides whether the gap after <paramref name="survivedWave"/> gets the range.
    /// The caller has already given every higher-priority event its chance and none
    /// of them wanted the gap — see BetweenWaveEvents.
    /// </summary>
    public static bool TryPick(int survivedWave, string mapId, out Pattern pattern)
    {
        pattern = null;

        if (mapId != MapId) return false;
        if (!IsDueAfter(survivedWave)) return false;

        if (Sprite == null)
        {
            Debug.LogWarning("[Targets] Target art not found in Resources/Trials — no target range.");
            return false;
        }

        if (!LowEventRoll.Roll(survivedWave, FirstWave, out int percent))
        {
            Debug.Log($"[Targets] after wave {survivedWave}: missed its roll ({percent}%)");
            return false;
        }

        pattern = Next();
        RecordPlayed(pattern);
        LowEventRoll.NotePlayed(survivedWave);
        Debug.Log($"[Targets] after wave {survivedWave}: pattern {pattern.Number} plays ({percent}% roll)");
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Test Mode's forced pattern: <see cref="TestRunConfig.ForcedTrial"/> set to
    /// <see cref="ForcedKey"/>, with the pattern number in ForcedTrialPhase. Only
    /// inside a Test Mode run — the setting is static and outlives the run.
    /// </summary>
    public static bool TryForced(out Pattern pattern)
    {
        pattern = null;
        if (!TestRunConfig.ActiveRun || TestRunConfig.ForcedTrial != ForcedKey) return false;
        pattern = Find(Mathf.Clamp(TestRunConfig.ForcedTrialPhase, 1, Patterns.Length));
        return pattern != null;
    }

    /// <summary>
    /// For Test Mode's event list: the pattern the range would put up in the gap
    /// after <paramref name="survivedWave"/>, or null when it is not due there. The
    /// roll is left out on purpose — the tester is the one choosing. Nothing is
    /// written; <see cref="PickByHand"/> does that.
    /// </summary>
    public static Pattern PeekDue(int survivedWave, string mapId)
    {
        if (mapId != MapId || !IsDueAfter(survivedWave) || Sprite == null) return null;
        return Next();
    }

    /// <summary>
    /// The tester picked the range for the gap. Recorded as put up, the way
    /// TryPick records it, without the roll.
    /// </summary>
    public static void PickByHand(Pattern pattern, int survivedWave)
    {
        RecordPlayed(pattern);
        LowEventRoll.NotePlayed(survivedWave);
        Debug.Log($"[Targets] after wave {survivedWave}: pattern {pattern.Number} plays (picked in Test Mode)");
    }
#endif

    /// <summary>
    /// Works through the patterns in batches of five (owner, 2026-10-05). From the one
    /// just played, the next pattern NOT yet cleared in this batch, wrapping from 5
    /// back to 1. Pass or fail, play always moves on: with 2 and 4 cleared, playing
    /// 3 leads to 5 whatever happens, and a failed 3 stays uncleared and comes round
    /// again. Once all five are cleared the batch resets and starts from 1.
    /// </summary>
    public static Pattern Next()
    {
        int mask = PlayerStats.Get(BatchKey);
        int last = Mathf.Clamp(PlayerStats.Get(LastPlayedKey), 0, Patterns.Length);

        for (int step = 1; step <= Patterns.Length; step++)
        {
            int index = (last - 1 + step + Patterns.Length) % Patterns.Length;
            if ((mask & (1 << index)) == 0) return Patterns[index];
        }
        return Patterns[0];
    }

    /// <summary>A pattern was put up, whether or not it gets cleared.</summary>
    private static void RecordPlayed(Pattern pattern)
    {
        PlayerStats.Set(LastPlayedKey, pattern.Number);
    }

    /// <summary>A pattern was cleared. Written between waves, so it lands at once.</summary>
    public static void RecordClear(Pattern pattern)
    {
        if (pattern == null) return;

        string key = PatternClearedKey(pattern.Number);
        if (PlayerStats.Get(key) == 0)
        {
            PlayerStats.Set(key, 1);
            // Last, so the quest that reads it sees the pattern already marked.
            PlayerStats.Increment(PatternsStat);
        }
        PlayerStats.Increment(ClearedStat);

        // The batch: this one is done until all five are, then it starts over at 1
        int full = (1 << Patterns.Length) - 1;
        int mask = PlayerStats.Get(BatchKey) | (1 << (pattern.Number - 1));
        if (mask == full)
        {
            mask = 0;
            PlayerStats.Set(LastPlayedKey, 0);
        }
        PlayerStats.Set(BatchKey, mask);
        PlayerStats.Flush();
    }

    // ---- the art ----

    private static Sprite _sprite;

    /// <summary>
    /// The bullseye (Assets/Resources/Trials/Target.aseprite), loaded from Resources
    /// because an .aseprite sprite reference cannot be written into a scene or an
    /// asset by hand — the same reason the venom orb's frames live there.
    /// </summary>
    public static Sprite Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;
            var loaded = Resources.LoadAll<Sprite>("Trials/Target");
            _sprite = loaded != null && loaded.Length > 0 ? loaded[0] : null;
            return _sprite;
        }
    }

    // ---- playing it ----

    /// <summary>Between the HUD going down and the first puff.</summary>
    private const float LeadInSeconds = 0.6f;

    /// <summary>The targets blink for this long before they go (owner, 2026-09-26).</summary>
    private const float WarningSeconds = 3f;

    /// <summary>
    /// The pattern itself, inside TrialRunner. Every target goes up at once; the
    /// clock starts when they can be hit. Runs until every target is broken (a pass)
    /// or the time runs out, which takes the rest off in smoke and loses.
    /// </summary>
    public static IEnumerator Run(TrialRunner runner, Pattern pattern)
    {
        var sprite = Sprite;
        if (sprite == null || pattern == null)
        {
            runner.Lose();
            yield break;
        }

        yield return new WaitForSeconds(LeadInSeconds);
        if (runner.Stopped) yield break;

        var targets = new List<RangeTarget>();
        for (int i = 0; i < pattern.Targets.Length; i++)
        {
            var path = pattern.Targets[i];
            var target = RangeTarget.Appear(sprite, path.From, path.To, pattern.Seconds);
            runner.Track(target.gameObject);
            targets.Add(target);
        }

        while (!runner.Stopped && !AllRevealed(targets)) yield return null;

        float deadline = Time.time + pattern.Seconds;
        bool warned = false;
        while (!runner.Stopped)
        {
            if (AllBroken(targets)) yield break;
            if (Time.time >= deadline) break;

            if (!warned && Time.time >= deadline - WarningSeconds)
            {
                warned = true;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null) targets[i].Warn();
                }
            }
            yield return null;
        }
        if (runner.Stopped) yield break;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && !targets[i].WasHit) targets[i].Vanish();
        }
        runner.Lose();
    }

    private static bool AllRevealed(List<RangeTarget> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t != null && !t.Revealed && !t.WasHit) return false;
        }
        return true;
    }

    private static bool AllBroken(List<RangeTarget> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            // A target is only ever destroyed by breaking or timing out, and the
            // time-out path never gets here — so a gone target was a broken one.
            if (t != null && !t.WasHit) return false;
        }
        return true;
    }

    // ---- the payout ----

    /// <summary>
    /// Above and below the knights, inside a sword's reach. The sword's point is
    /// about 1.4 from a knight, and these are the orb's middle.
    /// </summary>
    private const float OrbLaneOffset = 1.2f;

    /// <summary>Off the frame either side, as a wave's orbs are: they fly in, never appear.</summary>
    private const float OrbOffX = 12f;

    /// <summary>
    /// A clear. Runs as the fanfare starts: records it, sends the orbs — health
    /// left to right just above the knights, mana right to left just below, the
    /// way the Rat King pays his — brings the HUD back so the bars can be seen
    /// filling, and holds the gap until both orbs have been taken or have flown off.
    /// They are ordinary orbs and pay the ordinary amounts.
    /// </summary>
    public static IEnumerator PayOut(TrialRunner runner, Pattern pattern, bool practice)
    {
        if (!practice) RecordClear(pattern);
        yield return SendRewardOrbs(runner);
    }

    /// <summary>
    /// The two orbs on their own, without the bookkeeping. The mine's cart sorting
    /// pays the same way — see CartSorting.PayOut.
    /// </summary>
    public static IEnumerator SendRewardOrbs(TrialRunner runner)
    {
        var spawner = runner.Spawner;
        float y = runner.LeftKnight != null ? runner.LeftKnight.position.y
                : runner.RightKnight != null ? runner.RightKnight.position.y : 0f;

        GameObject health = SendOrb(spawner.healthOrbPrefab,
            new Vector2(-OrbOffX, y + OrbLaneOffset), new Vector2(OrbOffX, y + OrbLaneOffset));
        GameObject mana = SendOrb(spawner.manaOrbPrefab,
            new Vector2(OrbOffX, y - OrbLaneOffset), new Vector2(-OrbOffX, y - OrbLaneOffset));

        runner.StartCoroutine(ArenaHud.FadeTo(1f));

        while ((health != null || mana != null) && !runner.Abandoned) yield return null;
    }

    /// <summary>
    /// An ordinary orb along a lane given by its VISUAL centre: the prefab's pivot
    /// sits at the bottom of the art, and a lane authored at the transform would ride
    /// a quarter of a unit higher than it says (see TrialOrb.Spawn).
    /// </summary>
    private static GameObject SendOrb(GameObject prefab, Vector2 from, Vector2 to)
    {
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab);
        var orb = go.GetComponent<CollectibleOrb>();
        if (orb == null)
        {
            Object.Destroy(go);
            return null;
        }

        var collider = go.GetComponent<Collider2D>();
        Vector2 offset = collider != null ? (Vector2)go.transform.TransformVector(collider.offset) : Vector2.zero;
        orb.Initialize(from - offset, to - offset);
        return go;
    }
}

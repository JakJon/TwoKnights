using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Paladin's two trials, both forty-five seconds of fast rock and both lost the
/// moment a rock touches a knight (owner's spec, 2026-09-23). The rocks deal no
/// damage — a lost trial costs the trial, never the run.
///
/// GUARDIAN: one knight at a time. The rock switches knights every 7.5 seconds —
/// six turns, left first — and the knight whose turn it is wears the Guardian's
/// light and ring of shields, so nobody has to work out whose go it is.
///
/// DAWN: both knights at once, MIRRORED. Every rock at the left knight has a twin
/// at the right knight arriving at the same instant from the opposite direction:
/// left blocks up, right blocks down; left blocks out, right blocks out on its own
/// side. The whole pattern is turned 180° rather than flipped top-to-bottom, because
/// a rock "from the left" at the right knight would have to fly through the left
/// knight to get there.
///
/// Both are one fixed score, played the same way every attempt, so a failed bout
/// can be learned. The "chaotic" stretches are a golden-ratio walk round the dial —
/// they look random and are not.
///
/// Every score is written in ARRIVAL time and in the LEFT knight's frame: a shot is
/// "a rock reaches the knight from this direction at this second". The rock is
/// released early by exactly its own flight time, so two rocks written 0.12s apart
/// land 0.12s apart whatever distance each had to travel.
/// </summary>
public static class GuardTrial
{
    // ---- directions a rock can come FROM, in degrees, in the left knight's frame ----
    //
    // The left knight's legal dial runs from UpIn round through Out to DownIn. Never
    // straight in from the right: that is where the other knight is standing (wave
    // rule 2). The two inward diagonals are steep for the same reason — at 50° a rock
    // aimed at the left knight crosses the right knight's column some four units
    // above his head.
    private const float Out = 180f;
    private const float Up = 90f;
    private const float Down = 270f;
    private const float UpOut = 135f;
    private const float DownOut = 225f;
    private const float UpIn = 50f;
    private const float DownIn = 310f;

    /// <summary>
    /// Units a second. The waves throw at about 1.75; this is "very fast pace". From
    /// the top edge a rock is on screen for a little under a second before it lands.
    /// </summary>
    private const float RockSpeed = 4.5f;

    /// <summary>Rocks leave from this rectangle — wave rule 1's "out of frame", with room to spare.</summary>
    private const float SpawnHalfWidth = 12.5f;
    private const float SpawnHalfHeight = 7.5f;

    /// <summary>Where on the knight a rock is aimed, as ProjectileMovement aims it.</summary>
    private static readonly Vector2 AimOffset = new Vector2(0f, 0.5f);

    /// <summary>Six turns of 7.5s — the owner's 45 seconds, and 7.5s a turn.</summary>
    private const float TurnSeconds = 7.5f;
    /// <summary>First rock of a turn lands this long after the turn changes hands, so the light moves first.</summary>
    private const float TurnLeadIn = 1.6f;

    /// <summary>
    /// A moment of quiet before the first turn lights up. Also the runway every early
    /// rock needs: the longest flight in the rectangle is just under 2.9s (a rock
    /// from toward a top corner), and the first shot lands 1.6s into its turn, so
    /// 1.4s more lets even that one leave on time. Checked offline against every
    /// shot in both scores.
    /// </summary>
    private const float Runway = 1.4f;

    /// <summary>After the last rock lands, before the bout is called won.</summary>
    private const float Tail = 0.6f;

    private struct Shot
    {
        public float At;
        public float Deg;
        public Shot(float at, float deg) { At = at; Deg = deg; }
    }

    // ---------- Guardian ----------

    public static IEnumerator Guardian(TrialRunner runner)
    {
        var turns = GuardianScore();
        float lastArrival = 0f;

        for (int turn = 0; turn < turns.Count; turn++)
        {
            bool left = turn % 2 == 0;
            float turnStart = Runway + turn * TurnSeconds;
            Transform knight = runner.Knight(left);

            runner.After(turnStart, () => Markers.Guardian(runner, knight));

            foreach (var shot in turns[turn])
            {
                // The right knight's turns are the left knight's pattern in a
                // mirror, so "out" is his own outer side.
                float deg = left ? shot.Deg : 180f - shot.Deg;
                float arrival = turnStart + shot.At;
                Throw(runner, knight, deg, arrival);
                lastArrival = Mathf.Max(lastArrival, arrival);
            }
        }

        yield return WaitOut(runner, lastArrival + Tail);
        Markers.Clear();
    }

    // ---------- Dawn ----------

    public static IEnumerator Dawn(TrialRunner runner)
    {
        var score = DawnScore();
        float lastArrival = 0f;

        Markers.Dawn(runner);

        foreach (var shot in score)
        {
            float arrival = Runway + shot.At;
            Throw(runner, runner.LeftKnight, shot.Deg, arrival);
            // Turned half a circle: up becomes down, out becomes his own out.
            Throw(runner, runner.RightKnight, shot.Deg + 180f, arrival);
            lastArrival = Mathf.Max(lastArrival, arrival);
        }

        yield return WaitOut(runner, lastArrival + Tail);
        Markers.Clear();
    }

    private static IEnumerator WaitOut(TrialRunner runner, float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until && !runner.Stopped) yield return null;
        // A loss takes the markers off with everything else.
        if (runner.Stopped) Markers.Clear();
    }

    // ---------- throwing ----------

    private static void Throw(TrialRunner runner, Transform knight, float deg, float arrival)
    {
        if (knight == null) return;

        Vector2 centre = (Vector2)knight.position + AimOffset;
        float rad = deg * Mathf.Deg2Rad;
        Vector2 from = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        float distance = DistanceToSpawnEdge(centre, from);
        Vector2 spawn = centre + from * distance;
        float release = Mathf.Max(0f, arrival - distance / RockSpeed);

        runner.After(release, () => SpawnRock(runner, knight, spawn));
    }

    private static void SpawnRock(TrialRunner runner, Transform knight, Vector2 spawn)
    {
        var prefab = runner.Spawner.projectilePrefab;
        if (prefab == null) return;

        var rock = Object.Instantiate(prefab, spawn, Quaternion.identity);
        runner.Track(rock);

        var settings = rock.GetComponent<ProjectileSettings>();
        if (settings != null)
        {
            settings.MakeTrialRock(_ =>
            {
                if (runner.Stopped) return;
                var audio = AudioManager.Instance;
                if (audio != null) audio.PlaySFX(audio.playerHurt);
                runner.Lose();
            });
        }

        var mover = rock.GetComponent<ProjectileMovement>();
        if (mover != null) mover.Initialize(knight, spawn, RockSpeed);
    }

    /// <summary>How far along <paramref name="direction"/> from <paramref name="origin"/> the spawn rectangle's edge is.</summary>
    private static float DistanceToSpawnEdge(Vector2 origin, Vector2 direction)
    {
        float best = float.MaxValue;
        if (Mathf.Abs(direction.x) > 1e-4f)
        {
            float edge = direction.x > 0f ? SpawnHalfWidth : -SpawnHalfWidth;
            best = Mathf.Min(best, (edge - origin.x) / direction.x);
        }
        if (Mathf.Abs(direction.y) > 1e-4f)
        {
            float edge = direction.y > 0f ? SpawnHalfHeight : -SpawnHalfHeight;
            best = Mathf.Min(best, (edge - origin.y) / direction.y);
        }
        return best == float.MaxValue ? SpawnHalfWidth : best;
    }

    // ---------- the scores ----------

    /// <summary>
    /// Six turns, left, right, left... Each is written from 1.6s to about 7.1s into
    /// its own 7.5, so the knight coming off gets a couple of seconds' rest. Every
    /// turn has a character: the warm-up, the arcs, the up-up-down-down, the
    /// scramble, the mix, the finale.
    /// </summary>
    private static List<List<Shot>> GuardianScore()
    {
        var turns = new List<List<Shot>>();

        // 1 (left) — the warm-up: every direction once, half a second apart.
        turns.Add(new Score(TurnLeadIn)
            .Steps(0.5f, Out, Up, Down, Out, UpOut, DownOut, Up, Down, UpIn, DownIn, Out, Up)
            .Shots);

        // 2 (right) — arcs: sweep the dial one way and back, a flip, three to finish.
        turns.Add(new Score(TurnLeadIn)
            .Sweep(70f, 290f, 8, 0.15f).Rest(0.45f)
            .Sweep(290f, 70f, 8, 0.15f).Rest(0.45f)
            .Flip(Up, Down, 3, 0.12f, 0.3f).Rest(0.35f)
            .Steps(0.4f, Out, UpOut, DownOut)
            .Shots);

        // 3 (left) — up, up, down, down, left, right, left, right. Twice, the
        // second time faster. "Right" for the left knight is the steep inward
        // diagonal, above then below.
        turns.Add(new Score(TurnLeadIn)
            .Konami(0.38f).Rest(0.3f)
            .Konami(0.3f)
            .Shots);

        // 4 (right) — the scramble.
        turns.Add(new Score(TurnLeadIn)
            .Chaos(13, 0.45f, 0.11f)
            .Shots);

        // 5 (left) — a bit of everything.
        turns.Add(new Score(TurnLeadIn)
            .Zigzag(8, 0.28f).Rest(0.35f)
            .Sweep(290f, 70f, 9, 0.13f).Rest(0.35f)
            .Flip(Down, Up, 3, 0.12f, 0.25f).Rest(0.2f)
            .Steps(0.3f, Out)
            .Shots);

        // 6 (right) — the finale: a scramble, then the whole dial in one sweep.
        turns.Add(new Score(TurnLeadIn)
            .Chaos(8, 0.4f, 0.57f).Rest(0.25f)
            .Sweep(UpIn, DownIn, 13, 0.12f).Rest(0.2f)
            .Steps(0.12f, Out, Out, Out)
            .Shots);

        return turns;
    }

    /// <summary>
    /// One unbroken 45-second score, played at both knights at once. A touch slower
    /// than the Guardian's — there is no rest between turns when both are working —
    /// with a short breather between each figure.
    /// </summary>
    private static List<Shot> DawnScore()
    {
        const float breath = 0.6f;
        return new Score(TurnLeadIn)
            .Steps(0.55f, Up, Down, Out, Up, Down, Out, UpOut, DownOut).Rest(breath)
            .Sweep(70f, 290f, 8, 0.18f).Rest(breath)
            .Konami(0.42f).Rest(breath)
            .Chaos(10, 0.5f, 0.23f).Rest(breath)
            .Flip(Up, Down, 3, 0.14f, 0.35f).Rest(0.35f)
            .Flip(Down, Up, 3, 0.14f, 0.35f).Rest(breath)
            .Sweep(290f, 70f, 8, 0.16f).Rest(0.3f)
            .Sweep(70f, 290f, 8, 0.16f).Rest(breath)
            .Zigzag(8, 0.32f).Rest(breath)
            .Konami(0.34f).Rest(breath)
            .Chaos(12, 0.45f, 0.71f).Rest(breath)
            .Sweep(UpIn, DownIn, 12, 0.14f).Rest(0.5f)
            .Steps(0.3f, Up, Up, Down, Down, Out, Out).Rest(0.5f)
            .Flip(Up, Down, 3, 0.12f, 0.25f).Rest(0.4f)
            .Steps(0.35f, UpOut, DownOut, Out)
            .Shots;
    }

    /// <summary>
    /// Writes a score. <see cref="T"/> is always when the NEXT shot would land, so
    /// figures chain at their own cadence and <see cref="Rest"/> adds a pause on top.
    /// </summary>
    private sealed class Score
    {
        public readonly List<Shot> Shots = new List<Shot>();
        private float T;

        public Score(float start) { T = start; }

        public Score Rest(float seconds)
        {
            T += seconds;
            return this;
        }

        public Score Steps(float gap, params float[] directions)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Shots.Add(new Shot(T, directions[i]));
                T += gap;
            }
            return this;
        }

        /// <summary>Round the dial from one direction to another in <paramref name="count"/> even steps.</summary>
        public Score Sweep(float from, float to, int count, float gap)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                Shots.Add(new Shot(T, Mathf.Lerp(from, to, t)));
                T += gap;
            }
            return this;
        }

        /// <summary>A raked burst from one side, a beat, then the answer from the other.</summary>
        public Score Flip(float first, float second, int each, float gap, float between)
        {
            for (int i = 0; i < each; i++) { Shots.Add(new Shot(T, first)); T += gap; }
            T += between;
            for (int i = 0; i < each; i++) { Shots.Add(new Shot(T, second)); T += gap; }
            return this;
        }

        /// <summary>Up, up, down, down, out, in, out, in.</summary>
        public Score Konami(float gap)
        {
            return Steps(gap, Up, Up, Down, Down, Out, UpIn, Out, DownIn);
        }

        public Score Zigzag(int count, float gap)
        {
            for (int i = 0; i < count; i++)
            {
                Shots.Add(new Shot(T, i % 2 == 0 ? UpOut : DownOut));
                T += gap;
            }
            return this;
        }

        /// <summary>
        /// Looks random and is not: a golden-ratio stride round the legal dial, from a
        /// fixed starting point. Every consecutive pair is between 99° and 161°
        /// apart, so each rock asks for a real turn of the guard.
        /// </summary>
        public Score Chaos(int count, float gap, float start)
        {
            const float golden = 0.618034f;
            float span = DownIn - UpIn;
            float phase = start;
            for (int i = 0; i < count; i++)
            {
                Shots.Add(new Shot(T, UpIn + span * phase));
                T += gap;
                phase = Mathf.Repeat(phase + golden, 1f);
            }
            return this;
        }
    }

    // ---------- whose turn it is ----------

    /// <summary>
    /// The light on the knight being tested — the Paladin's own bloom, and for the
    /// Guardian his ring of shields turning over the knight's head. Moved on each
    /// change of turn; on both knights throughout for Dawn.
    /// </summary>
    private static class Markers
    {
        private static readonly List<NpcAura> Live = new List<NpcAura>();

        private static readonly Color GuardianSteel = new Color(0.62f, 0.66f, 0.70f);
        private static readonly Color GuardianOrbs = new Color(0.78f, 0.84f, 0.90f);
        private static readonly Color DawnGold = new Color(1f, 0.88f, 0.72f);

        public static void Guardian(TrialRunner runner, Transform knight)
        {
            Clear();
            if (knight == null) return;
            Add(knight, NpcAura.Bloom(knight.gameObject, GuardianSteel, 2.2f, 0), behind: true);
            Add(knight, NpcAura.Ring(knight.gameObject, GuardianOrbs, 4, 0.55f, 0.18f, 90f,
                                             height: 1.35f, sortingOrder: 0), behind: false);
        }

        public static void Dawn(TrialRunner runner)
        {
            Clear();
            foreach (var knight in new[] { runner.LeftKnight, runner.RightKnight })
            {
                if (knight == null) continue;
                Add(knight, NpcAura.Bloom(knight.gameObject, DawnGold, 2.2f, 0), behind: true);
            }
        }

        public static void Clear()
        {
            for (int i = 0; i < Live.Count; i++)
            {
                if (Live[i] != null) Live[i].Release();
            }
            Live.Clear();
        }

        /// <summary>
        /// NpcAura draws on the NPC's sorting band, far above the knights. Re-sorted
        /// onto the knight's own layer: the light just behind his body, the ring in
        /// front of everything he wears.
        /// </summary>
        private static void Add(Transform knight, NpcAura aura, bool behind)
        {
            if (aura == null) return;
            Live.Add(aura);

            var body = knight.GetComponent<SpriteRenderer>();
            if (body == null) body = knight.GetComponentInChildren<SpriteRenderer>();
            if (body == null) return;

            int order = body.sortingOrder;
            if (!behind)
            {
                foreach (var r in knight.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (r.sortingLayerID != body.sortingLayerID) continue;
                    if (r.GetComponentInParent<NpcAura>() != null) continue;
                    order = Mathf.Max(order, r.sortingOrder);
                }
            }

            foreach (var r in aura.GetComponentsInChildren<SpriteRenderer>())
            {
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = behind ? body.sortingOrder - 1 : order + 1;
            }
        }
    }
}

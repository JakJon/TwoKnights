using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The three orb trials: the Ember Order's fire orbs, the Frigid Order's ice orbs
/// and the Serpent Order's venom orbs. Every schedule here is fixed — the same
/// orbs, the same lanes, the same seconds every time a phase is played (see
/// OrderTrials for why).
///
/// Fire and ice (owner's spec, 2026-09-23): shoot every orb. The moment one leaves
/// the field unshot, the trial is lost. Fire orbs are health orbs at twice a health
/// orb's speed; ice orbs are mana orbs at half a mana orb's. Neither pays anything.
///
/// Venom: shoot every orb exactly ONCE. A bitten orb darkens and keeps flying, and
/// hitting it again loses — "after an enemy is poisoned you do not need to strike
/// it again". Each orb crosses the field three times (owner's call), so a player
/// is never down to one look at one; a fresh orb that finishes its third crossing
/// unbitten loses too.
/// </summary>
public static class OrbTrial
{
    // ---- lanes, in the orb's visual centre. The frame is x ±10, y ±5.625. ----

    /// <summary>Ice rides this close to the top and bottom edges — "near the edge of the view".</summary>
    private const float EdgeLaneY = 4.75f;
    /// <summary>
    /// Fire's top and bottom lanes: half a unit further in than ice's, nearer the
    /// knights (owner, 2026-09-26). Was the shared 4.75.
    /// </summary>
    private const float FireLaneY = EdgeLaneY - 0.5f;
    /// <summary>
    /// Fire's left and right lanes (phase III). Was 9.3, just inside the sides of the
    /// frame; brought 2.5 units in toward the knights (owner, 2026-09-26).
    /// </summary>
    private const float SideLaneX = 6.8f;
    /// <summary>Where a lane starts and ends: clear of the frame, so an orb flies in rather than appearing.</summary>
    private const float OffX = 11.5f;
    private const float OffY = 7f;

    // Venom's four lanes. Two out at the edges, and two in close over and under the
    // knights, which is where phase three's crossings happen: an orb on an inner lane
    // passes between a knight and an orb on the outer one, going the other way.
    private const float VenomOuterTop = 4.4f;
    private const float VenomOuterBottom = -4.4f;
    private const float VenomInnerTop = 2.9f;
    private const float VenomInnerBottom = -1.9f;

    /// <summary>
    /// Seconds between two venom orbs in the same lane — at a normal orb's pace,
    /// 1.8 units, a little under four orb-widths. Tight on purpose (owner, 2026-09-24):
    /// the line crosses as one snake, so a bitten orb always has fresh ones either
    /// side of it and the aim has to pick the right one. Was 1.2–1.8s.
    /// </summary>
    private const float VenomSpacingSeconds = 0.6f;

    /// <summary>A beat of empty field before the first orb, so a trial does not open mid-thought.</summary>
    private const float LeadInSeconds = 0.8f;

    // Ice's top and bottom edge lanes.
    private static TrialOrb.Pass Top => new TrialOrb.Pass(new Vector2(-OffX, EdgeLaneY), new Vector2(OffX, EdgeLaneY));
    private static TrialOrb.Pass Bottom => new TrialOrb.Pass(new Vector2(OffX, -EdgeLaneY), new Vector2(-OffX, -EdgeLaneY));

    // Fire runs clockwise round the frame: along the top left to right, down the
    // right, back along the bottom, up the left.
    private static TrialOrb.Pass FireTop => new TrialOrb.Pass(new Vector2(-OffX, FireLaneY), new Vector2(OffX, FireLaneY));
    private static TrialOrb.Pass FireBottom => new TrialOrb.Pass(new Vector2(OffX, -FireLaneY), new Vector2(-OffX, -FireLaneY));
    private static TrialOrb.Pass Left => new TrialOrb.Pass(new Vector2(-SideLaneX, -OffY), new Vector2(-SideLaneX, OffY));
    private static TrialOrb.Pass Right => new TrialOrb.Pass(new Vector2(SideLaneX, OffY), new Vector2(SideLaneX, -OffY));

    private static TrialOrb.Pass Across(float y, bool rightward)
    {
        return rightward
            ? new TrialOrb.Pass(new Vector2(-OffX, y), new Vector2(OffX, y))
            : new TrialOrb.Pass(new Vector2(OffX, y), new Vector2(-OffX, y));
    }

    private struct Release
    {
        public float At;
        public TrialOrb.Pass[] Passes;
        public Release(float at, params TrialOrb.Pass[] passes) { At = at; Passes = passes; }
    }

    // ---------- fire ----------

    /// <summary>
    /// Seconds between the two fire orbs of a pair. Fire orbs cover four units a
    /// second, so the second trails the first by four units — two separate targets,
    /// both on screen together along the top and bottom.
    /// </summary>
    private const float FirePairGapSeconds = 1f;

    /// <summary>
    /// The top and bottom lanes send a PAIR of fire orbs (owner, 2026-09-25: double
    /// the orbs). I: a pair along the top. II: top, then bottom five seconds later.
    /// III: top, bottom, left, right, five seconds apart. The side lanes are the
    /// short ones — an orb is on screen for under three seconds going down the side
    /// against five going across — which is what makes III the hard one, so they
    /// send ONE orb each, not a pair (owner, 2026-09-25).
    /// </summary>
    public static IEnumerator Fire(TrialRunner runner, int phase)
    {
        var prefab = runner.Spawner.healthOrbPrefab;
        float speed = BaseSpeed(prefab, 2f) * 2f;

        TrialOrb.Pass[] lanes;
        switch (phase)
        {
            case 0: lanes = new[] { FireTop }; break;
            case 1: lanes = new[] { FireTop, FireBottom }; break;
            default: lanes = new[] { FireTop, FireBottom, Left, Right }; break;
        }

        var schedule = new List<Release>();
        for (int i = 0; i < lanes.Length; i++)
        {
            float at = i * 5f;
            schedule.Add(new Release(at, lanes[i]));
            bool sideLane = Mathf.Approximately(lanes[i].From.x, lanes[i].To.x);
            if (!sideLane) schedule.Add(new Release(at + FirePairGapSeconds, lanes[i]));
        }
        return ShootThemAll(runner, prefab, TrialOrb.Look.Fire, speed, schedule.ToArray());
    }

    // ---------- ice ----------

    /// <summary>
    /// Eight, twelve, then sixteen orbs (owner, 2026-09-25: doubled from four, six
    /// and eight). I and II release a second apart; III two seconds apart; and
    /// halfway through there is a five-second breather.
    ///
    /// Every orb comes in from a DIFFERENT corner from the one before it (owner,
    /// 2026-09-25). They used to run as a line along the top and then a line along
    /// the bottom, which at this pace packed them into a tight bunch the player
    /// cleared by holding one aim. The corners go top-left, bottom-right, top-right,
    /// bottom-left and round again — each orb diagonally across the field from the
    /// last — and every orb still runs along the top or bottom edge from its corner.
    /// A fixed order, never rolled, like every other schedule here.
    /// </summary>
    public static IEnumerator Ice(TrialRunner runner, int phase)
    {
        var prefab = runner.Spawner.manaOrbPrefab;
        float speed = BaseSpeed(prefab, 3f) * 0.5f;

        int perHalf = (phase + 2) * 2;           // 4, 6, 8
        float spacing = phase >= 2 ? 2f : 1f;    // "orbs two seconds apart" in III
        const float wait = 5f;                   // between the two halves

        TrialOrb.Pass[] corners =
        {
            Top,                              // from the top-left, heading right
            Bottom,                           // from the bottom-right, heading left
            Across(EdgeLaneY, false),         // from the top-right, heading left
            Across(-EdgeLaneY, true),         // from the bottom-left, heading right
        };

        var schedule = new List<Release>();
        float secondHalf = (perHalf - 1) * spacing + wait;
        for (int i = 0; i < perHalf * 2; i++)
        {
            float at = i < perHalf ? i * spacing : secondHalf + (i - perHalf) * spacing;
            schedule.Add(new Release(at, corners[i % corners.Length]));
        }

        return ShootThemAll(runner, prefab, TrialOrb.Look.Ice, speed, schedule.ToArray());
    }

    private static IEnumerator ShootThemAll(TrialRunner runner, GameObject prefab, TrialOrb.Look look,
                                            float speed, Release[] schedule)
    {
        int outstanding = schedule.Length;
        for (int i = 0; i < schedule.Length; i++)
        {
            var release = schedule[i];
            runner.After(LeadInSeconds + release.At, () =>
            {
                var orb = TrialOrb.Spawn(prefab, look, speed, release.Passes);
                if (orb == null) { outstanding--; return; }
                runner.Track(orb.gameObject);
                orb.Struck += _ => outstanding--;
                orb.Escaped += _ => runner.Lose();
                FlyBy();
            });
        }

        while (outstanding > 0 && !runner.Stopped) yield return null;
    }

    // ---------- venom ----------

    /// <summary>
    /// I: three orbs in a line — top, back along the bottom, top again.
    /// II: five, top and bottom lanes running opposite ways.
    /// III: seven, and the lanes cross. Four run the outer lanes while three run the
    /// inner ones the other way, so a bitten orb keeps sliding across the line
    /// between a knight and a fresh one. Holding fire until it has cleared is the test.
    /// </summary>
    public static IEnumerator Venom(TrialRunner runner, int phase)
    {
        var prefab = runner.Spawner.manaOrbPrefab;
        // A normal orb's pace. It is the three crossings that give a player time,
        // not a slow orb.
        float speed = BaseSpeed(prefab, 3f);

        var outerA = new[] { Across(VenomOuterTop, true), Across(VenomOuterBottom, false), Across(VenomOuterTop, true) };
        var outerB = new[] { Across(VenomOuterBottom, false), Across(VenomOuterTop, true), Across(VenomOuterBottom, false) };
        var inner = new[] { Across(VenomInnerTop, false), Across(VenomInnerBottom, true), Across(VenomInnerTop, false) };

        // Orbs sharing a lane ride in a tight line, VenomSpacingSeconds apart; a
        // second lane's orbs leave on the half-beats between them.
        const float s = VenomSpacingSeconds;
        const float h = VenomSpacingSeconds * 0.5f;

        Release[] schedule;
        switch (phase)
        {
            case 0:
                schedule = new[]
                {
                    new Release(0f, outerA), new Release(s, outerA), new Release(2f * s, outerA),
                };
                break;
            case 1:
                schedule = new[]
                {
                    new Release(0f, outerA), new Release(h, outerB), new Release(s, outerA),
                    new Release(s + h, outerB), new Release(2f * s, outerA),
                };
                break;
            default:
                schedule = new[]
                {
                    new Release(0f, outerA), new Release(h, inner), new Release(s, outerA),
                    new Release(s + h, inner), new Release(2f * s, outerA), new Release(2f * s + h, inner),
                    new Release(3f * s, outerA),
                };
                break;
        }

        int unbitten = schedule.Length;
        for (int i = 0; i < schedule.Length; i++)
        {
            var release = schedule[i];
            runner.After(LeadInSeconds + release.At, () =>
            {
                var orb = TrialOrb.Spawn(prefab, TrialOrb.Look.Venom, speed, release.Passes);
                if (orb == null) { unbitten--; return; }
                runner.Track(orb.gameObject);
                orb.Struck += _ => unbitten--;
                orb.StruckAgain += _ => runner.Lose();
                orb.Escaped += _ => runner.Lose();
                FlyBy();
            });
        }

        while (unbitten > 0 && !runner.Stopped) yield return null;
    }

    // ---------- shared ----------

    private static float BaseSpeed(GameObject orbPrefab, float fallback)
    {
        var orb = orbPrefab != null ? orbPrefab.GetComponent<CollectibleOrb>() : null;
        return orb != null && orb.MoveSpeed > 0f ? orb.MoveSpeed : fallback;
    }

    private static float _lastFlyBy = -10f;

    /// <summary>
    /// The ordinary orb's whoosh, rate-limited. The ice trial releases an orb a
    /// second, and a whoosh per orb stacks into one continuous hiss.
    /// </summary>
    private static void FlyBy()
    {
        if (Time.time - _lastFlyBy < 1.5f) return;
        _lastFlyBy = Time.time;
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(audio.orbFlyBy);
    }
}

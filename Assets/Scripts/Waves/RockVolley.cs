using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The shape one volley of rock arrives in.</summary>
public enum VolleyShape
{
    Single, // one rock, one knight
    Pair,   // two down the same shaft — the second lands while the first is still being answered
    Split,  // both knights at once. Nobody is shooting anything during this one
    Flip,   // same knight, one side then the other: the guard has to cross the whole dial
    Fan,    // a spread that walks outward around the knight's own half

    // The three below exist because the forest's deep tiers had run out of ways
    // to be harder that were not just "more rock, sooner". Everything above asks
    // for ONE facing held, or one crossing made; these ask for two facings at
    // once, or for a crossing on every single shot.
    //
    // APPENDED, NEVER REORDERED. VolleyShape is serialised into the wave assets
    // as an integer (`shape: 3` is Flip), so inserting a value here silently
    // rewrites every volley already authored in the Mine and the forest.
    Mirror,   // both knights at once, from OPPOSITE sides. Split, but the two shields end up facing apart
    Scissors, // Mirror, then both knights flip - two dials crossing at the same moment, in opposite directions
    Weave,    // one knight, N shots, each arriving on the far side of the dial from the last

    // Spirals (owner, 2026-09-25: "the projectile patterns are too simple, make
    // them spirals"). Every shot leaves from the same radius a little further
    // round the dial than the last, and every shot is in the air for the same
    // time, so while a volley is flying the rocks in the air literally form a
    // spiral arm closing on the knight. The guard follows it round like a clock
    // hand: small steps, one direction, rather than Weave's crossing on every shot.
    Spiral,     // one knight: arrivals walk steadily round his own side of the dial
    TwinSpiral  // both knights at once, mirror images - the two guards turn in opposite directions
}

/// <summary>
/// Rock down the shafts, in a named shape, at a named knight.
///
/// This started life inside Delivery and is out here because the mine needed it
/// everywhere: of twelve mine waves only three threw a rock at all, so nine of
/// them asked the players for nothing but arrows and position, and the guard —
/// half of what a knight IS — sat idle through them. Bodies are not a substitute
/// for that. A wave with more mobs asks the same question louder; a wave with
/// rock in it asks a second question at the same time, which is the one the
/// two-knight geometry is actually about.
///
/// Every shape is authored so a volley aimed at one knight can never cross the
/// middle and be eaten by the other's shield — see Fan, which is the only one
/// that could and is written to sweep outward.
/// </summary>
[System.Serializable]
public class RockVolley
{
    [Tooltip("Authoring only")]
    public string label;

    public VolleyShape shape = VolleyShape.Single;

    [Tooltip("Which knight. Split takes both and ignores this.")]
    public bool rightKnight;

    [Tooltip("Mirror the volley to come up from underneath. Flip uses both sides whatever this says, but it still sets which one it OPENS on — false is over then under, true is under then over.")]
    public bool fromBelow;

    [Tooltip("Seconds between shots inside the volley — used by Pair, Flip, Fan, Weave and the spirals. Because flight time is identical for every shot, this is also the gap between their ARRIVALS.")]
    public float spacing = 1f;

    [Tooltip("Fan: how many rocks in the spread. Weave: how many shots. Spiral and TwinSpiral: shots per pass - raised automatically if the sweep would need steps wider than 45 degrees.")]
    public int count = 3;

    [Tooltip("Fan only: degrees it sweeps, from straight overhead outward to the knight's own side. Hard-capped at 80 — past that the spread reaches round to the other knight's half and gets absorbed by the wrong guard.")]
    public float fanDegrees = 60f;

    [Tooltip("Weave only: how wide the crossing band is, in degrees either side of the knight's outward direction. 90 is the full legal half-circle and the house value; lower it to keep the crossings shallow. Never goes above 90 - past that the band reaches over the middle and the far end of it is answered by the WRONG knight's shield.")]
    public float weaveDegrees = 90f;

    [Tooltip("Spiral and TwinSpiral: how far round the dial one pass walks, in degrees. It starts 30 degrees past straight up toward the other knight (straight DOWN with fromBelow) and walks outward round the knight's own side. 240 is the whole legal sweep, top to bottom, and the cap: past it the far end reaches the band where a shot could be eaten by the partner's guard. 120 keeps it to one quarter plus the side, which is how a wave says 'from underneath' with a spiral.")]
    public float spiralDegrees = 180f;

    [Tooltip("Spiral and TwinSpiral: how many times it crosses the sweep. 1 walks it once. 2 walks it and comes back the same way, 3 goes out again. The turn at each end repeats no shot.")]
    public int spiralPasses = 1;

    [Tooltip("Rocks released TOGETHER on every shot of this volley. 1 is one rock. This is the Mine's tier dial for rock: the shape stays the shape and the windows stay the windows, but a late-tier window has to be answered with the shield actually covering it rather than nearly covering it. House ladder: 1 / 3 / 5 / 8 across a wave's four tiers.")]
    public int salvo = 1;

    [Tooltip("World units between neighbouring rocks of a salvo, measured ACROSS the line of flight. They still all converge on the knight, so one shield facing answers the whole salvo — see Salvo for why that has to stay true.")]
    public float salvoSpread = 0.45f;

    [Tooltip("Seconds between one rock of a salvo and the next. NEVER 0: rock released on the same frame arrives on the same frame, and a wall that lands all at once is not a thing a guard can be asked to block, it is damage with a picture on it. Flight time is identical for every rock of a salvo, so this is equally the gap between their ARRIVALS — a salvo rakes across the knight rather than slapping him. AUTHOR IT WIDE ON THE LOW TIERS: a second or two apart is a drip the guard sits through and a chance to look at something else between rocks, and a tenth is a wall. Mind the cost on Fan and Pair, which pay the whole rake once per shot of the shape — a two-second rake on a five-rock salvo is eight seconds of one volley, which on a Fan of four is most of a firing window.")]
    public float salvoStagger = 0.1f;

    [Tooltip("THE FIRING WINDOW: quiet after this volley's last shot. This is the knob that keeps a wave fair — it is the time the players get to do everything else the wave is asking.")]
    public float restAfter = 3.5f;

    /// <summary>
    /// Cycles <paramref name="volleys"/> in order for <paramref name="window"/>
    /// seconds, then stops issuing. Whatever is already in the air still falls.
    /// Nothing is rolled: the same wave throws the same rock in the same order
    /// every time it is fought.
    /// </summary>
    public static IEnumerator WorkTheShafts(Spawner spawner, List<RockVolley> volleys,
                                            float window, float rockSpeed)
    {
        if (volleys == null || volleys.Count == 0) yield break;

        float until = Time.time + Mathf.Max(0f, window);
        int index = 0;

        while (Time.time < until)
        {
            RockVolley volley = volleys[index % volleys.Count];
            index++;
            if (volley == null) continue;

            float busy = Fire(spawner, volley, rockSpeed);
            yield return new WaitForSeconds(busy + Mathf.Max(0f, volley.restAfter));
        }
    }

    /// <summary>
    /// Fire one volley <paramref name="delay"/> seconds from now.
    ///
    /// For waves that schedule their whole body up front instead of yielding
    /// through it — Bat Cauldron lays every ring down on one frame with the
    /// spacing carried as per-spawn delays, so there is no later moment at which
    /// to call Fire. Nothing is returned: a caller that is scheduling rather than
    /// pacing has nothing to do with a busy time.
    /// </summary>
    public static void FireAfter(Spawner spawner, RockVolley volley, float rockSpeed, float delay)
    {
        if (volley == null) return;
        if (delay <= 0f) { Fire(spawner, volley, rockSpeed); return; }
        spawner.StartCoroutine(FireAfterRoutine(spawner, volley, rockSpeed, delay));
    }

    private static IEnumerator FireAfterRoutine(Spawner spawner, RockVolley volley,
                                                float rockSpeed, float delay)
    {
        yield return new WaitForSeconds(delay);
        Fire(spawner, volley, rockSpeed);
    }

    /// <summary>
    /// Sends one volley and returns how long it stays busy — the time from its
    /// first shot to its last, so a caller can rest AFTER the volley rather than
    /// during it.
    /// </summary>
    public static float Fire(Spawner spawner, RockVolley volley, float rockSpeed)
    {
        Transform knight = volley.rightKnight ? spawner.RightPlayer : spawner.LeftPlayer;
        Vector2 above = volley.rightKnight ? spawner.aboveRightPlayer : spawner.aboveLeftPlayer;
        Vector2 below = volley.rightKnight ? spawner.belowRightPlayer : spawner.belowLeftPlayer;
        Vector2 from = volley.fromBelow ? below : above;
        float spacing = Mathf.Max(0.05f, volley.spacing);

        // A salvo is not instantaneous, so every gap in a volley has to be measured
        // from the END of the wall before it rather than from its start. `beat` is
        // therefore the whole unit a shape is built out of: the wall goes out, and
        // THEN the authored window runs. Measured the other way, a tier that raised
        // the salvo count would quietly eat its own windows — eight rocks at a
        // tenth apiece is 0.7s of rock, so a 0.1s flip would have had the answer
        // from above starting while the shots from below were still leaving.
        float salvoBusy = SalvoSeconds(volley);
        float beat = salvoBusy + spacing;

        switch (volley.shape)
        {
            case VolleyShape.Pair:
                Salvo(spawner, volley, knight, from, 0f, rockSpeed);
                Salvo(spawner, volley, knight, from, beat, rockSpeed);
                return beat + salvoBusy;

            case VolleyShape.Split:
            {
                // A frame and a half between the two halves. They are aimed at
                // DIFFERENT knights, so each player still only ever answers one
                // rock at a time either way — but nothing in this game puts two
                // projectiles on one frame, and a shape that did would be the one
                // exception a future author copied.
                //
                // Deliberately NOT half the salvo stagger, which it used to be.
                // Split means both knights at once, and a wave that authors a
                // two-second rake would have quietly turned it into a wave that
                // hits one player a second before the other — a different shape
                // wearing the Split name. The nudge is a constant so it stays a
                // nudge whatever the rake is doing.
                const float half = MinStagger * 0.5f;
                Salvo(spawner, volley, spawner.LeftPlayer,
                    volley.fromBelow ? spawner.belowLeftPlayer : spawner.aboveLeftPlayer, 0f, rockSpeed);
                Salvo(spawner, volley, spawner.RightPlayer,
                    volley.fromBelow ? spawner.belowRightPlayer : spawner.aboveRightPlayer, half, rockSpeed);
                return salvoBusy + half;
            }

            case VolleyShape.Flip:
            {
                // Opens on whichever side `from` resolved to and answers with the
                // other, so a flip can be authored to come up from underneath
                // first — the half of the dial a guard watching the shafts is
                // least likely to be sitting on.
                //
                // `spacing` here is the real window: the last rock of the first
                // wall has left before the clock on it starts. The Mine's late
                // tiers set it to a tenth of a second, which is a fair ask because
                // the guard turns instantly (ShieldOrbit) and the volley order is
                // fixed, so the player is already moving when the wall lands.
                Vector2 second = volley.fromBelow ? above : below;
                Salvo(spawner, volley, knight, from, 0f, rockSpeed);
                Salvo(spawner, volley, knight, second, beat, rockSpeed);
                return beat + salvoBusy;
            }

            case VolleyShape.Fan:
            {
                int count = Mathf.Max(2, volley.count);

                // Outward, into the knight's own half. Which way that is depends on
                // BOTH which knight it is and which side of him the fan starts on —
                // sweeping the wrong way walks the spread across the middle of the
                // board and into the other knight's shield.
                var direction = (volley.rightKnight ^ volley.fromBelow)
                    ? Spawner.ArcDirection.Clockwise
                    : Spawner.ArcDirection.CounterClockwise;

                // A fan is already a walk around the dial, so a salvo on it is a
                // walk in THICKER steps rather than a second spread on top: each
                // position of the sweep goes out as a wall instead of a rock.
                float radius = Vector2.Distance(from, ArcCentreFor(spawner, knight));
                float step = Mathf.Clamp(volley.fanDegrees, 10f, 80f) / (count - 1);
                if (direction == Spawner.ArcDirection.Clockwise) step = -step;

                Vector2 centre = ArcCentreFor(spawner, knight);
                float startAngle = Mathf.Atan2(from.y - centre.y, from.x - centre.x) * Mathf.Rad2Deg;

                for (int i = 0; i < count; i++)
                {
                    float angle = (startAngle + step * i) * Mathf.Deg2Rad;
                    Vector2 at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Salvo(spawner, volley, knight, at, beat * i, rockSpeed);
                }

                return beat * (count - 1) + salvoBusy;
            }

            case VolleyShape.Mirror:
            {
                // Split's problem: it resolves `fromBelow` once and hands BOTH
                // knights the same side, so both shields end up pointing the same
                // way and the player answers two rocks with one decision made
                // twice. Mirror resolves the side per knight and inverts it, so the
                // two guards finish the volley facing apart. Same geometry and the
                // same safety argument - each leg runs straight down its own
                // knight's x and never goes near the partner - for a genuinely
                // different ask.
                //
                // `fromBelow` names the LEFT knight's side; the right knight always
                // gets the other one.
                const float half = MinStagger * 0.5f;
                Salvo(spawner, volley, spawner.LeftPlayer,
                    volley.fromBelow ? spawner.belowLeftPlayer : spawner.aboveLeftPlayer, 0f, rockSpeed);
                Salvo(spawner, volley, spawner.RightPlayer,
                    volley.fromBelow ? spawner.aboveRightPlayer : spawner.belowRightPlayer, half, rockSpeed);
                return salvoBusy + half;
            }

            case VolleyShape.Scissors:
            {
                // Mirror answered by its own inverse: both knights cross the dial
                // on the same beat, in opposite directions. The deepest thing the
                // two-knight geometry can be asked for, and still fair - each
                // knight only ever answers one rock at a time, and the order never
                // changes, so it is a rehearsal problem rather than a reaction one.
                const float half = MinStagger * 0.5f;

                Vector2 leftOpen  = volley.fromBelow ? spawner.belowLeftPlayer  : spawner.aboveLeftPlayer;
                Vector2 leftShut  = volley.fromBelow ? spawner.aboveLeftPlayer  : spawner.belowLeftPlayer;
                Vector2 rightOpen = volley.fromBelow ? spawner.aboveRightPlayer : spawner.belowRightPlayer;
                Vector2 rightShut = volley.fromBelow ? spawner.belowRightPlayer : spawner.aboveRightPlayer;

                Salvo(spawner, volley, spawner.LeftPlayer,  leftOpen,  0f,          rockSpeed);
                Salvo(spawner, volley, spawner.RightPlayer, rightOpen, half,        rockSpeed);
                Salvo(spawner, volley, spawner.LeftPlayer,  leftShut,  beat,        rockSpeed);
                Salvo(spawner, volley, spawner.RightPlayer, rightShut, beat + half, rockSpeed);
                return beat + salvoBusy + half;
            }

            case VolleyShape.Weave:
            {
                // One knight, and every shot lands on the far side of the dial from
                // the one before it. This is Suppressing Fire's crossing pattern
                // (SuppressingFire.PatternFor, tier 5) made available to any wave:
                // a stream whose difficulty is WHERE the next one comes from rather
                // than how fast they come.
                //
                // Written as a closed form over the shot index and NOT as a walk,
                // which is the lesson that file paid for. Its first version stepped
                // the angle on each shot, two of those rules were contractions, and
                // after about ten shots every rock was leaving from the same spot -
                // the pattern quietly decayed into the straight stream it existed
                // to replace. Nothing here reads the previous shot, so nothing here
                // can converge.
                int shots = Mathf.Max(2, volley.count);

                // The sign alternates on every shot, so consecutive arrivals ALWAYS
                // cross the middle of the band; the magnitude shrinks strictly, so
                // the crossings start at the full width of the band and close in.
                //
                // Strictly shrinking is what makes the shape safe, and it was the
                // second attempt. The first ran the magnitude out to the edge and
                // back - close in, then open out again - which reads beautifully and
                // is degenerate: with the sign flipping every shot and the magnitude
                // symmetric about the middle, the back half of an odd-length weave
                // replays the front half shot for shot. A five-shot weave came out
                // -90, +45, 0, +45, -90, which is three distinct angles wearing five
                // rocks. A strictly decreasing magnitude cannot do that at any count.
                float band = Mathf.Clamp(volley.weaveDegrees, 10f, MaxWeaveDegrees);
                float weaveBeat = salvoBusy + Mathf.Max(MinWeaveGap, spacing);

                for (int i = 0; i < shots; i++)
                {
                    float magnitude = (float)(shots - i) / shots;
                    float sign = (i % 2 == 0) ? -1f : 1f;
                    // fromBelow opens the weave underneath instead of on top
                    if (volley.fromBelow) sign = -sign;

                    Salvo(spawner, volley, knight,
                          WeavePoint(spawner, knight, sign * magnitude * band),
                          weaveBeat * i, rockSpeed);
                }

                return weaveBeat * (shots - 1) + salvoBusy;
            }

            case VolleyShape.Spiral:
            {
                int perPass = SpiralShotsPerPass(volley);
                int shots = SpiralShots(perPass, volley.spiralPasses);
                float spiralBeat = salvoBusy + Mathf.Max(MinSpiralGap, spacing);

                for (int i = 0; i < shots; i++)
                {
                    Salvo(spawner, volley, knight,
                          SpiralPoint(spawner, knight, SpiralDegreesAt(volley, perPass, i), volley.fromBelow),
                          spiralBeat * i, rockSpeed);
                }

                return spiralBeat * (shots - 1) + salvoBusy;
            }

            case VolleyShape.TwinSpiral:
            {
                // Both knights on the same beat, both opening on the same side, so
                // the two arms are mirror images across the middle of the board and
                // the two guards turn in opposite directions. Each knight still only
                // ever answers one rock at a time. The right knight's arm is nudged
                // a frame behind for the same reason Split's is.
                const float half = MinStagger * 0.5f;
                int perPass = SpiralShotsPerPass(volley);
                int shots = SpiralShots(perPass, volley.spiralPasses);
                float spiralBeat = salvoBusy + Mathf.Max(MinSpiralGap, spacing);

                for (int i = 0; i < shots; i++)
                {
                    float degrees = SpiralDegreesAt(volley, perPass, i);
                    Salvo(spawner, volley, spawner.LeftPlayer,
                          SpiralPoint(spawner, spawner.LeftPlayer, degrees, volley.fromBelow),
                          spiralBeat * i, rockSpeed);
                    Salvo(spawner, volley, spawner.RightPlayer,
                          SpiralPoint(spawner, spawner.RightPlayer, degrees, volley.fromBelow),
                          spiralBeat * i + half, rockSpeed);
                }

                return spiralBeat * (shots - 1) + salvoBusy + half;
            }

            default:
                Salvo(spawner, volley, knight, from, 0f, rockSpeed);
                return salvoBusy;
        }
    }

    // The band a Weave is allowed to cross, measured either side of the knight's
    // outward direction. At 90 it is the whole half-circle on that knight's own
    // side of the board, which is the widest span where no shot can be absorbed by
    // the partner - the same argument SuppressingFire.MaxOffset makes.
    private const float MaxWeaveDegrees = 90f;

    // Seconds between the ARRIVALS of a weave, floored (owner, 2026-09-15, for
    // Suppressing Fire; the same number here for the same reason). A weave is a
    // reading problem: the work is turning to meet a shot that came from the
    // opposite side of the dial, and under about a second that stops being a turn
    // anyone can read and becomes a toll. Flip is allowed a tenth of a second
    // because it is ONE crossing off a shape already seen; a weave is a crossing on
    // every shot, for the length of the volley.
    //
    // A wave converting a straight stream into a weave therefore has to THIN it:
    // eleven rocks at 0.45s is five seconds of stream and nineteen seconds of
    // weave. Half the rocks per knight, twice the knights, four times the turns.
    private const float MinWeaveGap = 1.2f;

    // Where one shot of a weave leaves from. Built on the same arc centre and the
    // same radius as the belt in PinnedWave, for the identical reason: every shot
    // of the volley sits at ONE radius, so they arrive in the order they left with
    // the gaps they were authored with.
    //
    // THIRTEEN, and the exact number matters - this is not a round-up of the 12 the
    // pin belt uses. Two separate things rule it:
    //
    // Seven, what above/belowPlayer sit at, is far too short: the band sweeps
    // through the knight's outward horizontal, and at radius 7 that point is
    // (-9, 0), a rock appearing in open frame nine units from the middle of the
    // board. Rule 1 forbids it outright.
    //
    // Twelve is not enough either, which is less obvious and was caught by sweeping
    // the band rather than by looking at it. Off the left knight's centre a shot at
    // offset o lands at x = -2 - R*cos(o), y = R*sin(o). Clearing the frame needs
    // |x| >= 12 OR |y| >= 7, and at R = 12 the first fails past |o| = 33.6 degrees
    // while the second does not hold until 35.7 - a two-degree window where a rock
    // appears just inside the corner of the screen. R = 12.25 closes it exactly;
    // 13 closes it with about half a unit to spare at the tightest angle.
    //
    // The pin belt gets away with 12 because it only ever uses offsets 0 and +/-90,
    // and Suppressing Fire because its authored angles happen to step over the
    // window. A weave generates its angles, so it has to clear the whole band.
    private const float WeaveRadius = 13f;

    private static Vector2 WeavePoint(Spawner spawner, Transform knight, float offsetDegrees)
    {
        Vector2 centre = ArcCentreFor(spawner, knight);
        // Outward = away from the middle of the board: 0 degrees for the right
        // knight, 180 for the left.
        float outward = (knight == spawner.LeftPlayer) ? 180f : 0f;
        float radians = (outward + offsetDegrees) * Mathf.Deg2Rad;
        return centre + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * WeaveRadius;
    }

    // How far round a spiral may walk. It starts 30 degrees past straight up
    // toward the partner and ends 30 degrees past straight down, 240 in all, so
    // the steepest shot on the partner's side still comes in 60 degrees off the
    // horizontal: from the left knight's point of view that rock crosses x = 2
    // (the right knight) about seven units up, far clear of his guard. Rule 2.
    //
    // It also stays inside the out-of-frame arc at WeaveRadius: off the left
    // knight's centre every angle from 32.6 to 327.4 degrees clears the frame at
    // radius 13, and the spiral uses 60 to 300.
    private const float MaxSpiralDegrees = 240f;
    private const float SpiralOvershoot = 30f;

    // The biggest step one shot may take round the dial. The stock guard covers
    // sixty degrees, so a step of 45 always overlaps the facing before it: the
    // spiral asks for a steady turn, never for a crossing. A sweep with too few
    // shots to honour that gets more shots rather than bigger steps.
    private const float MaxSpiralStep = 45f;

    // Seconds between the arrivals of a spiral, floored. Much lower than a
    // weave's 1.2s because each shot is a small turn in a direction the player
    // already knows, not a crossing.
    private const float MinSpiralGap = 0.3f;

    private static int SpiralShotsPerPass(RockVolley volley)
    {
        float sweep = Mathf.Clamp(volley.spiralDegrees, 30f, MaxSpiralDegrees);
        int needed = Mathf.CeilToInt(sweep / MaxSpiralStep) + 1;
        return Mathf.Max(2, Mathf.Max(volley.count, needed));
    }

    // A pass after the first shares its opening shot with the end of the pass
    // before it, so it adds one shot fewer.
    private static int SpiralShots(int perPass, int passes)
    {
        return perPass + (Mathf.Max(1, passes) - 1) * (perPass - 1);
    }

    // Degrees along the sweep for shot i. A CLOSED FORM over the shot index, for
    // the reason Weave gives: nothing here reads the previous shot, so nothing
    // here can drift. Even passes walk out from the start, odd passes walk back.
    private static float SpiralDegreesAt(RockVolley volley, int perPass, int shot)
    {
        float sweep = Mathf.Clamp(volley.spiralDegrees, 30f, MaxSpiralDegrees);
        float step = sweep / (perPass - 1);

        int along;
        if (shot < perPass)
        {
            along = shot;
        }
        else
        {
            int beyond = shot - perPass;
            int pass = 1 + beyond / (perPass - 1);
            int into = beyond % (perPass - 1);
            along = (pass % 2 == 1) ? perPass - 2 - into : into + 1;
        }

        return along * step;
    }

    // Where a spiral shot leaves from: `degrees` along the sweep from its start.
    // The start is SpiralOvershoot past straight up toward the partner (straight
    // down with fromBelow), and the sweep walks away from the partner, round the
    // knight's own side. Built on WeavePoint, so the offsets are measured from
    // the knight's outward direction and every shot sits at WeaveRadius.
    private static Vector2 SpiralPoint(Spawner spawner, Transform knight, float degrees, bool fromBelow)
    {
        // Offsets are counter-clockwise from outward. For the left knight straight
        // up is -90 and the top end of the sweep is -120; for the right knight both
        // signs flip.
        float side = (knight == spawner.LeftPlayer) ? -1f : 1f;
        float start = 90f + SpiralOvershoot;

        float offset = fromBelow
            ? -side * start + side * degrees
            : side * start - side * degrees;

        return WeavePoint(spawner, knight, offset);
    }

    /// <summary>
    /// How long ONE wall takes to leave, first rock to last. Every gap a shape
    /// authors is stacked on top of this rather than overlapping it.
    /// </summary>
    private static float SalvoSeconds(RockVolley volley)
    {
        int count = Mathf.Max(1, volley.salvo);
        return (count - 1) * Mathf.Max(MinStagger, volley.salvoStagger);
    }

    // The floor on salvoStagger, and it is a floor rather than a default because
    // 0 is authorable and 0 is the one value that must never reach the field.
    // Rock released on the same frame arrives on the same frame, and a guard has
    // no answer to that however well it is aimed.
    private const float MinStagger = 0.05f;

    /// <summary>
    /// One SHOT of a volley: <c>salvo</c> rocks released on the same beat from the
    /// same stretch of off-frame, fanned a little across the line of flight so they
    /// read as a wall of stone coming down a shaft rather than as one rock.
    ///
    /// The rocks are STAGGERED, one every <c>salvoStagger</c> seconds, and that is
    /// not a garnish — it is the difference between a wall and a tax. Flight time
    /// is identical for every rock of a salvo, so the gap between their releases is
    /// exactly the gap between their arrivals: a salvo rakes across the knight over
    /// most of a second, which a guard can be asked to hold through. Fired on one
    /// frame they would land on one frame, and no facing, aim or reaction answers
    /// that. Never author the stagger to 0; the floor here refuses it anyway.
    ///
    /// They are all aimed at the same knight, so they CONVERGE, and that is the
    /// rest of the safety argument: eight rocks 0.45 units apart at seven units out
    /// arrive inside about 25 degrees of each other, and the stock shield covers
    /// sixty (a one-unit bar on a one-unit orbit). One correct facing therefore
    /// still answers the entire salvo. A salvo is the tier saying "cover it
    /// properly", never "you cannot cover it" — spread it wide enough that the
    /// ends fall outside the bar and the wave stops being a wave and starts being
    /// a toll.
    ///
    /// Spread ACROSS the flight line rather than around the knight for the same
    /// reason the Fan is capped at 80 degrees: a spread that walks round the dial
    /// eventually reaches the other knight's half and gets eaten by the wrong guard.
    /// </summary>
    private static void Salvo(Spawner spawner, RockVolley volley, Transform knight,
                              Vector2 from, float delay, float rockSpeed)
    {
        int count = Mathf.Max(1, volley.salvo);

        if (count == 1 || knight == null)
        {
            spawner.SpawnProjectile(knight, from, delay, rockSpeed);
            return;
        }

        Vector2 along = ((Vector2)knight.position + AimOffset) - from;
        Vector2 across = along.sqrMagnitude > 0.0001f
            ? new Vector2(-along.y, along.x).normalized
            : Vector2.right;

        float step = Mathf.Max(0.05f, volley.salvoSpread);
        float first = -(count - 1) * 0.5f * step;
        float stagger = Mathf.Max(MinStagger, volley.salvoStagger);

        // Released in order along the line, so the wall RAKES across the knight
        // from one end to the other rather than arriving as a block. That order is
        // what makes the stagger readable: the player can see which way it is
        // sweeping and hold the facing through it.
        for (int i = 0; i < count; i++)
        {
            spawner.SpawnProjectile(knight, from + across * (first + i * step),
                                    delay + i * stagger, rockSpeed);
        }
    }

    // Rocks are aimed half a unit above the knight's transform
    // (ProjectileMovement.FaceTarget). Measuring the flight line from the same
    // point is what keeps a salvo's spread square to the line it actually flies.
    private static readonly Vector2 AimOffset = new Vector2(0f, 0.5f);

    // The point a Fan sweeps around, matching Spawner.ArcCenterFor. Duplicated
    // rather than exposed because it is one line and a public accessor on the
    // Spawner would invite waves to do their own arc geometry.
    private static Vector2 ArcCentreFor(Spawner spawner, Transform knight)
    {
        return (knight == spawner.LeftPlayer) ? new Vector2(-2f, 0f) : new Vector2(2f, 0f);
    }
}

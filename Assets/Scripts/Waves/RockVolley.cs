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
    Fan     // a spread that walks outward around the knight's own half
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

    [Tooltip("Seconds between shots inside the volley — used by Pair, Flip and Fan. Because flight time is identical for every shot, this is also the gap between their ARRIVALS.")]
    public float spacing = 1f;

    [Tooltip("Fan only: how many rocks in the spread")]
    public int count = 3;

    [Tooltip("Fan only: degrees it sweeps, from straight overhead outward to the knight's own side. Hard-capped at 80 — past that the spread reaches round to the other knight's half and gets absorbed by the wrong guard.")]
    public float fanDegrees = 60f;

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

            default:
                Salvo(spawner, volley, knight, from, 0f, rockSpeed);
                return salvoBusy;
        }
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

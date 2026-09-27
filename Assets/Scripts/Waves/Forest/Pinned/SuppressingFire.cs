using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "SuppressingFire", menuName = "Waves/Suppressing Fire")]
public class SuppressingFire : PinnedWave
{
    // Pattern half of the pinning family (see PinnedWave). The pinned knight holds
    // the belt; the FREE knight faces a CONTINUOUS stream whose arrival direction
    // walks steadily across their half of the arena.
    //
    // Discrete volleys with gaps between them left the free knight idle between
    // reads, which made the wave feel empty and left the pinned knight blocking a
    // belt for nothing. Instead the stream never stops: one shot every interval, its
    // angle advancing a fixed number of degrees each time, sweeping to the edge of
    // the legal band and turning back. The free knight's stick is therefore always
    // moving - slowly at tier 1, briskly by tier 4 - and never parked.
    //
    // THE RULE THAT SHAPES ALL OF IT: every shot sits at the same radius, so two
    // spawned together ARRIVE together, from different angles, on one shield - which
    // is unblockable, not hard. One shot per interval keeps arrivals strictly
    // sequential and the sweep readable.
    //
    // LEGAL BAND: offsets are measured from the free knight's outward direction (180
    // degrees for the left knight, 0 for the right) and stay within +/-90. That
    // half-circle at radius 12 lands entirely outside the x+/-12, y+/-7 field, and no
    // shot on it can cross the knight's partner.

    [Tooltip("1-6. Selects the authored sweep. Each tier is a DIFFERENT shape, not the " +
             "same sweep turned up: see NextOffset. Numbers alone stopped reading as new " +
             "waves somewhere around tier three, because a faster metronome is still a metronome.")]
    [SerializeField] private int tier = 1;
    [Tooltip("Radius of every shot around the free knight. 12 matches the pin's flight time")]
    [SerializeField] private float arcRadius = 12f;
    [Tooltip("Seconds between shots in the stream. Floored at 1.2s: this wave is a " +
             "reading problem, not a density one, and anything tighter turns a complex pattern " +
             "into a wall with no time to answer it.")]
    [SerializeField] private float secondsBetweenShots = 1f;
    [Tooltip("Seconds the stream runs. Match the pin length so neither knight ever idles")]
    [SerializeField] private float patternSeconds = 38f;
    [Tooltip("Bats sent at the free knight across the wave, for kill economy")]
    [SerializeField] private int batCount = 3;

    [Header("Company - all of it enters from the pinned knight's edge")]
    [Tooltip("Rats that scurry in from the far edge and take up station on the free knight's " +
             "side. They ENTER from the right of the screen but do not patrol across the pinned " +
             "knight, who cannot answer anything while holding the belt.")]
    [SerializeField] private int ratCount = 0;
    [Tooltip("Slimes sent at the free knight. They cross high and low rather than down the " +
             "middle, so their line never runs through the pinned knight.")]
    [SerializeField] private int slimeCount = 0;
    [Tooltip("Size of those slimes, 1-3")]
    [SerializeField] private int slimeSize = 1;

    // Past this the spawn point rotates into frame, or into the partner's lane
    private const float MaxOffset = 90f;

    // 1.2 seconds between arrivals, always (owner, 2026-09-15). The difficulty
    // here is WHERE the next one comes from, never how fast they come, so the gap
    // is a floor rather than a dial: the deepest tier sits on it and nothing is
    // allowed under it.
    private const float MinShotGap = 1.2f;

    protected override IEnumerator SpawnFreeKnightContent(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float interval = Mathf.Max(MinShotGap, secondsBetweenShots);
        float span = Mathf.Max(1f, patternSeconds);

        // The whole stream is scheduled up front: no dice, no runtime decisions, so
        // the sweep plays out identically every run (design rule 6)
        float[] pattern = PatternFor(tier);

        for (int shot = 0; ; shot++)
        {
            float t = shot * interval;
            if (t >= span) break;
            spawner.SpawnProjectile(knight, this.ArcStart(knight, pattern[shot % pattern.Length]), t);
        }

        // Bats spread through the stream. Each one forces the free knight to break
        // off the sweep, shoot, and pick the sweep back up
        for (int b = 0; b < batCount; b++)
        {
            float spread = batCount > 1 ? (float)b / (batCount - 1) : 0.5f;
            float delay = Mathf.Lerp(span * 0.15f, span * 0.8f, spread);
            bool high = b % 2 == 0;
            spawner.SpawnBat(new Vector2(this.BatSpawnX(high ? 11f : 12f), high ? 5.5f : -5.5f), delay);
        }

        // Rats come in off the same edge the bats do, then take station on the FREE
        // knight's own side. They must not patrol across the pinned knight: a rat
        // pacing over someone who is holding an angle is damage with no answer,
        // which is the one thing the shield rules forbid outright.
        for (int r = 0; r < ratCount; r++)
        {
            float spread = ratCount > 1 ? (float)r / (ratCount - 1) : 0.5f;
            float delay = Mathf.Lerp(span * 0.2f, span * 0.75f, spread);
            float y = r % 2 == 0 ? 3f : -3f;
            spawner.SpawnRat(new Vector2(FreeSideSign * 7f, y), delay, knight,
                             entryPoint: new Vector2(this.BatSpawnX(13f), y));
        }

        // Slimes cross high and low rather than down the middle, for the same
        // reason: a straight line from the edge to the free knight at y = 0 runs
        // directly over the pinned one.
        for (int s = 0; s < slimeCount; s++)
        {
            float spread = slimeCount > 1 ? (float)s / (slimeCount - 1) : 0.5f;
            float delay = Mathf.Lerp(span * 0.25f, span * 0.7f, spread);
            float y = s % 2 == 0 ? 5.5f : -5.5f;
            spawner.SpawnSlime(Mathf.Clamp(slimeSize, 1, 3),
                               new Vector2(this.BatSpawnX(12f), y), delay, knight);
        }

        yield return new WaitForSeconds(span);
    }

    // ---- The sweep, one shape per tier ----

    /// <summary>
    /// Where each shot of the stream comes from, in degrees off the free knight's
    /// outward direction, cycled in order.
    ///
    /// AUTHORED AS A LIST, NOT DERIVED FROM A RULE, and that is the whole point.
    /// The first version of this walked the angle with a little formula per tier,
    /// and two of those formulas were contractions: "step on, then flip and shrink"
    /// has an attracting fixed point, so after about ten shots every rock was
    /// leaving from the same spot and the wave degenerated into one straight stream
    /// from a single angle. A list cannot do that. It also cannot drift, cannot
    /// depend on the interval, and can be read off the page.
    ///
    /// Every entry stays inside the +/-90 band, so no shot spawns in frame or
    /// crosses the pinned knight. The tiers get harder by how FAR consecutive
    /// entries sit apart - a neighbouring angle is a nudge of the stick, the
    /// opposite edge is a full crossing - and by how much the order defeats a
    /// guess about where the next one goes.
    /// </summary>
    private static float[] PatternFor(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 6))
        {
            // Tier 1 - a clean sweep out and back. This is where the shape is learned.
            case 1: return new float[] { -90, -60, -30, 0, 30, 60, 90, 60, 30, 0, -30, -60 };

            // Tier 2 - the same sweep, but it stutters backwards twice on the way
            // out, so arriving early is punished and the sweep has to be watched.
            case 2: return new float[] { -90, -60, -30, -60, 0, 30, 0, 60, 90, 60, 30, -30 };

            // Tier 3 - the CUT. It runs to the far edge and reappears at the near
            // one instead of turning, so the band is crossed rather than reversed.
            case 3: return new float[] { -90, -45, 0, 45, 90, -90, -45, 0, 45, 90, 0, -45 };

            // Tier 4 - near and far alternating: every shot is answered on the
            // opposite half of the band from the last, at shrinking distances.
            case 4: return new float[] { -90, 0, -60, 30, -30, 60, 0, 90, 30, -90, 60, -60 };

            // Tier 5 - full crossings. Edge to edge repeatedly, with the pairs
            // closing in, then opening back out.
            case 5: return new float[] { -90, 90, -45, 45, 0, -90, 60, -60, 30, -30, 90, 0, -45, 45 };

            // Tier 6 - crossings with baited repeats: the same angle twice in a row
            // is the tell that the next one is the far edge.
            default: return new float[] { -90, 90, 90, -45, 45, -90, 0, 0, 90, -60, 60, -90, 30, -30, 45, 90 };
        }
    }

    // Spawner.ArcCenterFor puts the arc centre at (+/-2, 0), so the ring is built on
    // the knight's x and y = 0 to match it exactly.
    private Vector2 ArcStart(Transform knight, float offset)
    {
        float outwardDeg = FreeSideSign > 0f ? 0f : 180f;
        float radians = (outwardDeg + Mathf.Clamp(offset, -MaxOffset, MaxOffset)) * Mathf.Deg2Rad;
        return new Vector2(knight.position.x, 0f)
             + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * arcRadius;
    }
}


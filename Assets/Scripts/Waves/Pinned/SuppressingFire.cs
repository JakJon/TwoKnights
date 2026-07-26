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

    [Tooltip("1-4. Selects the authored pattern progression")]
    [SerializeField] private int tier = 1;
    [Tooltip("Radius of every shot around the free knight. 12 matches the pin's flight time")]
    [SerializeField] private float arcRadius = 12f;
    [Tooltip("How fast the incoming direction walks across the band. The core difficulty dial")]
    [SerializeField] private float wiperDegreesPerSecond = 12f;
    [Tooltip("Seconds between shots in the stream. Lower = denser wall")]
    [SerializeField] private float secondsBetweenShots = 1f;
    [Tooltip("Double back after this many shots (0 = a pure, predictable sweep)")]
    [SerializeField] private int reverseEveryNShots = 0;
    [Tooltip("Seconds the stream runs. Match the pin length so neither knight ever idles")]
    [SerializeField] private float patternSeconds = 38f;
    [Tooltip("Bats sent at the free knight across the wave, for kill economy")]
    [SerializeField] private int batCount = 3;

    // Past this the spawn point rotates into frame, or into the partner's lane
    private const float MaxOffset = 90f;

    protected override IEnumerator SpawnFreeKnightContent(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float interval = Mathf.Max(0.15f, secondsBetweenShots);
        float span = Mathf.Max(1f, patternSeconds);

        // The whole stream is scheduled up front: no dice, no runtime decisions, so
        // the sweep plays out identically every run (design rule 6)
        float offset = -MaxOffset;
        float direction = 1f;
        int shot = 0;

        for (float t = 0f; t < span; t += interval)
        {
            spawner.SpawnProjectile(knight, this.ArcStart(knight, offset), t);

            offset += direction * wiperDegreesPerSecond * interval;
            if (offset >= MaxOffset) { offset = MaxOffset; direction = -1f; }
            else if (offset <= -MaxOffset) { offset = -MaxOffset; direction = 1f; }

            shot++;
            // Deterministic double-back: breaks the metronome at higher tiers so the
            // sweep has to be read rather than predicted
            if (reverseEveryNShots > 0 && shot % reverseEveryNShots == 0)
            {
                direction = -direction;
            }
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

        yield return new WaitForSeconds(span);
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


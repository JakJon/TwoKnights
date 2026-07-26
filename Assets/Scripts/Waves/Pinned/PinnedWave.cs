using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Where a pin stream originates, relative to the knight it is pinning. All three
// sit on that knight's own axes, so a rock never travels near the other knight.
public enum PinAnchor { Side, Top, Bottom }

// Shared machinery for the "pinning" family: one knight is held down for the whole
// wave by a steady stream of rocks from a single off-screen anchor, while the other
// knight carries the actual challenge. Two properties make the pin fair, and both
// are load-bearing:
//
// 1. UNIFORM FLIGHT TIME. Every anchor sits at the same radius from the pinned
//    knight's AIM POINT (transform + 0.5y, the spot ProjectileMovement steers at),
//    so every rock takes exactly the same time to land. When the anchor moves, the
//    old stream's tail and the new stream's head can never arrive together - the
//    handover is a clean baton pass. Mixed radii would put two arrival directions
//    on one shield at once, which is simply unblockable.
//
// 2. ROCKS CAN ONLY BE BLOCKED. ProjectileSettings reacts to the Shield and player
//    tags and nothing else, so neither knight can shoot the stream down. The pin is
//    escaped by holding an angle, never by damage - which is the whole point.
//
// At the default 0.5s cadence the knight is pinned in earnest: the gap is shorter
// than the 1.5s shot cooldown, so leaving the angle always costs a hit. Blocking is
// combo-safe and pays +1 special per rock, so a clean pin past 55 rocks earns the
// x2 multiplier - the pinned knight's reward for holding the line.
public abstract class PinnedWave : BaseWave
{
    [Header("Pin - who and where")]
    [Tooltip("The knight held down by the rock stream. The OTHER knight carries the wave")]
    [SerializeField] protected KnightTarget pinnedKnight = KnightTarget.LeftKnight;
    [Tooltip("Anchor order. One entry = the stream never moves; every extra entry is one reposition")]
    [SerializeField] private List<PinAnchor> anchorSequence = new List<PinAnchor> { PinAnchor.Side };
    [Tooltip("Seconds each anchor holds before the stream jumps to the next one")]
    [SerializeField] private float anchorHoldSeconds = 20f;
    [Tooltip("Extra seconds of silence across a reposition. 0 = the belt never breaks")]
    [SerializeField] private float switchGapSeconds = 0f;

    [Header("Pin - tempo")]
    [Tooltip("Seconds between rocks. 0.5 pins outright - shorter than the 1.5s shot cooldown")]
    [SerializeField] private float secondsBetweenRocks = 0.5f;
    [Tooltip("Insert a breather after this many rocks (0 = unbroken stream)")]
    [SerializeField] private int pauseEveryNRocks = 0;
    [Tooltip("Length of that breather, when pauseEveryNRocks is set")]
    [SerializeField] private float pauseSeconds = 0f;

    [Header("Pin - geometry")]
    [Tooltip("Aim point to anchor. 12 = a 12s flight at rock speed 1, and puts all three anchors off-screen")]
    [SerializeField] private float anchorRadius = 12f;

    // ProjectileMovement aims at target.position + (0, 0.5). Centring the anchor ring
    // on THAT point rather than the knight's feet is what keeps Side/Top/Bottom flight
    // times identical - off the feet, Side would run 0.5s long and smear a handover.
    private const float AimYOffset = 0.5f;

    protected Transform PinnedTransform(Spawner spawner)
    {
        return pinnedKnight == KnightTarget.LeftKnight ? spawner.LeftPlayer : spawner.RightPlayer;
    }

    protected Transform FreeTransform(Spawner spawner)
    {
        return pinnedKnight == KnightTarget.LeftKnight ? spawner.RightPlayer : spawner.LeftPlayer;
    }

    // +1 when the free knight is the right one. Free-knight mobs spawn on this side
    // so they never trek across the pinned knight on their way in.
    protected float FreeSideSign
    {
        get { return pinnedKnight == KnightTarget.LeftKnight ? 1f : -1f; }
    }

    // Bats choose their victim from spawn X alone (EnemyBat: x < 0 targets the RIGHT
    // knight). Every bat in this family has to land on the free knight - post-gate
    // every 4th bat is a dark bat, and confusing the PINNED knight would be
    // unavoidable damage rather than a test of anything.
    protected float BatSpawnX(float magnitude)
    {
        return pinnedKnight == KnightTarget.LeftKnight ? -Mathf.Abs(magnitude) : Mathf.Abs(magnitude);
    }

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        SchedulePin(spawner);

        float pinEnd = Time.time + PinTotalSeconds;
        yield return SpawnFreeKnightContent(spawner);

        // The pin schedules every rock up front, so the wave must not be marked
        // complete until the last one has actually spawned and registered itself -
        // an unspawned rock is not yet a tracked projectile.
        while (Time.time < pinEnd) yield return null;

        MarkSpawningComplete();
        yield return null;
    }

    // The half that differs per family: what the free knight has to overcome.
    protected abstract IEnumerator SpawnFreeKnightContent(Spawner spawner);

    protected int AnchorCount
    {
        get { return anchorSequence != null ? Mathf.Max(1, anchorSequence.Count) : 1; }
    }

    protected float PinTotalSeconds
    {
        get { return AnchorCount * anchorHoldSeconds + (AnchorCount - 1) * Mathf.Max(0f, switchGapSeconds); }
    }

    // Every rock is scheduled at t=0 with its own delay. The stream is fully
    // deterministic - nothing is decided at runtime, so the wave always plays out
    // identically and the pattern can be learned (design rule 6).
    private void SchedulePin(Spawner spawner)
    {
        Transform pinned = PinnedTransform(spawner);
        if (pinned == null || anchorSequence == null || anchorSequence.Count == 0) return;

        float interval = Mathf.Max(0.05f, secondsBetweenRocks);
        float t = 0f;

        for (int a = 0; a < anchorSequence.Count; a++)
        {
            Vector2 anchor = AnchorPosition(pinned, anchorSequence[a]);
            float segmentEnd = t + anchorHoldSeconds;
            int rockInSegment = 0;

            while (t < segmentEnd)
            {
                spawner.SpawnProjectile(pinned, anchor, t);
                rockInSegment++;
                t += interval;
                if (pauseEveryNRocks > 0 && rockInSegment % pauseEveryNRocks == 0)
                {
                    t += pauseSeconds;
                }
            }

            t = segmentEnd + Mathf.Max(0f, switchGapSeconds);
        }
    }

    private Vector2 AnchorPosition(Transform pinned, PinAnchor anchor)
    {
        Vector2 aim = new Vector2(pinned.position.x, pinned.position.y + AimYOffset);
        switch (anchor)
        {
            case PinAnchor.Top:
                return new Vector2(aim.x, aim.y + anchorRadius);
            case PinAnchor.Bottom:
                return new Vector2(aim.x, aim.y - anchorRadius);
            default:
                // Outward, away from the arena, so the stream comes in over the
                // knight's own edge and can never cross their partner
                float outward = pinnedKnight == KnightTarget.LeftKnight ? -1f : 1f;
                return new Vector2(aim.x + outward * anchorRadius, aim.y);
        }
    }
}

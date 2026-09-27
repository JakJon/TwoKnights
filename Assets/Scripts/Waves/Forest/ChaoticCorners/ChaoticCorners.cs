using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "ChaoticCorners", menuName = "Waves/Chaotic Corners")]
public class ChaoticCorners : BaseWave
{
    // This is a medium difficult wave that spawns a large conical shape of
    // projectiles, with the opisite corner spawning a swarm of bats. The last wave
    // spawns one giant slime instead of a swarm of bats.


    [Tooltip("Recommended minimum delay time is 3 seconds to ensure proper wave pacing")]
    [SerializeField] private float delayTime = 5f;
    [SerializeField] private int projectilesPerArc = 8;
    [SerializeField] private int batsPerArc = 3;
    [SerializeField] private int kingSlimes = 1;
    [SerializeField] private int slimesPerArc;
    [SerializeField] private int slimesSize = 1;
    [SerializeField] private int arcCount = 5;
    [SerializeField] private float arcDelay = 1f;
    [SerializeField] private float delayBetweenProjectiles = 0.1f;

    [Header("Deep tiers")]
    [Tooltip("Reverse the sweep on every repeat instead of replaying it in the same direction. The repeats are identical without this, so the guard learns one drag of the stick and then performs it ten times; with it the stick has to turn round at the top of every sweep.")]
    [SerializeField] private bool alternateArcDirection = false;

    [Tooltip("Mirror every sweep onto the other knight at the same time, so both guards are being walked around their own dial at once instead of one knight working while the other waits out the phase.")]
    [SerializeField] private bool mirrorArcs = false;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        FireSweep(spawner, rightKnight: false, fromBelow: false);

        for (int i = 0; i < slimesPerArc; i++)
        {
            spawner.SpawnSlime(slimesSize, new Vector2(11, -5 - i), 0f, spawner.LeftPlayer);
        }

        for (int i = 0; i < batsPerArc; i++)
        {
            spawner.SpawnBat(new Vector2(11, -5), 0f);
            if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
        }

        spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), false);
        yield return new WaitForSeconds(arcDelay * arcCount);

        FireSweep(spawner, rightKnight: true, fromBelow: true);

        for (int i = 0; i < slimesPerArc; i++)
        {
            spawner.SpawnSlime(slimesSize, new Vector2(-11, 5 + i), 0f, spawner.RightPlayer);
        }

        for (int i = 0; i < batsPerArc; i++)
        {
            spawner.SpawnBat(new Vector2(-11, 5), 0f);
            if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
        }

        FireSweep(spawner, rightKnight: false, fromBelow: true);

        for (int i = 0; i < batsPerArc; i++)
        {
            spawner.SpawnBat(new Vector2(11, 5), 0f);
            if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
        }

        FireSweep(spawner, rightKnight: true, fromBelow: false);

        for (int i = 0; i < kingSlimes; i++)
        {
            spawner.SpawnSlime(3, new Vector2(-11 + (i * 3), -6), 0f, spawner.RightPlayer);
        }

        if (slimesPerArc > 0 && kingSlimes > 0)
        {
            spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), true);
        }

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }

    // One phase of the wave: arcCount repeats of a 90-degree sweep, at one knight
    // or (mirrorArcs) at both.
    //
    // The four phases used to hand-author a start point and a rotation direction
    // each, which worked but hid the rule they were all obeying. They are named by
    // WHICH KNIGHT and WHICH SIDE now, and the direction is derived — because it is
    // not a free choice. A sweep has to travel OUTWARD, away from the middle of the
    // board, and which rotation that is depends on both the knight and the side:
    // left-from-above and right-from-below are counter-clockwise, the other two are
    // clockwise. This is the same rule RockVolley.Fan derives, for the same reason.
    // Turn one of them round and the sweep walks across the centre line and is
    // eaten by the wrong knight's shield (rule 2).
    private void FireSweep(Spawner spawner, bool rightKnight, bool fromBelow)
    {
        Sweep(spawner, rightKnight, fromBelow);
        // Mirrored onto the other knight from the SAME side of the board. Each
        // half still sweeps outward into its own knight's territory, so the pair
        // opens away from each other and neither can reach the other's guard.
        if (mirrorArcs) Sweep(spawner, !rightKnight, fromBelow);
    }

    private void Sweep(Spawner spawner, bool rightKnight, bool fromBelow)
    {
        Transform knight = rightKnight ? spawner.RightPlayer : spawner.LeftPlayer;
        Vector2 centre = new Vector2(rightKnight ? 2f : -2f, 0f);
        Vector2 start = centre + new Vector2(0f, fromBelow ? -ArcRadius : ArcRadius);
        Spawner.ArcDirection direction = (rightKnight ^ fromBelow)
            ? Spawner.ArcDirection.Clockwise
            : Spawner.ArcDirection.CounterClockwise;

        if (!alternateArcDirection)
        {
            spawner.SpawnProjectileArc(knight, direction, start, SweepDegrees,
                                       projectilesPerArc, delayBetweenProjectiles,
                                       arcCount, arcDelay);
            return;
        }

        spawner.StartCoroutine(PingPong(spawner, knight, centre, start, direction));
    }

    // Reverse the sweep by swapping its ENDS, never by flipping the rotation in
    // place. Both are "the other direction" and only one of them is legal: the
    // sweep runs from straight above the knight out to the horizontal on that
    // knight's own side, so turning the rotation round at the same start point
    // sends it from straight above towards the CENTRE of the board — in frame, and
    // into the other knight. Starting from the far end and rotating back retraces
    // the identical quarter circle, which is what a reversal is supposed to mean.
    private IEnumerator PingPong(Spawner spawner, Transform knight, Vector2 centre,
                                 Vector2 start, Spawner.ArcDirection direction)
    {
        Vector2 end = EndOf(centre, start, direction);

        for (int repeat = 0; repeat < arcCount; repeat++)
        {
            bool outbound = repeat % 2 == 0;
            spawner.SpawnProjectileArc(
                knight,
                outbound ? direction : Opposite(direction),
                outbound ? start : end,
                SweepDegrees,
                projectilesPerArc,
                delayBetweenProjectiles);

            yield return new WaitForSeconds(arcDelay);
        }
    }

    // Where a SweepDegrees rotation about `centre` leaves `start`. Hard-wired to a
    // quarter turn, which is what SweepDegrees is and what keeps this a two-line
    // rotation rather than a trig call.
    private static Vector2 EndOf(Vector2 centre, Vector2 start, Spawner.ArcDirection direction)
    {
        Vector2 spoke = start - centre;
        Vector2 turned = direction == Spawner.ArcDirection.CounterClockwise
            ? new Vector2(-spoke.y, spoke.x)   // +90
            : new Vector2(spoke.y, -spoke.x);  // -90
        return centre + turned;
    }

    private static Spawner.ArcDirection Opposite(Spawner.ArcDirection direction)
    {
        return direction == Spawner.ArcDirection.Clockwise
            ? Spawner.ArcDirection.CounterClockwise
            : Spawner.ArcDirection.Clockwise;
    }

    // The sweep is a quarter circle at radius 12 from the knight's arc centre,
    // which is where the four hand-authored start points already sat. Twelve keeps
    // the whole quarter outside x +/-12 / y +/-7 (rule 1) — at the seven that
    // above/belowPlayer use, the horizontal end of the sweep would appear in open
    // frame.
    private const float ArcRadius = 12f;
    private const float SweepDegrees = 90f;
}
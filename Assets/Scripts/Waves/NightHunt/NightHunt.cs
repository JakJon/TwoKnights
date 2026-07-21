using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NightHunt", menuName = "Waves/Night Hunt")]
public class NightHunt : BaseWave
{
    // Coordinated wolf-and-bat pincer strikes. The wolf stalks a long visible arc
    // below its knight (the pre-chip window), lunges, and the bat dives from the
    // opposite high side strikeStagger seconds later — an aim-swing test, never a
    // forced shield-eat. Rounds escalate: single strike, staggered both, simultaneous.

    [Tooltip("How many strike rounds the wave runs. Round 1: single strike. Round 2: staggered on both knights. Round 3+: simultaneous on both")]
    [FormerlySerializedAs("rounds")]
    [SerializeField] private int strikeRounds = 3;
    [Tooltip("Seconds between the wolf reaching the knight and the bat diving in. Bigger = more time to swing your aim between the two")]
    [FormerlySerializedAs("strikeStagger")]
    [SerializeField] private float secondsBetweenWolfAndBat = 3f;
    [Tooltip("Full laps the wolf circles around its knight before it attacks")]
    [SerializeField] private int circlesBeforeAttack = 1;
    [Tooltip("Wolf color used in round 1")]
    [SerializeField] private WolfType roundOneWolf = WolfType.Grey;
    [Tooltip("Wolf color used in round 2")]
    [SerializeField] private WolfType roundTwoWolf = WolfType.Grey;
    [Tooltip("Wolf color used in rounds 3 and up")]
    [SerializeField] private WolfType roundThreeWolf = WolfType.Brown;

    // The stalk circle must stay entirely on the wolf's own half: with knights at
    // x = +-2, a knight-centered radius-5 circle swung to x = -+3 and clipped the
    // OTHER knight on the first pass. Centering further out keeps the loop clear.
    private const float StalkRadius = 3.5f;
    private const float StalkCenterX = 3.5f;
    private const float EntryPathLength = 8f;     // bottom corner to the first circle point
    private const float ArcStepDegrees = 15f;
    private const float LungeSeconds = 2f;
    private const float BatTravelEstimate = 8.5f; // spawn -> (0,±3) -> knight at bat speed 2
    private const float RestSeconds = 6f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        int totalRounds = Mathf.Max(1, strikeRounds);
        for (int round = 0; round < totalRounds; round++)
        {
            WolfType wolf = WolfForRound(round);

            if (round == 0)
            {
                LaunchStrike(spawner, 1f, wolf, 0f);
            }
            else if (round == 1)
            {
                LaunchStrike(spawner, -1f, wolf, 0f);
                LaunchStrike(spawner, 1f, wolf, 2f);
            }
            else
            {
                LaunchStrike(spawner, -1f, wolf, 0f);
                LaunchStrike(spawner, 1f, wolf, 0f);
                if (round >= 3)
                {
                    // A straggler tank trails the simultaneous strike in the biggest rounds
                    LaunchStrike(spawner, round % 2 == 0 ? 1f : -1f, WolfType.Black, 3f);
                }
            }

            // Wait out the strike, then a rest bar: a lazy steep arc keeps shields
            // honest (blocked projectiles are combo-safe) and an orb rewards the swing
            float strikeLength = StalkSeconds(wolf) + LungeSeconds + secondsBetweenWolfAndBat + 4f;
            yield return new WaitForSeconds(strikeLength);

            Transform arcTarget = round % 2 == 0 ? spawner.LeftPlayer : spawner.RightPlayer;
            Vector2 arcStart = round % 2 == 0 ? new Vector2(-2f, 12f) : new Vector2(2f, -12f);
            spawner.SpawnProjectileArc(arcTarget, Spawner.ArcDirection.CounterClockwise, arcStart, 60f, 3, 0.4f);

            bool lastRound = round == totalRounds - 1;
            float orbFromY = round % 2 == 0 ? -9f : 9f;
            spawner.SpawnOrb(new Vector2(0f, orbFromY), new Vector2(0f, -orbFromY), lastRound, 1f);

            yield return new WaitForSeconds(RestSeconds);
        }

        MarkSpawningComplete();
        yield return null;
    }

    private void LaunchStrike(Spawner spawner, float side, WolfType wolfType, float delay)
    {
        Transform knight = side > 0 ? spawner.RightPlayer : spawner.LeftPlayer;
        Vector2 center = new Vector2(side * StalkCenterX, 0f);

        // Stalk: in from the bottom corner, then full circles around the knight
        // (the menace/pre-chip window), ending on a quarter arc below before the lunge
        var waypoints = new List<Vector2> { new Vector2(side * 13f, -6.5f) };
        float endDeg = -110f - 360f * Mathf.Max(0, circlesBeforeAttack);
        for (float deg = -20f; deg >= endDeg; deg -= ArcStepDegrees)
        {
            float rad = deg * Mathf.Deg2Rad;
            waypoints.Add(center + new Vector2(side * StalkRadius * Mathf.Cos(rad), StalkRadius * Mathf.Sin(rad)));
        }
        spawner.SpawnWolf(waypoints, knight, wolfType, delay);

        // The bat spawns on the OPPOSITE side (so it crosses to this knight, arriving
        // high via (0,3)), timed to dive secondsBetweenWolfAndBat after the wolf lands
        float wolfContact = delay + StalkSeconds(wolfType) + LungeSeconds;
        float batDelay = Mathf.Max(0f, wolfContact + secondsBetweenWolfAndBat - BatTravelEstimate);
        spawner.SpawnBat(new Vector2(-side * 13f, 7.5f), batDelay);
    }

    private float StalkSeconds(WolfType type)
    {
        float speed = type == WolfType.Brown ? 3.5f : type == WolfType.Grey ? 3f : 2.5f;
        float arcDegrees = 90f + 360f * Mathf.Max(0, circlesBeforeAttack);
        float pathLength = EntryPathLength + (arcDegrees / 360f) * (2f * Mathf.PI * StalkRadius);
        return pathLength / speed;
    }

    private WolfType WolfForRound(int round)
    {
        if (round <= 0) return roundOneWolf;
        if (round == 1) return roundTwoWolf;
        return roundThreeWolf;
    }
}

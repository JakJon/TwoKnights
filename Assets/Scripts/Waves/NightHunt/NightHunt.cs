using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NightHunt", menuName = "Waves/Night Hunt")]
public class NightHunt : BaseWave
{
    // Coordinated wolf-and-bat pincer strikes. The wolf stalks a long visible arc
    // below its knight (the pre-chip window), lunges, and the bat dives from the
    // opposite high side strikeStagger seconds later — an aim-swing test, never a
    // forced shield-eat. Rounds escalate: single strike, staggered both, simultaneous.

    [Tooltip("Strike rounds. Round 1: single strike. Round 2: staggered on both knights. Round 3+: simultaneous")]
    [SerializeField] private int rounds = 3;
    [Tooltip("Seconds between the wolf's lunge landing and the bat's dive — the aim-swing window")]
    [SerializeField] private float strikeStagger = 3f;
    [SerializeField] private WolfType roundOneWolf = WolfType.Grey;
    [SerializeField] private WolfType roundTwoWolf = WolfType.Grey;
    [SerializeField] private WolfType roundThreeWolf = WolfType.Brown;

    private const float StalkRadius = 5f;
    private const float StalkPathLength = 16f;    // corner entry + 90-degree arc, ~16 units
    private const float LungeSeconds = 2f;
    private const float BatTravelEstimate = 8.5f; // spawn -> (0,±3) -> knight at bat speed 2
    private const float RestSeconds = 6f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        int totalRounds = Mathf.Max(1, rounds);
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
            float strikeLength = StalkSeconds(wolf) + LungeSeconds + strikeStagger + 4f;
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
        Vector2 center = new Vector2(side * 2f, 0f);

        // Stalk: in from the bottom corner, then a slow visible arc under the knight
        var waypoints = new List<Vector2> { new Vector2(side * 13f, -6.5f) };
        for (int deg = -20; deg >= -110; deg -= 15)
        {
            float rad = deg * Mathf.Deg2Rad;
            waypoints.Add(center + new Vector2(side * StalkRadius * Mathf.Cos(rad), StalkRadius * Mathf.Sin(rad)));
        }
        spawner.SpawnWolf(waypoints, knight, wolfType, delay);

        // The bat spawns on the OPPOSITE side (so it crosses to this knight, arriving
        // high via (0,3)), timed to dive strikeStagger seconds after the wolf lands
        float wolfContact = delay + StalkSeconds(wolfType) + LungeSeconds;
        float batDelay = Mathf.Max(0f, wolfContact + strikeStagger - BatTravelEstimate);
        spawner.SpawnBat(new Vector2(-side * 13f, 7.5f), batDelay);
    }

    private float StalkSeconds(WolfType type)
    {
        float speed = type == WolfType.Brown ? 3.5f : type == WolfType.Grey ? 3f : 2.5f;
        return StalkPathLength / speed;
    }

    private WolfType WolfForRound(int round)
    {
        if (round <= 0) return roundOneWolf;
        if (round == 1) return roundTwoWolf;
        return roundThreeWolf;
    }
}

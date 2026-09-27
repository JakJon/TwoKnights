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
    //
    // What that shape was missing, and what the two blocks below are (owner's call,
    // 2026-09-06):
    //
    //   THE HUNT WAS TOO THIN. A strike is one wolf and one bat, so a round was
    //   two bodies for two knights and the wave read as a drill rather than a
    //   hunt. Both halves of the pincer now come in numbers — a flight of bats
    //   behind the diver, a litter of rats let loose under the stalk — and the
    //   counts are per-tier so the ladder is a pack getting bigger rather than
    //   the same four animals arriving sooner.
    //
    //   AND THE REST BAR WAS FREE. The wave's whole tension is that shield facing
    //   is also shooting direction, so being made to look low costs you the high
    //   answer — and then it handed the player a long quiet stretch between rounds
    //   with one lazy arc in it. Rock now runs UNDERNEATH the entire wave on its
    //   own cycle (RockVolley, the same authoring the Mine uses), so the stalk is
    //   read while the shafts are working and the rest bar is where you pay for
    //   the arrows you did not spend. The old single arc is gone: it was one
    //   shape, at one moment, in one direction, and the guard learned it in a run.

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

    [Header("The pack")]
    [Tooltip("Bats in the flight behind each strike's diver. The first is the pincer's own bat and keeps its timing exactly; the rest follow it in at batSpacing, so the dive is a stream to be shot down rather than a single yes-or-no block. In the Mine's staggering window every fourth bat of a wave is a dark one, so raising this is also how many sonars the round carries.")]
    [SerializeField] private int batsPerStrike = 1;

    [Tooltip("Seconds between the bats of one flight. Kept short — the flight is meant to arrive as a flight, not as four separate dives.")]
    [SerializeField] private float batSpacing = 0.7f;

    [Tooltip("Rats let out under each strike, on the stalking wolf's own side. They enter as the wolf begins its circles, which is deliberate: the stalk is a long window the player is supposed to spend arrows in, and these are what that window now costs. Keep it to two a knight — rats are not one-shot kills.")]
    [SerializeField] private int ratsPerStrike;

    [Tooltip("Extra stalkers in each strike beyond the first, released a beat apart on the same circle. The lunge order is therefore the release order, which is what stops a second wolf being a surprise.")]
    [SerializeField] private int wolvesPerStrike = 1;

    [Tooltip("Seconds between one stalker of a strike entering and the next.")]
    [SerializeField] private float wolfSpacing = 1.2f;

    [Header("Rock")]
    [Tooltip("Volleys cycled in order underneath the whole wave — the shapes, the knights and the windows are all authored here (see RockVolley). This is the wave's second question: the pincer steers your facing, and the rock punishes the facing it steered you to.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of firing, from the start of the wave. New volleys stop being issued once it elapses; whatever is in the air still falls. Size it against the rounds — a window that runs out halfway leaves the back of the wave silent.")]
    [SerializeField] private float projectileWindow = 45f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.6f;

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
        // Beside the wave, never inside it: the window is a fixed number of
        // seconds from the start, so the rock neither stretches nor truncates
        // with how the rounds happen to run.
        Coroutine shafts = spawner.StartCoroutine(
            RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed));

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

            // No arc here any more — the shafts have been working underneath the
            // whole round and go on working through the rest bar, which is a
            // better version of what the single arc was for.
            bool lastRound = round == totalRounds - 1;
            // Three orbs is the wave's whole allowance, so the five-round tiers
            // spread them over the rounds instead of one per round. The last round
            // always keeps its health orb — it is the round that earns it.
            if (WaveOrbBudget.SlotCarriesOrb(round, totalRounds))
            {
                float orbFromY = round % 2 == 0 ? -9f : 9f;
                spawner.SpawnOrb(new Vector2(0f, orbFromY), new Vector2(0f, -orbFromY), lastRound, 1f);
            }

            yield return new WaitForSeconds(RestSeconds);
        }

        // Waited on rather than stopped: rock already issued is rock the wave is
        // still responsible for, and tracking will not end the wave until it lands.
        yield return shafts;

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

        // Every stalker of a strike walks the SAME circle, a beat apart. Same path
        // rather than nested ones on purpose: the arc is authored to stay clear of
        // the other knight (see StalkRadius), and a second, wider circle would not
        // be. Spaced out, they arrive in the order they were released, so the
        // second wolf is a deadline the player can already see rather than an
        // ambush out of the first one's shadow.
        int stalkers = Mathf.Max(1, wolvesPerStrike);
        for (int i = 0; i < stalkers; i++)
        {
            spawner.SpawnWolf(waypoints, knight, wolfType,
                              delay + i * Mathf.Max(0f, wolfSpacing));
        }

        // The rats come in WITH the stalk rather than after it. The stalk is a long
        // deliberate window the wave is asking the player to spend arrows in, and
        // until now the only thing in it was the wolf they could already see.
        for (int i = 0; i < Mathf.Max(0, ratsPerStrike); i++)
        {
            // Their own side of the board, so they never walk the middle and
            // become the other knight's problem
            Vector2 target = new Vector2(side * (4f + i * 1.6f), i % 2 == 0 ? -1.5f : 1.5f);
            spawner.SpawnRat(target, delay + 0.5f + i * 0.9f, knight);
        }

        // The bats spawn on the OPPOSITE side (so they cross to this knight,
        // arriving high via (0,3)), the first timed to dive secondsBetweenWolfAndBat
        // after the wolf lands and the rest trailing it. The first one's timing is
        // untouched by the count — the pincer is the pincer, and the flight behind
        // it is the part that grew.
        float wolfContact = delay + StalkSeconds(wolfType) + LungeSeconds;
        float batDelay = Mathf.Max(0f, wolfContact + secondsBetweenWolfAndBat - BatTravelEstimate);
        int bats = Mathf.Max(1, batsPerStrike);
        for (int i = 0; i < bats; i++)
        {
            // Walked along the top edge so a flight arrives spread out rather than
            // as one column the shield answers with a single facing
            float x = -side * (13f - i * 1.4f);
            spawner.SpawnBat(new Vector2(x, 7.5f), batDelay + i * Mathf.Max(0f, batSpacing));
        }
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

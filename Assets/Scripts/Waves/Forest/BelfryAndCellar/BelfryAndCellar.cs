using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BelfryAndCellar", menuName = "Waves/Belfry and Cellar")]
public class BelfryAndCellar : BaseWave
{
    // Two jobs, one aim direction. Bats cross on a steady beat, alternating high/low
    // lanes (one arrow each, on schedule). Meanwhile rat pairs patrol the floor on the
    // rat's built-in 15s fuse — spare arrows between bat beats must pay the rats down
    // before they charge. Beat accelerates after every rat pair.

    [Tooltip("How many pairs of rats spawn over the wave (one rat per knight, per pair). Each pair charges 15s after walking in")]
    [SerializeField] private int ratPairs = 3;
    [Tooltip("Seconds between one bat and the next at the start of the wave")]
    [FormerlySerializedAs("batBeatSeconds")]
    [SerializeField] private float secondsBetweenBats = 4f;
    [Tooltip("Bats spawn faster as the wave goes on: the gap between bats is multiplied by this after every rat pair. 1 = no speed-up, 0.9 = 10% faster each time")]
    [FormerlySerializedAs("beatDecay")]
    [SerializeField] private float batSpeedUpMultiplier = 0.9f;

    [Header("Rock")]
    [Tooltip("Cycled in order, one per pin slot - a pin comes up every third bat. Leave EMPTY to keep the old single steep rock, which is one rock every three bats and the thinnest rock in the forest. Author these to fire BETWEEN bat beats, as the old one did: a pin that lands on a bat's arrival traps a knight into eating it, which is the one thing the shield rules forbid.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    private const float CycleSeconds = 12f;    // gap between rat pairs; the rat fuse itself is 15s
    private const float MinBeatSeconds = 1.5f; // never outpace the arrow cooldown
    private const float RatSlotX = 6.5f;
    private const float RatSlotY = -4f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        float beat = Mathf.Max(MinBeatSeconds, secondsBetweenBats);
        int batIndex = 0;
        // Which pin slot the wave is on. Counted separately from batIndex so the
        // volley list is walked one entry per pin rather than every third entry.
        int pinIndex = 0;

        for (int pair = 0; pair < ratPairs; pair++)
        {
            // The cellar: one rat per knight walks in and patrols — a visible 15s fuse.
            // Which rat is the map's call now, not this wave's: the pair comes off
            // the depth cadence, so the same asset sends greys and blacks early and
            // moonlit and browns deep without needing a tier per roster.
            spawner.SpawnRat(new Vector2(-RatSlotX, RatSlotY), 0f, spawner.LeftPlayer);
            spawner.SpawnRat(new Vector2(RatSlotX, RatSlotY), 0f, spawner.RightPlayer);

            // Orbs ride the lane you should already be watching. Three per wave is
            // the allowance (health and mana counted together), so the four- and
            // five-pair tiers spread them across the pairs rather than one apiece.
            if (WaveOrbBudget.SlotCarriesOrb(pair, ratPairs))
            {
                if (pair % 2 == 0)
                    spawner.SpawnOrb(new Vector2(-13f, 4.5f), new Vector2(13f, 4.5f), false, beat * 0.5f);
                else
                    spawner.SpawnOrb(new Vector2(13f, -4.5f), new Vector2(-13f, -4.5f), true, beat * 0.5f);
            }

            // The belfry: bats cross on the beat, alternating entry side and altitude.
            // A bat spawned on the left always crosses to attack the RIGHT knight.
            float elapsed = 0f;
            while (elapsed < CycleSeconds)
            {
                float sideX = batIndex % 2 == 0 ? -13f : 13f;
                float altY = (batIndex / 2) % 2 == 0 ? 7.5f : -7.5f;
                spawner.SpawnBat(new Vector2(sideX, altY), 0f);

                // Sparse steep pin shot at the knight NOT handling this bat,
                // fired between beats so it never traps anyone into eating an enemy
                if (batIndex % 3 == 2)
                {
                    if (volleys != null && volleys.Count > 0)
                    {
                        RockVolley.FireAfter(spawner, volleys[pinIndex % volleys.Count],
                                             rockSpeed, beat * 0.5f);
                    }
                    else
                    {
                        bool pinLeft = batIndex % 2 == 0;
                        Transform pinned = pinLeft ? spawner.LeftPlayer : spawner.RightPlayer;
                        Vector2 from = altY > 0
                            ? (pinLeft ? spawner.belowLeftPlayer : spawner.belowRightPlayer)
                            : (pinLeft ? spawner.aboveLeftPlayer : spawner.aboveRightPlayer);
                        spawner.SpawnProjectileStraight(from, pinned, 1, 1f, beat * 0.5f);
                    }
                    pinIndex++;
                }

                batIndex++;
                yield return new WaitForSeconds(beat);
                elapsed += beat;
            }

            beat = Mathf.Max(MinBeatSeconds, beat * batSpeedUpMultiplier);
        }

        MarkSpawningComplete();
        yield return null;
    }

}

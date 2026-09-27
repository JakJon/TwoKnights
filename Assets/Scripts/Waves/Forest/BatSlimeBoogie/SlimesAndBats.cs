using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimesAndBats", menuName = "Waves/Slimes and Bats")]
public class SlimesAndBats : BaseWave
{
    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;
    [SerializeField] private int minimumSlimeSize = 1;
    [SerializeField] private float batDelay = 1f;
    [SerializeField] private float projectileDelay = 3f;
    [Tooltip("LEGACY SHAPE, tiers I-IV: rocks in each of the two straight streams. Ignored entirely once `volleys` has anything in it")]
    [SerializeField] private int projectileCount = 4;

    [Header("Rock")]
    [Tooltip("Cycled in order underneath the whole wave (see RockVolley). Leave EMPTY to keep the old pair of straight streams from underneath - two knights, but both shields held on the same downward facing for the length of the stream, which is the thing the deep tiers are authored to stop doing.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of rock from the start of the wave. Occurrences run 10 + N seconds each, so six of them is about 81 and seven about 98.")]
    [SerializeField] private float projectileWindow = 0f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // Two orbs every even occurrence ran to six on the longest tier. Orbs come
        // in pairs, so there is no clean slot to spread three across — the budget
        // stops the pairs at three. Local to the run, not a field: the asset is
        // shared and its state outlives the wave.
        WaveOrbBudget.Budget orbs = new WaveOrbBudget.Budget();

        // Underneath the occurrences rather than once per occurrence: the streams
        // this replaces ran for most of an occurrence, and a single volley on the
        // occurrence beat would have been a fraction of the rock the wave had.
        bool authored = volleys != null && volleys.Count > 0;
        Coroutine shafts = authored
            ? spawner.StartCoroutine(
                RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed))
            : null;

        for (int wave = 1; wave <= waveOccurences; wave++)
        {
            if (!authored)
            {
                spawner.SpawnProjectileStraight(spawner.belowLeftPlayer, spawner.LeftPlayer, projectileCount, projectileDelay);
                spawner.SpawnProjectileStraight(spawner.belowRightPlayer, spawner.RightPlayer, projectileCount, projectileDelay);
            }

            // Amount of bats goes up each wave
            for (int j = -1; j < wave; j++)
            {
                if (j == -1)
                    spawner.SpawnBat(new Vector2(10, -6.5f), 0f); // No delay for first bat
                else if (math.abs(j) % 2 == 0)
                    spawner.SpawnBat(new Vector2(12 - wave, 6.5f), batDelay);
                else
                    spawner.SpawnBat(new Vector2(-12 + wave, -6.5f), batDelay);
            }

            // Spawn slimes: largest possible sizes, sum to wave number, max size 3.
            //
            // Every slime used to be handed to the RIGHT knight, which made the
            // left one a spectator for the slime half of the wave while his
            // partner was buried. They alternate now, and each one comes down over
            // the side of the board it is going to rather than queueing at the
            // middle and walking across - a slime aimed at the left knight that
            // enters on the right crosses the whole arena to get there, which
            // reads as the wave changing its mind.
            int remaining = wave;
            const float slimeEntryY = 6.5f;
            int slimeCount = 0;
            while (remaining > 0)
            {
                int size = Mathf.Min(3, remaining);
                // Ensure slime size meets minimum requirement
                size = Mathf.Max(minimumSlimeSize, size);

                // Paired outward from the middle - -2, +2, -6, +6 - so the two
                // knights are handed the same slime at the same distance on the
                // same beat instead of one of them getting the whole column.
                bool toLeft = slimeCount % 2 == 0;
                float lane = 2f + (slimeCount / 2) * 4f;
                Vector2 spawnPos = new Vector2(toLeft ? -lane : lane, slimeEntryY);

                spawner.SpawnSlime(size, spawnPos, 0,
                                   toLeft ? spawner.LeftPlayer : spawner.RightPlayer);
                remaining -= size;
                slimeCount++;
            }

            // Spawn mana orbs
            if (wave % 2 == 0)
            {
                if (orbs.TrySpend()) spawner.SpawnOrb(spawner.leftOfLeftPlayer, spawner.aboveRightPlayer, false);
                if (orbs.TrySpend()) spawner.SpawnOrb(spawner.rightOfRightPlayer, spawner.aboveLeftPlayer, false);
            }

            yield return new WaitForSeconds(10f + wave);
        }

        if (shafts != null) yield return shafts;

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
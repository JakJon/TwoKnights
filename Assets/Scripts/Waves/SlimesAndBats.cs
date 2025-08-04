using System.Collections;
using System.Net.NetworkInformation;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimesAndBats", menuName = "Waves/Slimes and Bats")]
public class SlimesAndBats : BaseWave
{
    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;
    [SerializeField] private int minimumSlimeSize = 1;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        for (int wave = 1; wave <= waveOccurences; wave++)
        {
            spawner.SpawnProjectileStraight(spawner.belowLeftPlayer, spawner.LeftPlayer, 3 + wave, 4);
            spawner.SpawnProjectileStraight(spawner.belowRightPlayer, spawner.RightPlayer, 3 + wave, 4);

            // Amount of bats goes up each wave
            for (int j = -1; j < wave; j++)
            {
                spawner.SpawnBat(new Vector2(wave * 2, 6.5f), math.abs(j));
            }

            // Spawn slimes: largest possible sizes, sum to wave number, max size 3
            int remaining = wave;
            Vector2 spawnPos = new Vector2(0, 6.5f);
            int slimeCount = 0;
            while (remaining > 0)
            {
                int size = Mathf.Min(3, remaining);
                // Ensure slime size meets minimum requirement
                size = Mathf.Max(minimumSlimeSize, size);
                // Offset each slime horizontally for visibility
                Vector2 offset = new Vector2(slimeCount * 4 - 2, 0);
                spawner.SpawnSlime(size, spawnPos + offset, 0, spawner.RightPlayer);
                remaining -= size;
                slimeCount++;
            }

            // Spawn mana orbs
            if (wave % 2 == 0)
            {
                spawner.SpawnOrb(spawner.leftOfLeftPlayer, spawner.aboveRightPlayer, false);
                spawner.SpawnOrb(spawner.rightOfRightPlayer, spawner.aboveLeftPLayer, false);
            }

            yield return new WaitForSeconds(10f + wave);
        }
        
        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();
        
        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
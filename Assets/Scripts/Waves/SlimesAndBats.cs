using System.Collections;
using System.Net.NetworkInformation;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimesAndBats", menuName = "Waves/Slimes and Bats")]
public class SlimesAndBats : BaseWave
{
    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        for (int wave = 1; wave <= waveOccurences; wave++)
        {
            spawner.SpawnProjectileStraight(spawner.belowLeftPlayer, spawner.LeftPlayer, 3 + wave, 4 );
            spawner.SpawnProjectileStraight(spawner.belowRightPlayer, spawner.RightPlayer, 3 + wave, 4);

            // Amount of bats goes up each wave
            for (int j = -1; j < wave; j++)
            {
                spawner.SpawnBat(new Vector2(wave * 2, 7), math.abs(j));
            }

            // Spawn slimes: largest possible sizes, sum to wave number, max size 3
            int remaining = wave;
            Vector2 spawnPos = new Vector2(0, 7);
            int slimeCount = 0;
            while (remaining > 0)
            {
                int size = Mathf.Min(3, remaining);
                // Offset each slime horizontally for visibility
                Vector2 offset = new Vector2(slimeCount * 2 - 2, 0);
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

            yield return new WaitForSeconds(15f + wave);
        }
        yield return new WaitForSeconds(5f);
    }
}
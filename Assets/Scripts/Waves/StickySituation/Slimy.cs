using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimyWave", menuName = "Waves/Slimy")]
public class Slimy : BaseWave
{
    [SerializeField] private int waveOccurences = 1;
    [SerializeField] private int projectilesPerStraight = 1;
    [SerializeField] private int delayBetweenProjectiles = 1;
    [SerializeField] private int delayBetweenStraights = 1;
    [SerializeField] private int amountOfStraights = 8;
    [Tooltip("Amount of base slimes, if mid slimes / 2, if king slime / 3")]
    [SerializeField] private int amountOfSlimesOnEachSide = 4;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // starting loops at one so that the first waves are not skipped
        for (int wave = 1; wave <= waveOccurences; wave++)
        {

            // Spawn slimes based on the wave number
            if (wave % 2 == 0)
            {
                spawner.SpawnOrb(new Vector2(-12, 3), new Vector2(12, 3), false);
                // variable that divides the amount of slimes by 2 and rounds down
                int midSlimes = Mathf.FloorToInt(amountOfSlimesOnEachSide / 2f);
                for (int i = 0; i < midSlimes; i++)
                {
                    spawner.SpawnSlime(2, new Vector2(-12 - (i * 2), i * 4), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(2, new Vector2(12 + (i * 2), -i * 4), 0f, spawner.RightPlayer);
                }
            }
            else if (wave % 3 == 0)
            {
                spawner.SpawnOrb(new Vector2(-12, -3), new Vector2(12, -3), true);
                // variable that divides the amount of slimes by 3 and rounds down
                int kingSlimes = Mathf.FloorToInt(amountOfSlimesOnEachSide / 3f);
                for (int i = 0; i < kingSlimes; i++)
                {
                    spawner.SpawnSlime(3, new Vector2(-12 - (i * 2), (i + 1)* 5), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(3, new Vector2(12 + (i * 2), (-i - 1) * 5), 0f, spawner.RightPlayer);
                }
            }
            else
            {
                for (int i = 0; i < amountOfSlimesOnEachSide; i++)
                {
                    spawner.SpawnSlime(1, new Vector2(-12 - (i * 2), i * 2), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(1, new Vector2(12 + (i * 2), -i * 2), 0f, spawner.RightPlayer);
                }
            }

            for (int straight = 1; straight < amountOfStraights; straight++)
            {
                if (straight % 2 == 0)
                {
                    spawner.SpawnProjectileStraight(
                        spawner.aboveLeftPLayer, 
                        spawner.LeftPlayer, 
                        projectilesPerStraight, 
                        delayBetweenProjectiles
                     );

                    spawner.SpawnProjectileStraight(
                        spawner.belowRightPlayer,
                        spawner.RightPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                }
                else                 {
                    spawner.SpawnProjectileStraight(
                        spawner.belowLeftPlayer,
                        spawner.LeftPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                    spawner.SpawnProjectileStraight(
                        spawner.aboveRightPlayer,
                        spawner.RightPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                }
                yield return new WaitForSeconds(delayBetweenStraights);
            }
        }
        
        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();
        
        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
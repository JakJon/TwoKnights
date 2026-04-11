using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WolfCircles", menuName = "Waves/Wolf Circles")]
public class WolfCircles : BaseWave
{
    [SerializeField] private int wolfCount = 1;
    [SerializeField] private int additionalCircles = 0;

    [SerializeField] private WolfType WolfOneType = WolfType.Grey;
    [SerializeField] private WolfType WolfTwoType = WolfType.Grey;
    [SerializeField] private WolfType WolfThreeType = WolfType.Grey;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        var circles = new List<Vector2> // Start with an entry point above left player
        {
            spawner.aboveLeftPlayer,
            new Vector2(-2, 2.5f) 
        };

        for (int c = 0; c < additionalCircles; c++)
        {
            circles.AddRange(WolfMovementPatterns.CircleLeftThenRight);
        }

        for (int i = 0; i < wolfCount; i++)
        {
            if (i == 0)
            {
                spawner.SpawnWolf(circles, spawner.RightPlayer, WolfOneType, 0); // First wolf spawns immediately
            }
            else if (i % 3 == 1)
            {
                spawner.SpawnWolf(circles, spawner.RightPlayer, WolfTwoType, i + 1);
            }
            else if (i % 3 == 0)
            {
                spawner.SpawnWolf(circles, spawner.RightPlayer, WolfThreeType, i + 1);
            }
            else
            {
                spawner.SpawnWolf(circles, spawner.RightPlayer, WolfOneType, i + 1);
            }
        }

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
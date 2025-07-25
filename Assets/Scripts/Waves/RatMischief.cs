using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "RatMischief", menuName = "Waves/Rat Mischief")]
public class RatMischef : BaseWave
{
    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;
    [SerializeField] private int baseRatCount = 1;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        int[] yPattern = { 2, 3, 4, 3, 2, 1 };
        for (int i = 1; i < waveOccurences + 1; i++)
        {
            float yOffset = yPattern[(i - 1) % yPattern.Length];

            int totalRats = baseRatCount * i;
            float spacing = 2.5f;
            float groupWidth = (totalRats - 1) * spacing;
            float startX = 0 + (-groupWidth / 2f); // Center group around x = -6
            float staggerAmount = 0.75f; // Adjust for more/less vertical staggering

            if (i == 2)
            {
                spawner.SpawnOrb(new Vector2(0, 6), new Vector2(-12, -2), false, 12f);
            }
            if (i == 5)
            {
                spawner.SpawnOrb(new Vector2(8, -7), new Vector2(8, 7), false, 20f);
            }

            spawner.SpawnProjectileArc(
                spawner.RightPlayer,
                Spawner.ArcDirection.CounterClockwise,
                spawner.belowRightPlayer,
                180f,
                3 + i,
                1f
                );

            for (int ratIndex = 1; ratIndex < totalRats + 1; ratIndex++)
            {
                Vector2 spawnPos;
                GameObject ratPrefab = null;
                Transform targetPlayer = null;

                float xPos = startX + (ratIndex - 1) * spacing;
                float stagger = (ratIndex - 1 - (totalRats - 1) / 2f) * staggerAmount;

                float y = (ratIndex % 2 == 0) ? -yOffset + stagger : yOffset + stagger;
                if (y > -2f && y < 2f)
                    y += 2f * Mathf.Sign(y == 0 ? (ratIndex % 2 == 0 ? -1 : 1) : y);
                y = Mathf.Clamp(y, -yOffset, yOffset);
                if (y < 0 && y > -2)
                {
                    y = y - 2f;
                }
                else if (y > 0 && y < 2)
                {
                    y = y + 2f;
                }

                spawnPos = new Vector2(xPos, y);

                if (ratIndex % 2 == 0)
                {
                    ratPrefab = spawner.brownRat;
                    targetPlayer = spawner.RightPlayer;
                }
                else
                {
                    targetPlayer = spawner.LeftPlayer;
                }

                if (ratIndex % 3 == 0)
                {
                    ratPrefab = spawner.greyRat;
                }
                else if (ratPrefab == null)
                {
                    ratPrefab = spawner.blackRat;
                }



                spawner.SpawnRat(spawnPos, ratPrefab, 0, targetPlayer);
            }
            yield return new WaitForSeconds(12f + i * 2);
        }
        yield return new WaitForSeconds(2f);
    }
}

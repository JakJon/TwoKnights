using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "BatSwarmWave", menuName = "Waves/Bat Swarm Wave")]
public class BatSwarmWave : BaseWave
{
    // The first wave ever made. Super simple, just spawns a swarm of bats in a circular formation

    [SerializeField] private int batsPerRing = 8;
    [SerializeField] private int numberOfRings = 3;
    [SerializeField] private float delayBetweenRings = 15f;
    [SerializeField] private float ringRadius = 3f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {

        for (int ring = 1; ring <= numberOfRings; ring++)
        {


            float angleStep = 360f / batsPerRing;
            for (int i = 1; i <= batsPerRing; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle * Mathf.Deg2Rad) * ringRadius;
                float y = Mathf.Sin(angle * Mathf.Deg2Rad) * ringRadius;
                
                spawner.SpawnBat(new Vector2(x, y), ring * delayBetweenRings);

            }

            if (ring % 2 == 0)
            {
                spawner.SpawnProjectileStraight(spawner.topLeftCorner, spawner.LeftPlayer, 3, 4, ring * delayBetweenRings);
                spawner.SpawnOrb(new Vector2(8, 8), new Vector2(8, -8), false);
                spawner.SpawnOrb(new Vector2(-8, 8), new Vector2(-8, -8), false);
            }
            else if (ring % 2 != 0)
            {
                spawner.SpawnProjectileStraight(spawner.bottomRightCorner, spawner.RightPlayer, 3, 4, ring * delayBetweenRings);
            }
        }

        // Wait for the wave to finish
        yield return new WaitForSeconds(numberOfRings * delayBetweenRings + 15f);
    }
}
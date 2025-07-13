using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "ChaoticCorners", menuName = "Waves/Chaotic Corners")]
public class ChaoticCorners : BaseWave
{
    // This is a medium difficult wave that spawns a large conical shape of
    // projectiles, with the opisite corner spawning a swarm of bats. The last wave
    // spawns one giant slime instead of a swarm of bats.

    [SerializeField] private int numberOfSwarms = 3;
    [Tooltip("Recommended minimum delay time is 3 seconds to ensure proper wave pacing")]
    [SerializeField] private float delayTime = 5f;
    [SerializeField] private int projectilesPerArc = 8;
    [SerializeField] private int batsPerArc = 3;
    [SerializeField] private int kingSlimes = 1;
    [SerializeField] private int slimesPerArc;
    [SerializeField] private int slimesSize = 1;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        for (int swarm = 0; swarm < numberOfSwarms; swarm++)
        {
            spawner.SpawnProjectileArc(
                spawner.LeftPlayer,
                Spawner.ArcDirection.CounterClockwise,
                new Vector2(-2, 12),
                90f,
                projectilesPerArc,
                0.1f,
                5,
                1f
            );

            for (int i = 0; i < slimesPerArc; i++)
            {
                spawner.SpawnSlime(slimesSize, new Vector2(11, -5 - i), 0f, spawner.LeftPlayer);
            }

            for (int i = 0; i < batsPerArc; i++) {
                spawner.SpawnBat(new Vector2(11, -5), 0f);
                if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
            }

            spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), false);

            spawner.SpawnProjectileArc(
                spawner.RightPlayer,
                Spawner.ArcDirection.CounterClockwise,
                new Vector2(2, -12),
                90f,
                projectilesPerArc,
                0.1f,
                5,
                1f
            );

            for (int i = 0; i < slimesPerArc; i++) {
                spawner.SpawnSlime(slimesSize, new Vector2(-11, 5 + i), 0f, spawner.RightPlayer);
            }

            for (int i = 0; i < batsPerArc; i++) {
                spawner.SpawnBat(new Vector2(-11, 5), 0f);
                if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
            }

            spawner.SpawnProjectileArc(
                spawner.LeftPlayer,
                Spawner.ArcDirection.Clockwise,
                new Vector2(-2, -12),
                90f,
                projectilesPerArc,
                0.1f,
                5,
                1f
            );

            for (int i = 0; i < batsPerArc; i++) {
                spawner.SpawnBat(new Vector2(11, 5), 0f);
                if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
            }

            spawner.SpawnProjectileArc(
                spawner.RightPlayer,
                Spawner.ArcDirection.Clockwise,
                new Vector2(2, 12),
                90f,
                projectilesPerArc,
                0.1f,
                5,
                1f
            );

            for (int i = 0; i < kingSlimes; i++) {
                spawner.SpawnSlime(3, new Vector2(-11 + (i * 3), -6), 0f, spawner.RightPlayer);
            }

            if (slimesPerArc > 0 && kingSlimes > 0)
            {
                spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), true);
            }

            yield return new WaitForSeconds(delayTime * 3);

            yield return new WaitForSeconds(delayTime * 2);
        }
        // Wait for the wave to finish
        yield return new WaitForSeconds(numberOfSwarms * delayTime + 5f);
    }
}
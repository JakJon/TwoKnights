using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "ChaoticCorners", menuName = "Waves/Chaotic Corners")]
public class ChaoticCorners : BaseWave
{
    // This is a medium difficult wave that spawns a large conical shape of
    // projectiles, with the opisite corner spawning a swarm of bats. The last wave
    // spawns one giant slime instead of a swarm of bats.


    [Tooltip("Recommended minimum delay time is 3 seconds to ensure proper wave pacing")]
    [SerializeField] private float delayTime = 5f;
    [SerializeField] private int projectilesPerArc = 8;
    [SerializeField] private int batsPerArc = 3;
    [SerializeField] private int kingSlimes = 1;
    [SerializeField] private int slimesPerArc;
    [SerializeField] private int slimesSize = 1;
    [SerializeField] private int arcCount = 5;
    [SerializeField] private float arcDelay = 1f;


    public override IEnumerator SpawnWave(Spawner spawner)
    {
            spawner.SpawnProjectileArc(
                spawner.LeftPlayer,
                Spawner.ArcDirection.CounterClockwise,
                new Vector2(-2, 12),
                90f,
                projectilesPerArc,
                0.1f,
                arcCount,
                arcDelay
            );

            for (int i = 0; i < slimesPerArc; i++)
            {
                spawner.SpawnSlime(slimesSize, new Vector2(11, -5 - i), 0f, spawner.LeftPlayer);
            }

            for (int i = 0; i < batsPerArc; i++)
            {
                spawner.SpawnBat(new Vector2(11, -5), 0f);
                if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
            }

            spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), false);
            yield return new WaitForSeconds(arcDelay * arcCount);

            spawner.SpawnProjectileArc(
                spawner.RightPlayer,
                Spawner.ArcDirection.CounterClockwise,
                new Vector2(2, -12),
                90f,
                projectilesPerArc,
                0.1f,
                arcCount,
                arcDelay
            );

            for (int i = 0; i < slimesPerArc; i++)
            {
                spawner.SpawnSlime(slimesSize, new Vector2(-11, 5 + i), 0f, spawner.RightPlayer);
            }

            for (int i = 0; i < batsPerArc; i++)
            {
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
                arcCount,
                arcDelay
            );

            for (int i = 0; i < batsPerArc; i++)
            {
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
                arcCount,
                arcDelay
            );

            for (int i = 0; i < kingSlimes; i++)
            {
                spawner.SpawnSlime(3, new Vector2(-11 + (i * 3), -6), 0f, spawner.RightPlayer);
            }

            if (slimesPerArc > 0 && kingSlimes > 0)
            {
                spawner.SpawnOrb(new Vector2(0, -6), new Vector2(0, 6), true);
            }

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
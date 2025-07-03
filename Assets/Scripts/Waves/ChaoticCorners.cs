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
    [Tooltip("Number of projectiles per arc")]
    [SerializeField] private int projectilesPerArc = 8;
    [Tooltip("Number of bats per arc phase")]
    [SerializeField] private int batsPerArc = 3;
    [Tooltip("Number of king slimes to spawn at the end of the wave")]
    [SerializeField] private int kingSlimes = 1;

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

            for (int i = 0; i < batsPerArc; i++) {
                spawner.SpawnBat(new Vector2(11, -5), 0f);
                if (i < batsPerArc - 1) yield return new WaitForSeconds(delayTime);
            }

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
            yield return new WaitForSeconds(delayTime * 3);

            yield return new WaitForSeconds(delayTime * 2);
        }
    }
}
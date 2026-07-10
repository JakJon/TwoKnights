using UnityEngine;
using System.Collections;

// Boss wave: spawns the Rat King and hands him his per-asset tuning. Never
// registered in a map's normal wave pool — MapDefinition references it directly
// as gateBoss/trueBoss and the WaveManager schedules it by wave number.
[CreateAssetMenu(fileName = "RatKingWave", menuName = "Waves/Rat King (Boss)")]
public class RatKingWave : BaseWave
{
    [SerializeField] private GameObject ratKingPrefab;
    [SerializeField] private EnemyRatKing.Config config = new EnemyRatKing.Config();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // A breath of silence before the king arrives
        yield return new WaitForSeconds(1.5f);

        var bossObject = Instantiate(ratKingPrefab, new Vector3(0f, 8.5f, 0f), Quaternion.identity);
        var boss = bossObject.GetComponent<EnemyRatKing>();
        if (boss != null)
        {
            boss.Initialize(spawner, config);
        }
        else
        {
            Debug.LogError("[RatKingWave] ratKingPrefab has no EnemyRatKing component!");
        }

        MarkSpawningComplete();
    }
}

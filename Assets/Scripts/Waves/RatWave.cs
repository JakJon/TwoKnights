using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "RatWave", menuName = "Waves/Rat Wave")]
public class RatWave : BaseWave
{
    [System.Serializable]
    private struct RatSpawnData
    {
        public Vector2 position;
        public float delay;
        public bool targetLeftPlayer;
    }

    [SerializeField] private RatSpawnData[] ratSpawns;
    [SerializeField] private float blackRatChance = 0.2f;
    [SerializeField] private float greyRatChance = 0.3f;
    // The remaining chance will be brown rats

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        foreach (var spawnData in ratSpawns)
        {
            // Determine rat type
            float rand = Random.value;
            GameObject ratType;
            if (rand < blackRatChance)
                ratType = spawner.blackRat;
            else if (rand < blackRatChance + greyRatChance)
                ratType = spawner.greyRat;
            else
                ratType = spawner.brownRat;

            Transform target = spawnData.targetLeftPlayer ? spawner.LeftPlayer : spawner.RightPlayer;
            spawner.SpawnRat(spawnData.position, ratType, spawnData.delay, target);
        }

        // Wait for the wave to complete
        float maxDelay = 0f;
        foreach (var spawn in ratSpawns)
            maxDelay = Mathf.Max(maxDelay, spawn.delay);

        yield return new WaitForSeconds(maxDelay + 1f);
    }

    // Example of a custom unlock condition
    //[SerializeField] private int requiredScore = 1000;
    //public override bool CanPlay()
    //{
    //    return base.CanPlay() && GameManager.Instance.Score >= requiredScore;
    //}
}
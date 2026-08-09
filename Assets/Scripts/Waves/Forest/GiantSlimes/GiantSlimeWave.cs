using UnityEngine;
using System.Collections;

// Boss wave: the two giant red slimes, one per knight, plus the conductor that keeps
// them in lockstep. Like RatKingWave this is never in a map's normal pool — the
// MapDefinition points at it as trueBoss and the WaveManager schedules it by number.
[CreateAssetMenu(fileName = "GiantSlimeWave", menuName = "Waves/Giant Red Slimes (Boss)")]
public class GiantSlimeWave : BaseWave
{
    [Header("Prefabs")]
    [SerializeField] private GameObject leftSlimePrefab;
    [SerializeField] private GameObject rightSlimePrefab;
    [Tooltip("Shared by both twins for arc shots, straight shots and wards.")]
    [SerializeField] private GameObject fireballPrefab;

    [Header("Entrance")]
    [Tooltip("Where the RIGHT twin enters, out past the top-right corner; the left twin " +
             "mirrors it on x so the pair is always symmetric.\n\n" +
             "x 13.5 sits just off-frame (frame ends at 10, a scale-6 slime is ~6.1 wide), " +
             "so they crest the corner ~7s in instead of lurking unseen.\n\n" +
             "y is a READABILITY ceiling, not a free dial. The slime is ~5.7 tall with its " +
             "pivot at its feet and its ward orbits its body centre at radius 3.6, so the " +
             "higher it enters the more of that orbit sits above the frame when the ward " +
             "phase opens at half distance — 2.5 costs ~32% of the orbit at that instant " +
             "(clearing as it descends), while a corner-height 5.9 costs ~48%, i.e. half " +
             "the ward unshootable exactly when it must be shot. Raising y barely lengthens " +
             "the path either (the horizontal leg dominates: 11.9 vs 13.2), so the fight's " +
             "length comes from approachSpeed instead.")]
    [SerializeField] private Vector2 spawnCorner = new Vector2(13.5f, 2.5f);
    [Tooltip("A breath of silence before they arrive, matched to the wave banner.")]
    [SerializeField] private float entranceDelay = 1.5f;

    [SerializeField] private EnemyGiantSlime.Config config = new EnemyGiantSlime.Config();
    [SerializeField] private GiantSlimeDuel.AddsConfig adds = new GiantSlimeDuel.AddsConfig();
    [SerializeField] private GiantSlimeDuel.OrbConfig orbs = new GiantSlimeDuel.OrbConfig();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        yield return new WaitForSeconds(entranceDelay);

        if (leftSlimePrefab == null || rightSlimePrefab == null)
        {
            Debug.LogError("[GiantSlimeWave] Slime prefabs are not assigned!");
            MarkSpawningComplete();
            yield break;
        }

        var left = SpawnTwin(leftSlimePrefab, EnemyGiantSlime.Side.Left, spawner.LeftPlayer,
            new Vector2(-spawnCorner.x, spawnCorner.y));
        var right = SpawnTwin(rightSlimePrefab, EnemyGiantSlime.Side.Right, spawner.RightPlayer,
            new Vector2(spawnCorner.x, spawnCorner.y));

        var duel = new GameObject("GiantSlimeDuel").AddComponent<GiantSlimeDuel>();
        // The wave asset's name doubles as the health bar title, as with the Rat King
        duel.Begin(spawner, left, right, config, adds, orbs, WaveName);

        MarkSpawningComplete();
    }

    // Each twin descends diagonally from its own top corner onto the knight it hunts —
    // still a straight line the player can read at a glance, just a longer one than a
    // flank approach, which buys the fight its length.
    private EnemyGiantSlime SpawnTwin(GameObject prefab, EnemyGiantSlime.Side side,
        Transform knight, Vector2 entry)
    {
        var go = Instantiate(prefab, new Vector3(entry.x, entry.y, 0f), Quaternion.identity);

        var slime = go.GetComponent<EnemyGiantSlime>();
        if (slime == null)
        {
            Debug.LogError($"[GiantSlimeWave] {prefab.name} has no EnemyGiantSlime component!");
            return null;
        }

        slime.Initialize(config, fireballPrefab, side, knight);
        return slime;
    }
}

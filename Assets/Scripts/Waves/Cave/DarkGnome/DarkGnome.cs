using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Mine's true boss: the Overseer alone on a full-frame loop, and then not
// alone.
//
// The Millstone was a hundred small things arranged into one problem. This is the
// opposite shape on the same track — ONE thing, with three thousand hit points,
// riding the circuit the whole mine has been riding all map. He enters by himself
// and the fight opens as a duel: a lap you can read, two weapons on two clocks,
// and nothing in the way of your arrows.
//
// What the fight does from there is take the track back off you. Every gear the
// wheel changes into is bought with his health, and each one puts more iron
// between the knights and the only target that matters:
//
//   3000  he rides alone. The player learns the lap.
//   2000  the whistle, the track winds up, and a haul rolls out behind him —
//         gnomes with an empty cart between each and a keg at either end, so the
//         loop that was a firing line becomes a queue you have to shoot through.
//   1000  faster again, and twice the haul.
//    500  he stops, roars off everything on him, and comes back with both
//         weapons doubled.
//
// The hauls ride HIS loop rather than a ring of their own, which is the whole
// point: they are not a second problem, they are cover. A gnome nose to tail in
// front of the Overseer is twenty hit points of iron parked between the player
// and the boss, and the powder at either end of the haul is the way through it —
// the same trade the Millstone taught, asked again at speed.
//
// Everything is fixed, no dice (the no-randomness pillar): the hauls are authored
// slot for slot, the bomb cycle is four and one, and the gears are health, not
// time. Two players who play it the same way meet the same fight.
[CreateAssetMenu(fileName = "DarkGnome", menuName = "Waves/The Overseer (Boss)")]
public class DarkGnome : BaseWave
{
    // One gear the fight changes into, bought with the boss's health. Authored as
    // data because the ladder wants reading all at once — "then this, then this"
    // is a shape, and a shape belongs in a list.
    [System.Serializable]
    public class Haul
    {
        [Tooltip("Authoring only — names the step in the inspector and the dev log")]
        public string label;

        [Tooltip("Fires the first frame the Overseer is at or below this much health.")]
        public float atBossHealth = 2000f;

        [Tooltip("What the whole track runs at from here on, as a multiple of the mine's ordinary speed. The wheel never winds back down.")]
        public float speedScale = 1.5f;

        [Tooltip("One slot per cart, laid nose to tail behind him in this order. Every entry is a real cart — the empty ones between the gnomes are cover, not gaps, so they are authored as the empty cart prefab rather than as nulls.")]
        public List<GameObject> pattern = new List<GameObject>();

        [Tooltip("Blow the whistle as this step fires. It is the only warning the players get that the lap they just learned has stopped being the lap they learned.")]
        public bool whistle = true;
    }

    [Header("Track")]
    [Tooltip("The full-frame loop. One closed run: he rides it, the hauls ride it, and it is the whole board.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs — where he and every haul enter, off-frame.")]
    [SerializeField] private int entryRun = 0;

    [Header("The Overseer")]
    [Tooltip("The black gnome in the gold cart. Needs an EnemyDarkGnomeCart.")]
    [SerializeField] private GameObject bossPrefab;

    [Tooltip("Seconds after the last rail piece lands before he rolls in")]
    [SerializeField] private float entryDelay = 1f;

    [Tooltip("His cart's speed as a multiple of the mine's ordinary track speed. He is faster than everything else on the rails from the first lap, and stays faster: a gear change multiplies every cart by the same ratio, so the gap he opens is kept.")]
    [SerializeField] private float bossSpeedScale = 1.5f;

    [Tooltip("He stops and roars the first frame he is at or below this. Everything on him comes off and both weapons double.")]
    [SerializeField] private float roarAtHealth = 500f;

    [Header("The hauls")]
    [Tooltip("Gears the fight changes into as he is cut down, in order.")]
    [SerializeField] private List<Haul> hauls = new List<Haul>();

    [Header("Orbs")]
    [Tooltip("A three thousand point fight with no orbs in it is a fight the knights cannot recover from a single mistake in.")]
    [SerializeField] private OrbRun orbs = new OrbRun();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        var rails = spawner.Rails;
        if (rails == null || bossPrefab == null)
        {
            Debug.LogWarning("[Overseer] No RailNetwork in the scene, or no boss prefab — there is no loop, so there is no fight.");
            MarkSpawningComplete();
            yield break;
        }

        float layDuration = rails.Lay(railLayout);

        // Opened before he is released and NOT released until he is down: the
        // group is "everything the wave is waiting on", and while it is open the
        // wave cannot end no matter how empty the track looks between hauls.
        BeginAmbush();

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, entryDelay));

        MineCart bossCart = rails.SpawnCart(bossPrefab, entryRun,
                                            MineCart.TrackSpeed * Mathf.Max(0.1f, bossSpeedScale));
        EnemyDarkGnomeCart boss = bossCart != null ? bossCart.GetComponent<EnemyDarkGnomeCart>() : null;
        if (boss == null)
        {
            Debug.LogWarning("[Overseer] The boss prefab has no EnemyDarkGnomeCart — the fight has no boss in it.");
            MarkAmbushReleased();
            MarkSpawningComplete();
            spawner.StopCoroutine(orbRun);
            yield break;
        }

        // The ladder owns the rest of the fight: it watches his health, changes
        // gear, rolls the hauls out and calls the roar. It also owns the release,
        // because the one thing that must be true at the end is that the group is
        // released exactly once, whether he died on the last threshold or through
        // three of them at once.
        yield return spawner.StartCoroutine(RunLadder(spawner, rails, boss));

        yield return WaitForAmbushClear();

        spawner.StopCoroutine(orbRun);
        MarkSpawningComplete();
        yield return null;
    }

    // Watches the boss and nothing else. Steps fire in order and never unfire —
    // several can fall to one volley, and a burst that takes him through two
    // thresholds gets both hauls rather than skipping the one it overshot.
    private IEnumerator RunLadder(Spawner spawner, RailNetwork rails, EnemyDarkGnomeCart boss)
    {
        int next = 0;
        bool roared = false;

        while (boss != null && !boss.IsDead)
        {
            while (next < hauls.Count && boss.GetHealth() <= hauls[next].atBossHealth)
            {
                yield return spawner.StartCoroutine(ReleaseHaul(rails, hauls[next]));
                next++;
            }

            if (!roared && boss.GetHealth() <= roarAtHealth)
            {
                roared = true;
                boss.BeginLastStand();
            }

            yield return null;
        }

        // He is down (or gone). Whatever is still rolling has to be cleared before
        // the wave ends — the hauls are kills the player owes, not scenery.
        MarkAmbushReleased();
    }

    // Lays one haul onto the loop behind him, nose to tail. One cell of track per
    // slot is what makes the carts touch: the spacing is a function of the track
    // and the speed, and a hardcoded figure stops meaning "touching" the moment
    // either changes.
    private IEnumerator ReleaseHaul(RailNetwork rails, Haul haul)
    {
        if (haul == null) yield break;

        rails.SetSpeedScale(Mathf.Max(0.05f, haul.speedScale));
        if (haul.whistle && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.cartWhistle);
        }

        Debug.Log($"[Overseer] '{haul.label}' — the wheel is now at {haul.speedScale:F2}x, " +
                  $"{(haul.pattern != null ? haul.pattern.Count : 0)} cart(s) rolling out.");

        if (haul.pattern == null || haul.pattern.Count == 0) yield break;

        float cell = railLayout != null ? railLayout.CellSize : 1f;
        // Against the gear the track is actually in, or a haul released at 2.25x
        // comes out spaced for 1x and arrives as a train with holes in it
        float speed = MineCart.TrackSpeed * Mathf.Max(0.05f, haul.speedScale);
        float interval = Mathf.Max(0.05f, cell / speed);

        for (int i = 0; i < haul.pattern.Count; i++)
        {
            if (rails.LineCount == 0)
            {
                Debug.LogWarning($"[Overseer] The track went out from under '{haul.label}'.");
                yield break;
            }

            GameObject prefab = haul.pattern[i];
            if (prefab != null) rails.SpawnCart(prefab, Mathf.Clamp(entryRun, 0, rails.LineCount - 1));

            if (i < haul.pattern.Count - 1) yield return new WaitForSeconds(interval);
        }
    }
}

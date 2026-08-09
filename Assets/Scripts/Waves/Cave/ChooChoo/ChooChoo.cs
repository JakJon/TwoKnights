using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Mine's first wave, and the proving ground for the rail system: a track
// slams into place around the knights and the gnomes come out of the dark in
// shifts. Clear a shift and the next one is already rolling in.
//
// The wave is authored as AMBUSHES rather than as a schedule. Each group is
// released onto the loop, and the next one does not set off until every gnome in
// the last one is dead — so the wave's length is set by the knights' shooting and
// not by a stopwatch. That is the whole reason the mine wants it: on a circuit
// nothing ever leaves, so a timed wave either releases into a ring that is
// already full or leaves the knights standing in an empty one.
//
// There is deliberately NO patience on that gate. Dawdling is not free, though —
// the shafts keep spitting rock the entire time a group is alive, so a shift left
// alive is a shift that keeps hitting you, and the only way out of the wave is
// through it.
//
// Kegs and empties ride along with each shift but do not hold it up: they are not
// kills the player owes (see EnemyMineCart), so a group of nothing but powder
// clears the instant it is out. Only gnomes gate a shift.
//
// Every cadence is fixed (no dice — see the no-randomness pillar): carts go out in
// the authored order with the monster crates moved to the back of every shift (see
// GnomesFirst), and the projectile cycle is a four-position rotation —
// above-left, above-right, below-left, below-right — that carries on across the
// whole wave rather than restarting with each shift.
//
// Two numbers govern spacing on a loop, and both are geometry rather than feel:
//   * The release interval is a PERMANENT spacing. Every cart runs at the same
//     speed, so whatever gap it opens is the gap they keep forever. Take it from
//     the lap: the Mine Circuit is 40 units (two 12-unit straights, two 4-unit
//     shafts, and 2 units through each of the four elbows), so at 2.4 u/s a lap is
//     16.7s and six carts want 2.78s to sit evenly around it.
//   * A later shift can be released into a ring that is still busy without carts
//     landing on top of each other, and that is not luck. Carts are released a
//     lead-in BEHIND the mouth (MineCart.leadIn, 6 units) while returning traffic
//     re-enters at the teleport seam a single unit behind it — so a new cart is
//     always at least 5 units clear of whatever is already round, and same speed
//     means it stays that way.
//
// What ends the wave is the gnomes. Empty carts do not gate completion, so a run
// of this wave finishes with the wrecks of every shift still rolling — which is
// why ambushes are kept small: the ring the last shift arrives into is carrying
// every cart the earlier ones left behind.
[CreateAssetMenu(fileName = "ChooChoo", menuName = "Waves/Choo Choo")]
public class ChooChoo : BaseWave
{
    [System.Serializable]
    public class Ambush
    {
        [Tooltip("Authoring only — names the group in the inspector and the dev log")]
        public string label;

        [Tooltip("Released in this order from the mouth of the track. Each needs a MineCart. Only gnomes gate the group; kegs and empties ride along without holding it up.")]
        public List<GameObject> carts = new List<GameObject>();

        [Tooltip("Quiet before this group's first cart — the beat after the last one was cleared. On the opening group it is measured from the track finishing its fall.")]
        public float leadIn = 1f;

        [Tooltip("Seconds between this group's carts. On a looping track this is the spacing they keep forever, so take it from the lap length, not by feel.")]
        public float releaseInterval = 2.78f;

        [Tooltip("Seconds between shots down the shafts while this group is alive. 0 turns the shafts off for the group — which also makes leaving it alive free, so use it sparingly.")]
        public float projectileInterval = 5f;
    }

    [Header("Track")]
    [Tooltip("Laid the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs. Every cart enters here, off-frame, and rolls in — so a circuit has one mouth rather than carts materialising on all four sides at once.")]
    [SerializeField] private int entryRun;

    [Header("Ambushes")]
    [Tooltip("Shifts, in order. Each one waits for the last one's gnomes to be dead before it sets off.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    [Header("Orbs")]
    [Tooltip("Crosses INSIDE the loop by default — an orb outside the ring can only be shot through a gap in the traffic, which is a harsher trade than this wave is asking for.")]
    [SerializeField] private OrbRun orbs = new OrbRun();

    // Where the projectile rotation stands. Reset at the top of every run — SO
    // state outlives the run that set it, and a cycle that carries over would
    // open a different corner each time the wave came up.
    private int _shot;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _shot = 0;

        var rails = spawner.Rails;
        if (rails == null)
        {
            if (railLayout != null)
            {
                Debug.LogWarning("[ChooChoo] No RailNetwork in the scene — the track will not appear.");
            }
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock, and the
        // opening group waits it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        // Beside the wave, never inside it: the shifts are player-paced, so an orb
        // run joined to the spawn phase would stretch or truncate with them
        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush ambush = ambushes[i];
            if (ambush == null || ambush.carts == null || ambush.carts.Count == 0) continue;

            BeginAmbush();

            // Started before the group is even out, so the shafts are already
            // working while the first cart rolls in
            Coroutine shafts = ambush.projectileInterval > 0f
                ? spawner.StartCoroutine(WorkTheShafts(spawner, ambush.projectileInterval))
                : null;

            yield return spawner.StartCoroutine(Release(rails, ambush, layDuration));
            layDuration = 0f; // only the opening group waits out the track

            yield return WaitForAmbushClear();

            if (shafts != null) spawner.StopCoroutine(shafts);
        }

        // Orbs already on the board finish their crossing on their own; what stops
        // here is any that had not been released yet
        spawner.StopCoroutine(orbRun);

        MarkSpawningComplete();
        yield return null;
    }

    // One cart at a time, all from the same mouth. They are NOT dealt across the
    // layout's runs: on a circuit every run is the same piece of track a lap
    // apart, so spreading them would have carts appearing in the middle of the
    // board on all four sides at once instead of driving in from off-frame.
    private IEnumerator Release(RailNetwork rails, Ambush ambush, float trackDelay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, ambush.leadIn));

        float interval = Mathf.Max(0.1f, ambush.releaseInterval);
        List<GameObject> order = GnomesFirst(ambush.carts);

        for (int i = 0; i < order.Count; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[ChooChoo] The track went out from under the wave — the rest of the shift stays underground.");
                break;
            }

            int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(order[i], line, MineCart.TrackSpeed);

            if (i < order.Count - 1) yield return new WaitForSeconds(interval);
        }

        // Break out above and this still runs, which is the point: a group that is
        // never marked released can never read as clear, and the wave would hang
        // on a shift that no longer exists.
        MarkAmbushReleased();
    }

    // Every monster crate in a shift goes out AFTER every gnome in it, whatever
    // order they were authored in. Enforced here rather than left to the author
    // because it is a rule about the fight, not a preference about the list: the
    // gnomes are what gates the shift, so a crate released ahead of them arrives
    // at its flag while the group is still being cleared and the player is asked to
    // answer the delivery and the riders on the same beat. Sent last, the crate
    // comes down into a shift the knights are already on top of, and the race for
    // the flag is a decision they can actually make.
    //
    // Relative order is otherwise preserved on both sides of the split — kegs and
    // empties keep whatever slot they were authored into, and crates keep their own
    // running order — so this reshuffles a shift no more than it has to.
    private static List<GameObject> GnomesFirst(List<GameObject> carts)
    {
        var ordered = new List<GameObject>();
        var crates = new List<GameObject>();

        for (int i = 0; i < carts.Count; i++)
        {
            GameObject prefab = carts[i];
            if (prefab == null) continue;

            if (prefab.GetComponent<EnemyDeliveryCart>() != null) crates.Add(prefab);
            else ordered.Add(prefab);
        }

        ordered.AddRange(crates);
        return ordered;
    }

    // Works the shafts for as long as the group it was started for is alive. Self
    // terminating as well as explicitly stopped, so a wave cut short cannot leave
    // it running on the Spawner.
    private IEnumerator WorkTheShafts(Spawner spawner, float interval)
    {
        float wait = Mathf.Max(0.1f, interval);

        while (!IsAmbushClear())
        {
            SpawnShot(spawner);
            yield return new WaitForSeconds(wait);
        }
    }

    // Each shot drops straight down (or straight up) onto its own knight, so a
    // volley never crosses the other knight's guard
    private void SpawnShot(Spawner spawner)
    {
        switch (_shot++ % 4)
        {
            case 0:
                spawner.SpawnProjectile(spawner.LeftPlayer, spawner.aboveLeftPlayer);
                break;
            case 1:
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.aboveRightPlayer);
                break;
            case 2:
                spawner.SpawnProjectile(spawner.LeftPlayer, spawner.belowLeftPlayer);
                break;
            default:
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.belowRightPlayer);
                break;
        }
    }
}

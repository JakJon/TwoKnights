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

    [Header("The gear change")]
    [Tooltip("What the whole loop gears up to once half the shift's gnomes are down. One change, once, for the rest of the wave: the players earn a faster track by clearing it, and the second half of every shift is fought at that speed.")]
    [SerializeField] private float speedUpScale = 1.35f;

    [Tooltip("Two blasts off the mine whistle on the gear change — the same announcement the Millstone makes, and the only warning the players get that the track just got quicker.")]
    [SerializeField] private bool whistleOnSpeedUp = true;

    [Header("The guard")]
    [Tooltip("Stones in the ring one rider of every shift comes out wearing. 0 sends the shift out bare. This is the tier dial and it is a dial over the GAPS, not over hit points: evenly spaced on a 0.9-unit ring, four stones leave gaps over a unit wide and eleven leave a quarter of one. House ladder across the four tiers: 4 / 6 / 8 / 11.")]
    [SerializeField] private int wardStones;

    [Tooltip("The rock the guard is made of. Read for its sprite and its hitbox only — the ring builds its own stones, because a rock prefab registers with wave tracking and eleven of them circling a gnome would hold the wave open forever.")]
    [SerializeField] private GameObject wardStonePrefab;

    [Tooltip("How far off the rider the ring sits. Wide enough to be a barrier in front of him rather than a texture on him; narrow enough that a knight who has closed the range can still see which gap is coming.")]
    [SerializeField] private float wardRadius = 0.9f;

    [Tooltip("Degrees per second the guard turns. This is the clock the player is reading — 120 is a full turn every three seconds, which is slow enough to time a shot into a gap and quick enough that the gap does not wait for you.")]
    [SerializeField] private float wardSpin = 120f;

    [Tooltip("Seconds before a broken stone is back in its own slot. Its own slot, so grinding the ring down never widens the gaps — which is what keeps threading them the real answer.")]
    [SerializeField] private float wardRegrow = 4f;

    [Header("Rock")]
    [Tooltip("Cycled in order as the shafts work. This used to be one rock at a time round four fixed points, which is a metronome rather than a pattern — the guard learned it in one wave and never had to read it again. Each ambush still says HOW OFTEN it fires, below.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("Crosses INSIDE the loop by default — an orb outside the ring can only be shot through a gap in the traffic, which is a harsher trade than this wave is asking for.")]
    [SerializeField] private OrbRun orbs = new OrbRun();

    // Where the projectile rotation stands. Reset at the top of every run — SO
    // state outlives the run that set it, and a cycle that carries over would
    // open a different corner each time the wave came up.
    private int _shot;

    // Every gnome rider this wave has put on the track. Entries go null as they
    // die, which is how the gear change knows half the shift is down. Cleared at
    // the top of the wave — the asset outlives the run.
    private readonly List<GameObject> _riders = new List<GameObject>();
    private bool _gearedUp;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _shot = 0;
        _riders.Clear();
        _gearedUp = false;

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

        // Watches the whole wave rather than one shift: "half the gnomes" is a
        // claim about the shift the wave is going to work through, so the gear
        // change lands once, wherever in the wave that half falls.
        Coroutine gearbox = spawner.StartCoroutine(WatchForHalfDown(rails, PlannedGnomes()));

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
        if (gearbox != null) spawner.StopCoroutine(gearbox);

        MarkSpawningComplete();
        yield return null;
    }

    // How many gnomes the wave will release across every shift. Counted up front
    // from what is authored, so the change fires halfway through the SHIFT and
    // not halfway through whichever group happens to be on the track.
    private int PlannedGnomes()
    {
        int total = 0;
        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush ambush = ambushes[i];
            if (ambush == null || ambush.carts == null) continue;
            for (int c = 0; c < ambush.carts.Count; c++)
            {
                GameObject prefab = ambush.carts[c];
                if (prefab != null && prefab.GetComponent<EnemyGnomeCart>() != null) total++;
            }
        }
        return total;
    }

    private IEnumerator WatchForHalfDown(RailNetwork rails, int planned)
    {
        if (rails == null || planned <= 0 || speedUpScale <= 1f) yield break;

        int half = Mathf.CeilToInt(planned * 0.5f);

        while (!_gearedUp)
        {
            int down = 0;
            for (int i = 0; i < _riders.Count; i++)
            {
                if (_riders[i] == null) down++;
            }

            if (down >= half)
            {
                _gearedUp = true;
                rails.SetSpeedScale(speedUpScale);
                if (whistleOnSpeedUp && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.cartWhistle);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ChooChoo] {down} of {planned} gnome(s) down — the loop is now at {speedUpScale:F2}x.");
#endif
                yield break;
            }

            yield return null;
        }
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
        bool guarded = false;

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
            MineCart cart = rails.SpawnCart(order[i], line, MineCart.TrackSpeed);

            // Only riders count toward the gear change: kegs and empty wrecks
            // are hazards on the loop, not the shift the players are clearing.
            if (cart != null && cart.GetComponent<EnemyGnomeCart>() != null)
            {
                _riders.Add(cart.gameObject);

                // One guarded rider per shift, and he is the FIRST one out. Not a
                // random one and not all of them: the ring is a question the
                // player has to have time to read, so it arrives on the cart that
                // is on screen by itself, and by the time the rest of the shift is
                // round the answer is already known. GnomesFirst has already put
                // the riders at the front, so "first cart with a gnome on it" and
                // "first gnome" are the same slot.
                if (!guarded && wardStones > 0)
                {
                    guarded = true;
                    GnomeWardRing.Attach(cart.gameObject, wardStonePrefab, wardStones,
                                         wardRadius, wardSpin, wardRegrow);
                }
            }

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
        if (volleys == null || volleys.Count == 0) yield break;

        float wait = Mathf.Max(0.1f, interval);

        // The ambush is the clock, not a window: the shafts work for exactly as
        // long as the shift on the loop is alive, so leaving a gnome up is what
        // keeps the rock coming. The ambush's own interval stands in for each
        // volley's restAfter, which is how a tier says the shafts got busier
        // without re-authoring every shape.
        while (!IsAmbushClear())
        {
            RockVolley volley = volleys[_shot++ % volleys.Count];
            float busy = RockVolley.Fire(spawner, volley, rockSpeed);
            yield return new WaitForSeconds(busy + wait);
        }
    }

}

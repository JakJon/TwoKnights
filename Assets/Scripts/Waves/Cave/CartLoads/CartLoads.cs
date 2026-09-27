using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The same four shafts as Bits and Pieces, and nothing on them but freight.
//
// Where that wave is a puzzle about which arrow to spend, this one is a haulage
// schedule you are trying to break. Both edges run monster crates and only
// monster crates — no powder, no empties, nothing riding along to absorb a shot
// you meant for something else. Every cart on the board is a delivery in progress
// and every one of them is stoppable, so there is no wasted arrow available and
// no decision to agonise over: there is only whether you are shooting fast enough.
//
// Crates go out ONE AT A TIME, alternating lines — red, a pause, green, a pause,
// and on down the consignment. Never both on the same beat. On a wave whose whole
// demand is interception that is not a presentation detail: two crates entering
// together are one decision the player cannot split, and the alternation is what
// turns the consignment into a queue of separate, answerable arrivals. When one
// line runs out the other carries on at the same pause.
//
// That is also why it is NOT authored as ambushes. A shift that waits for you is a
// wave that promises the pressure will pause; this one promises the opposite, and
// the promise has to be kept in the structure rather than in the tuning. The
// crates go out on a fixed cadence from the first second to the last, and the wave
// ends when the freight is dealt with — either shot on the descent, or fought on
// the floor after it has been let out.
//
// The route is what makes each crate a race with a shape, and it runs UPWARD. A
// crate comes up out of the dark at the FOOT of a left shaft, climbs the full
// height past the knights where it is plainly readable, wraps round to the foot
// of the matching RIGHT shaft, and climbs again to unload at the flag standing
// beside it — so every crate is last seen heading for the top right corner, which
// is where the mine's freight is going. Which flag is the tier:
//
//   * Low drop (tiers 1-2): both flags sit near the TOP of the right climb, so the
//     player gets the whole left climb and nearly all of the right one — around
//     nine seconds of open shot per crate. Miss and it was a miss.
//   * High drop (tiers 3-4): the OUTER flag is three units up the right climb,
//     barely above where the crate reappears, so that line has to die on the left
//     or it delivers. The INNER flag stands level with the knights, half way up,
//     which is the one concession the tier makes: same carts, same cadence, and
//     the two shafts no longer offer the same road.
//
// Nothing rolls dice (see the no-randomness pillar): the crate kinds come off a
// rotation that carries across the wave and resets at the top of every run, and
// the cadence is fixed. Note that a pause is a PERMANENT spacing on a loop —
// alternating lines leaves consecutive crates on ONE line 4.8 x pause units apart
// on a 36-unit lap, so a line of more than 36 / (4.8 x pause) crates laps itself
// and starts to bunch at the mouth. At a 2s pause that ceiling is under four.
[CreateAssetMenu(fileName = "CartLoads", menuName = "Waves/Cart Loads")]
public class CartLoads : BaseWave
{
    // Which run of Mine Twin Shafts each loop enters on. Not authored: the layout
    // is fixed for this wave and a run index is not a design decision anyone can
    // read, it is an index into a list. Retune the wave by the counts below.
    private const int OuterEntryRun = 0; // the outer pair of shafts, x = +/-9
    private const int InnerEntryRun = 2; // the inner pair, x = +/-8

    [Header("Track")]
    [Tooltip("Two touching shafts up each edge, with a drop flag on each right ascent. Swap the low-drop layout for the high-drop one to shorten the interception window without touching a single count.")]
    [SerializeField] private RailLayout railLayout;

    [Header("Freight")]
    [Tooltip("Cycled in order as the crate slots on BOTH shafts are filled. Each needs an EnemyDeliveryCart carrying ONE passenger — a second one strands the crate, because a delivery cart serves a given flag only once and would then circle for the rest of the wave still loaded, still holding the wave open.")]
    [SerializeField] private List<GameObject> crateCarts = new List<GameObject>();

    [Tooltip("Crates sent up the OUTER shaft over the whole wave")]
    [SerializeField] private int outerShaftCrates = 5;

    [Tooltip("Crates sent up the INNER shaft over the whole wave")]
    [SerializeField] private int innerShaftCrates = 5;

    [Header("Cadence")]
    [Tooltip("Quiet before the first crate, measured from the track finishing its fall")]
    [SerializeField] private float leadIn = 1f;

    [Tooltip("Seconds between one crate and the next, alternating red then green. This is a PERMANENT spacing on a loop: consecutive crates on one line end up 4.8 x this apart on a 36-unit lap, so keep it under 36 / (4.8 x crates per line) or the line laps itself.")]
    [SerializeField] private float releasePause = 1.8f;

    [Header("Rock")]
    [Tooltip("Volleys down the shafts, cycled in order for the window below. The mine had only three waves that threw a rock at all, which left the guard — half of what a knight is — idle through the rest. This is the second question a wave asks while the first one is still standing.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Length of the firing window, from the start of the wave. New volleys stop being issued once it elapses; whatever is already in the air still falls.")]
    [SerializeField] private float projectileWindow = 24f;

    [Tooltip("World units per second for THIS wave's rock, overriding the prefab's 1. A shaft is seven units up, so 1.75 puts a rock on a knight in four seconds instead of seven. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("The flanks")]
    [Tooltip("Rats dropped in off the top and bottom edges. Both shafts run up the EDGES of the board, so a guard watching one is turned hard sideways — these arrive in the half of the dial that guard has given up. Empty on the opening tier.")]
    [SerializeField] private List<RatFlank> flanks = new List<RatFlank>();

    [Tooltip("Once the authored consignment is spent, the shafts keep feeding while a flank rat is still alive, so leaving the vermin for later means answering freight for as long as you take over them.")]
    [SerializeField] private bool freightWaitsOnTheVermin = true;

    [Header("Orbs")]
    [Tooltip("The high crossing, over the top of the fight. Runs clear of both rails at y=4.3.")]
    [SerializeField] private OrbRun highOrbs = new OrbRun
    {
        count = 1, firstAt = 12f, interval = 14f,
        from = new Vector2(-12f, 4.3f), to = new Vector2(12f, 4.3f)
    };

    [Tooltip("The low crossing, under it. Runs the other way at y=-5 so the two lines are never the same shot twice.")]
    [SerializeField] private OrbRun lowOrbs = new OrbRun
    {
        count = 1, firstAt = 24f, interval = 14f,
        from = new Vector2(12f, -5f), to = new Vector2(-12f, -5f)
    };

    // Where the crate rotation stands. Reset at the top of every run — SO state
    // outlives the run that set it, and a rotation that carried over would deal a
    // different consignment each time the wave came up.
    private int _crate;

    // Every rat this wave put out. Cleared at the top of the wave — the asset is
    // a ScriptableObject and outlives the run.
    private readonly List<GameObject> _vermin = new List<GameObject>();

    // False until every flank has finished arriving. Without it an empty
    // roster during a group's lead-in reads as "the vermin are dead".
    private bool _verminReleased;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _crate = 0;
        _vermin.Clear();
        _verminReleased = false;

        var rails = spawner.Rails;
        if (rails == null)
        {
            if (railLayout != null)
            {
                Debug.LogWarning("[CartLoads] No RailNetwork in the scene — there are no shafts to run.");
            }
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock, and the
        // first crates wait it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        Coroutine highRun = spawner.StartCoroutine(highOrbs.Release(spawner));
        Coroutine lowRun = spawner.StartCoroutine(lowOrbs.Release(spawner));

        // Both consignments are dealt off the rotation before anything moves, the
        // outer shaft first and then the inner, so the running order is fixed at
        // authoring time rather than by which release happened to come up first.
        List<GameObject> outerLine = BuildConsignment(outerShaftCrates);
        List<GameObject> innerLine = BuildConsignment(innerShaftCrates);

        // There is no ambush gate here to hold the wave open, so
        // MarkSpawningComplete is the ONLY thing standing between a half-released
        // consignment and a wave that decides it is finished — it must not run
        // until every crate is actually out.
        Coroutine vermin = spawner.StartCoroutine(ReleaseTheFlanks(spawner));
        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner));

        yield return spawner.StartCoroutine(Release(rails, outerLine, innerLine, layDuration));

        // Every crate is out: if the orb timer has not paid yet, it pays now,
        // with the crates still on the track. See OrbRun.SendFirstIfWaiting.
        highOrbs.SendFirstIfWaiting(spawner);

        yield return vermin;
        yield return shafts;

        spawner.StopCoroutine(highRun);
        spawner.StopCoroutine(lowRun);

        // Everything the wave owes the player is on the track by now. Tracking
        // carries it the rest of the way: a loaded crate gates until it is shot or
        // emptied, and whatever it let out gates until it is dead.
        MarkSpawningComplete();
        yield return null;
    }

    // One line's crates, in running order. A slot whose prefab is missing is
    // dropped rather than left as a hole: the count is the contract here, and a
    // silent gap in the cadence reads as a release that misfired.
    private List<GameObject> BuildConsignment(int count)
    {
        var line = new List<GameObject>();
        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            GameObject prefab = NextOf(crateCarts, ref _crate);
            if (prefab != null) line.Add(prefab);
        }
        return line;
    }

    // One crate at a time, outer first, then inner, then outer again. Never both
    // at once — see the header: on a wave that is entirely about interception, two
    // arrivals on the same beat are one decision the player cannot split.
    //
    // Crates are NOT dealt across the layout's other runs. The two runs of a loop
    // are the same stretch of track half a lap apart, so spreading them would have
    // crates appearing on both edges of the board at once instead of driving in
    // from off-frame.
    private IEnumerator Release(RailNetwork rails, List<GameObject> outer, List<GameObject> inner,
                                float trackDelay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, leadIn));

        float pause = Mathf.Max(0.1f, releasePause);
        int o = 0;
        int n = 0;
        bool outersTurn = true;

        while (o < outer.Count || n < inner.Count)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[CartLoads] The shafts went out from under the wave — the rest of the consignment stays underground.");
                yield break;
            }

            // Whoever's turn it is, unless they have run dry — then the other line
            // simply carries on at the same pause rather than leaving a hole in it
            bool useOuter = outersTurn ? o < outer.Count : n >= inner.Count;

            int run = Mathf.Clamp(useOuter ? OuterEntryRun : InnerEntryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(useOuter ? outer[o++] : inner[n++], run, MineCart.TrackSpeed);

            outersTurn = !outersTurn;

            if (o < outer.Count || n < inner.Count) yield return new WaitForSeconds(pause);
        }

        yield return KeepFeedingWhileVerminLive(rails, pause);
    }

    // The consignment is spent but the vermin are not: the shafts keep running.
    // This is what makes the flank a decision rather than a distraction — every
    // second the rats are left alive is another crate to answer, so "clear the
    // rats first" costs freight and "clear the freight first" costs nothing but
    // never ends.
    private IEnumerator KeepFeedingWhileVerminLive(RailNetwork rails, float pause)
    {
        if (!freightWaitsOnTheVermin) yield break;

        bool outersTurn = true;
        while (!_verminReleased || !RatFlank.AllDead(_vermin))
        {
            // The wait comes first: the authored consignment's last crate has
            // only just gone out, and a sustain crate on the same frame would
            // read as the wave hiccupping rather than as it carrying on.
            yield return new WaitForSeconds(pause);
            if (rails.LineCount == 0) yield break;

            GameObject prefab = NextOf(crateCarts, ref _crate);
            if (prefab == null) yield break;

            int run = Mathf.Clamp(outersTurn ? OuterEntryRun : InnerEntryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(prefab, run, MineCart.TrackSpeed);
            outersTurn = !outersTurn;
        }
    }

    private IEnumerator ReleaseTheFlanks(Spawner spawner)
    {
        yield return null;
        if (flanks == null || flanks.Count == 0)
        {
            _verminReleased = true;
            yield break;
        }

        var running = new List<Coroutine>();
        for (int i = 0; i < flanks.Count; i++)
        {
            if (flanks[i] == null || flanks[i].Total == 0) continue;
            running.Add(spawner.StartCoroutine(flanks[i].Release(spawner, _vermin)));
        }

        for (int i = 0; i < running.Count; i++) yield return running[i];
        _verminReleased = true;
    }

    // Round the authored list, carrying the cursor across the whole wave so that
    // the two lines are not the same consignment twice.
    private static GameObject NextOf(List<GameObject> list, ref int cursor)
    {
        if (list == null || list.Count == 0) return null;
        return list[cursor++ % list.Count];
    }

    // Beside the wave, never inside it: the window is a fixed number of seconds
    // from the start, so it neither stretches nor truncates with how fast the
    // players clear whatever else is on the board.
    private IEnumerator WorkTheShafts(Spawner spawner)
    {
        yield return RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed);
    }
}

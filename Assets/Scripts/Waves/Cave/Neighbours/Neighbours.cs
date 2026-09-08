using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Two haulage roads across the board, crossing over each other, and NEITHER OF
// THEM IS CARRYING ANYTHING FOR YOU.
//
// A crate enters off-frame on one side, spends its whole run crossing the near
// knight's patch, and unloads at a flag planted on the FAR knight's doorstep.
// Then a second road does the same thing in the other direction. So the load
// going past your shoulder is not your problem — it is your partner's — and the
// one coming for you is behind their back, out where only they can see it in
// time.
//
// That is the whole wave, and it is the one thing this game can ask that no
// single-player game can. The knights cannot move and cannot cover for each
// other in the ordinary way: shield facing IS shooting direction, so a guard
// pointed at the thing about to land on you is a guard pointed away from the
// thing about to land on THEM. Everything else in the mine asks each player to
// solve their own half of the board. This asks them to solve the other one.
//
// THE PACK IS WHAT MAKES IT COST SOMETHING, and the pack runs the roads too.
// A wolf comes in off-frame at one end of a haulage road and lopes the whole
// length of it on foot, just outside the rail — above the upper road, below the
// lower one — and only turns on a knight once it has run out of track.
//
// It runs AGAINST the freight on its road, and that is the arrangement the whole
// wave hangs on. On the upper road the crates head east to the right knight's
// flag while the wolves head west to the left knight; on the lower road it is
// the mirror. So each road carries one of everything, going opposite ways, and
// they pass each other in front of the players. The thing crossing your patch
// left to right is your partner's; the thing crossing it right to left is yours.
// Turn inward to save them and the wolf lands unanswered; turn outward to save
// yourself and the crate reaches the flag. The wave never resolves that; it just
// keeps asking.
//
// Running the rail rather than circling is also what buys the wolf its exposure.
// A beast that came straight in off the edge would be on a knight in under three
// seconds — less time than three arrows take — and a wave whose only answer is
// to eat it on the shield is a wave with no answer (see the rule about
// shield-absorbing enemies). The full length of a road is a little over six
// seconds on screen before it even starts to turn in.
//
// The opening tiers buy MORE of it by sending the wolf over the same road more
// than once: it reaches the end, turns on the spot, and lopes back the way it
// came before it turns again (courseReversals). Three passes on tier one is
// twenty seconds of animal out in the open with nothing in front of it but
// arrows, which is the shape a first tier should have — the threat is legible,
// it is slow, and it is on the wrong side of the rail the whole time. The ladder
// is that patrol getting shorter, and then the road itself getting shorter after
// it: three passes, two, one, one.
//
// One consequence worth stating, because it changes what the wave asks. An ODD
// number of turns leaves the wolf back at the end it came IN at, so it charges
// the knight whose freight shares its road rather than the one across the board.
// Tier two is the tier where that is true, and it is a real difference: for that
// one tier a road is entirely one player's problem instead of being split.
//
// It is deliberately NOT authored as ambushes. A group that waits for you is a
// wave promising a pause, and a wave about not having time for both jobs cannot
// make that promise. The consignment runs on a fixed cadence from the first
// second to the last, and the wave ends when the freight is dealt with — either
// stopped on the road, or fought on the floor after it has been let out.
//
// The geometry, which is why the crates are readable at all:
//   * The roads run at y = +3 and y = -3, clear of the knights and clear of the
//     shields, so a crate is in open sight for its whole crossing rather than
//     lost in the middle of the fight.
//   * A crate is on screen for about six seconds before its flag, and four of
//     those are on the NEAR knight's side of the middle. One crate is two arrows
//     of iron, so the window is real but not generous — and the near knight has
//     their own pack circling while it burns.
//   * The flag stands two units short of the far knight, so once a crate crosses
//     the middle that knight has under two seconds. There is no late save; the
//     near knight was the save.
//   * One honest asymmetry: a delivery cart always tips its load DOWNWARD (see
//     EnemyDeliveryCart's drop offset), so the upper road sets its cargo down
//     about three units from the right knight and the lower road about four and
//     a half from the left. The decision the wave is asking about is identical on
//     both roads — stop it or wear it — but the left knight gets a breath more
//     once it has landed.
//
// Nothing rolls dice (see the no-randomness pillar): the crate kinds come off a
// rotation, the roads alternate strictly, the pack alternates sides and types.
// Every rotation resets at the top of the run, because a wave asset's state
// outlives the run that set it.
[CreateAssetMenu(fileName = "Neighbours", menuName = "Waves/Neighbours")]
public class Neighbours : BaseWave
{
    [Header("Track")]
    [Tooltip("Two open roads crossing the board in opposite directions, each with a drop flag planted beside the knight it is NOT protecting. Mine Crosshaul is the layout this is tuned against.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs: the road heading EAST, whose flag stands beside the right knight. Every crate on it is the left knight's responsibility.")]
    [SerializeField] private int eastboundRun;

    [Tooltip("Index into the layout's runs: the road heading WEST, whose flag stands beside the left knight. Every crate on it is the right knight's responsibility.")]
    [SerializeField] private int westboundRun = 1;

    [Header("The consignment")]
    [Tooltip("Cycled in order as the crates go out, and carried across the whole wave. Each needs an EnemyDeliveryCart carrying ONE passenger — a second one would have the same crate serve the second flag on its way past, which is the other knight's problem twice and nobody's decision.")]
    [SerializeField] private List<GameObject> crates = new List<GameObject>();

    [Tooltip("How many go out over the wave. Each one is a separate question, so this is also how many times the wave asks it.")]
    [SerializeField] private int crateCount = 4;

    [Tooltip("Seconds between one crate entering and the next. They alternate roads strictly, so a crate on the SAME road as the last is two of these behind it.")]
    [SerializeField] private float crateInterval = 5f;

    [Tooltip("Quiet between the track finishing its fall and the first crate entering")]
    [SerializeField] private float firstCrateAt = 1.5f;

    [Header("The pack")]
    [Tooltip("Wolves over the wave, alternating roads. Even ones run the upper road westward and turn on the LEFT knight; odd ones run the lower road eastward and turn on the RIGHT knight — the opposite way to the freight on the same road, so each road carries one of everything.")]
    [SerializeField] private int wolves = 4;

    [Tooltip("Seconds between one wolf entering and the next")]
    [SerializeField] private float wolfInterval = 6f;

    [Tooltip("Quiet before the first wolf. Held back past the first crate on purpose: the wave states the haulage problem first and then takes away the time to solve it.")]
    [SerializeField] private float firstWolfAt = 6f;

    [Tooltip("Cycled in order as wolves are released, and carried across the whole wave.")]
    [SerializeField] private List<WolfType> packOrder = new List<WolfType> { WolfType.Brown };

    [Tooltip("How far along the road a wolf runs before it turns on its knight. Mine Crosshaul's roads are 26 units plus the lead-in, so 28 runs from off-frame at one end to the last VISIBLE rail at the other. Going the full 32 walks it off the far edge and makes it come back, which reads as a bug. THE TIER DIAL: shortening it turns the wolf in earlier and buys the players less time.")]
    [SerializeField] private float courseDistance = 28f;

    [Tooltip("How far outside the rail the pack runs — above the upper road, below the lower one. Big enough to read as beside the track rather than inside the crates, small enough to stay well on screen.")]
    [SerializeField] private float courseOffset = 1.2f;

    [Tooltip("How far off-frame the pack joins the road. Matches MineCart's own lead-in so a wolf appears from exactly where a crate would have.")]
    [SerializeField] private float courseLeadIn = 6f;

    [Tooltip("How many times a wolf turns on the spot at the end of the road and runs it again before it charges. 0 is one pass. THE OTHER TIER DIAL, and the gentle one: courseDistance shortens the road and takes time away from the players, while this lengthens the wolf's stay on it and gives time back — the animal is out on the rail, in the open, being shot at, for as long as it lasts. Mind the parity: an ODD count leaves the wolf at the end it entered, so it turns on the knight whose freight shares its road instead of the one it was walking towards.")]
    [SerializeField] private int courseReversals;

    [Header("The flanks")]
    [Tooltip("Rats dropped in off the top and bottom edges, away from both roads. The roads are a horizontal problem and every answer to them is a horizontal shot; this is what stops the wave being solved by pointing along the rail and holding. Empty on the opening tier.")]
    [SerializeField] private List<RatFlank> flanks = new List<RatFlank>();

    [Header("Rock")]
    [Tooltip("Volleys down the shafts, cycled in order for the window below. The mine had only three waves that threw a rock at all, which left the guard — half of what a knight is — idle through the rest. This is the second question a wave asks while the first one is still standing.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Length of the firing window, from the start of the wave. New volleys stop being issued once it elapses; whatever is already in the air still falls.")]
    [SerializeField] private float projectileWindow = 24f;

    [Tooltip("World units per second for THIS wave's rock, overriding the prefab's 1. A shaft is seven units up, so 1.75 puts a rock on a knight in four seconds instead of seven. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Ogres")]
    [Tooltip("Brutes walking in from the edges, alternating sides. They ignore everything this wave is about and come straight for whichever knight they entered nearest — see OgreBand. Leave the count at 0 for a tier that should not have any.")]
    [SerializeField] private OgreBand ogres = new OgreBand();

    // Every ogre this wave put out. Cleared as the band is released — the asset
    // is a ScriptableObject and outlives the run.
    private readonly List<GameObject> _ogres = new List<GameObject>();

    [Header("Orbs")]
    [Tooltip("Crosses the middle band by default, between the two roads — the one stripe of the board no crate occupies. An orb on y=3 or y=-3 is an arrow the freight eats.")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, 0.5f), to = new Vector2(12f, 0.5f) };

    // Where the rotations stand. Reset at the top of every run — SO state
    // outlives the run that set it.
    private int _crate;
    private int _wolf;

    // Every rat this wave put out, so a flank can be waited on. Cleared at the
    // top of the wave: the asset is a ScriptableObject and outlives the run.
    private readonly List<GameObject> _vermin = new List<GameObject>();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _crate = 0;
        _wolf = 0;
        _vermin.Clear();

        var rails = spawner.Rails;
        if (rails == null && railLayout != null)
        {
            Debug.LogWarning("[Neighbours] No RailNetwork in the scene — there are no roads, so this is the pack alone.");
        }

        // Fire-and-forget: the cascade runs on the network's own clock. Only the
        // freight waits it out — the pack is timed from the same instant so that
        // the two schedules stay in the relationship they were authored in.
        float layDuration = rails != null ? rails.Lay(railLayout) : 0f;

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        // Run together rather than one after the other: the whole wave is the
        // overlap between them, so neither may be allowed to finish first by
        // construction.
        Coroutine haul = spawner.StartCoroutine(RunTheHaul(rails, layDuration));
        Coroutine pack = spawner.StartCoroutine(ReleaseThePack(spawner, rails, layDuration));
        Coroutine vermin = spawner.StartCoroutine(ReleaseTheFlanks(spawner));
        Coroutine brutes = spawner.StartCoroutine(ReleaseTheOgres(spawner));
        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner));

        yield return haul;
        yield return pack;
        yield return vermin;
        yield return brutes;
        yield return shafts;

        // Safe to mark the moment both schedules are done: everything this wave
        // spawns registers with tracking inside its own Awake, which Instantiate
        // runs before SpawnCart or SpawnWolf even return. Nothing is still queued
        // behind a delay. (Bats would be the exception — Spawner.SpawnBat yields
        // a frame before instantiating even at zero delay — and this wave has
        // none.)
        MarkSpawningComplete();

        // Guarded because StartCoroutine hands back null for an enumerator that
        // finishes without ever yielding — see OrbRun.Release.
        if (orbRun != null) spawner.StopCoroutine(orbRun);
    }

    // The consignment. Strictly alternating roads, on a cadence that never
    // pauses for anybody.
    private IEnumerator RunTheHaul(RailNetwork rails, float trackDelay)
    {
        yield return null;

        int count = Mathf.Max(0, crateCount);
        if (rails == null || count == 0 || crates == null || crates.Count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, firstCrateAt));

        for (int i = 0; i < count; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and a run index would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[Neighbours] The roads went out from under the wave — the rest of the consignment stays underground.");
                yield break;
            }

            // Never both on one beat. On a wave whose whole demand is
            // interception that is not presentation: two crates entering together
            // are one decision the players cannot split, and the alternation is
            // what keeps the consignment a queue of separate, answerable arrivals.
            int run = (i % 2 == 0) ? eastboundRun : westboundRun;
            run = Mathf.Clamp(run, 0, rails.LineCount - 1);

            GameObject prefab = NextOf(crates, ref _crate);
            if (prefab != null) rails.SpawnCart(prefab, run, MineCart.TrackSpeed);

            if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.1f, crateInterval));
        }
    }

    // The pack, one wolf at a time, alternating roads.
    private IEnumerator ReleaseThePack(Spawner spawner, RailNetwork rails, float trackDelay)
    {
        yield return null;

        int count = Mathf.Max(0, wolves);
        if (rails == null || count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, firstWolfAt));

        // Both roads are resolved up front. They do not change over the wave, and
        // once a course exists the wolves no longer need the track at all — they
        // follow world points — so nothing downstream has to re-check that the
        // rails are still standing.
        List<Vector2> upper = BuildCourse(rails, eastboundRun);
        List<Vector2> lower = BuildCourse(rails, westboundRun);

        if (upper.Count == 0 && lower.Count == 0)
        {
            Debug.LogWarning("[Neighbours] Neither road resolved into track, so the pack has nowhere to run. No wolves this wave.");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            // Even wolves take the upper road west, odd ones the lower road east
            // — against the freight on that road either way. If only one road
            // resolved, everything runs down it rather than half the pack simply
            // not turning up.
            bool onUpper = _wolf % 2 == 0;
            List<Vector2> course = onUpper
                ? (upper.Count > 0 ? upper : lower)
                : (lower.Count > 0 ? lower : upper);

            // A wolf turns on whichever knight it finishes beside. On a single pass
            // against the freight that is always the one the freight came FROM; on
            // an odd number of reversals it is the other one, because the wolf ends
            // its patrol back at the end it entered.
            Transform knight = course[course.Count - 1].x < 0f
                ? spawner.LeftPlayer
                : spawner.RightPlayer;

            spawner.SpawnWolf(course, knight, NextType(), 0f);
            _wolf++;

            if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.1f, wolfInterval));
        }
    }

    // One road, walked backwards along the outside of the rail. Backwards is what
    // makes it enter at the end the freight is heading AWAY from, which is the
    // whole reason each road ends up carrying one threat for each knight.
    private List<Vector2> BuildCourse(RailNetwork rails, int run)
    {
        if (rails == null || rails.LineCount == 0) return new List<Vector2>();

        run = Mathf.Clamp(run, 0, rails.LineCount - 1);

        RailLine line;
        if (!rails.TryGetLine(run, out line)) return new List<Vector2>();

        float from = RailCourse.MouthAlong(line, courseLeadIn, againstTheFlow: true);
        List<Vector2> course = RailCourse.Build(rails, run, from, courseDistance,
                                                againstTheFlow: true, outwardOffset: courseOffset);
        return RailCourse.WithReversals(course, courseReversals, courseLeadIn);
    }

    // The flanks run alongside the roads rather than after them: they are the
    // pressure that makes a road hard to watch, which they cannot be if they
    // arrive once the freight is already dealt with. Each group carries its own
    // lead-in, so staggering them is a matter of authoring, not of ordering.
    private IEnumerator ReleaseTheFlanks(Spawner spawner)
    {
        yield return null;
        if (flanks == null || flanks.Count == 0) yield break;

        var running = new List<Coroutine>();
        for (int i = 0; i < flanks.Count; i++)
        {
            if (flanks[i] == null || flanks[i].Total == 0) continue;
            running.Add(spawner.StartCoroutine(flanks[i].Release(spawner, _vermin)));
        }

        for (int i = 0; i < running.Count; i++) yield return running[i];
    }

    private WolfType NextType()
    {
        if (packOrder == null || packOrder.Count == 0) return WolfType.Brown;
        return packOrder[_wolf % packOrder.Count];
    }

    // Round the authored list, carrying the cursor across the whole wave so that
    // two crates on the same road are not always the same crate twice.
    private static GameObject NextOf(List<GameObject> list, ref int cursor)
    {
        if (list == null || list.Count == 0) return null;
        return list[cursor++ % list.Count];
    }

    // Beside the wave, never inside it. Ogres do not belong to any shift (see
    // EnemyOgre.JoinsAmbushes) — they are a clock running underneath whatever
    // else the wave is doing, and the wave is not finished until they are down.
    private IEnumerator ReleaseTheOgres(Spawner spawner)
    {
        _ogres.Clear();
        yield return ogres.Release(spawner, _ogres);
    }

    // Beside the wave, never inside it: the window is a fixed number of seconds
    // from the start, so it neither stretches nor truncates with how fast the
    // players clear whatever else is on the board.
    private IEnumerator WorkTheShafts(Spawner spawner)
    {
        yield return RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed);
    }
}

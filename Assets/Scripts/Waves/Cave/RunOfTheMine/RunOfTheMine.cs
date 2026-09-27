using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The pack has the run of the mine. They come up out of the dark and take the
// TRACK, running the circuit on foot, the wrong way up it, straight into the
// oncoming traffic.
//
// Every other wave down here treats the rails as the mine's property: the carts
// own the road and the knights answer whatever rides it. This one hands the road
// to something with teeth, and the whole fight follows from that one change.
//
// WOLVES ARE TOO EXPENSIVE TO SHOOT, and the wave is built on that number. A
// base arrow is ten damage on a second and a half, so a brown wolf is three
// arrows, a grey is five and a black is six — a pack of six greys is thirty
// arrows, which is forty-five seconds of two knights doing nothing else. The
// wave deliberately sends more pack than the bows can pay for. What it sends
// alongside is the powder to make up the difference.
//
// THE RING IS A MAGAZINE. The traffic is kegs and empties spread evenly round
// the loop rather than packed nose to tail — a rolling rack of charges, five
// units apart, going round and round for the whole wave. Nothing on it is a kill
// worth having (an empty is twenty hit points of nothing, a keg is ten), so the
// only reason to spend an arrow on the iron is to set it off at the moment the
// pack is beside it. Kegs are the wave's real ammunition and there are a fixed
// number of them: the ring never refills, so powder wasted early is powder the
// last relay does not get.
//
// RUNNING AGAINST THE TRAFFIC IS WHAT MAKES THAT A SKILL rather than a wish. On
// a loop, two things going the same way at the same speed never close — that is
// the frozen-spacing property Bits and Pieces is built on — so a pack running
// WITH the carts would sit beside the same slot forever and either always have a
// keg to hand or never. Head-on, a wolf at 3 u/s closes on a cart at 2.4 at 5.4
// u/s, so it passes a keg roughly every second pair of slots and the player
// picks WHICH pass to take. That is the wave in one sentence: the powder is
// coming round, the pack is coming round the other way, and you choose the
// crossing.
//
// AND THE ROAD IS LONG, because a choice needs somewhere to happen. The opening
// tier runs the pack the WHOLE RING — fifty-nine units, in from off-frame, the
// top edge, down the left shaft, the whole bottom edge, up the right shaft and
// back along the top to where it came in. That is twenty seconds of a wolf out
// on the track passing charge after charge, which is more road than a player
// needs, and that is the point of an opening tier: there is time to pass a
// crossing up, watch it go by, and take the next one.
// A quarter of a lap is not a decision, it is a reflex: the first keg you see is
// the only keg you get. So the first three tiers are the road getting shorter —
// fifty-nine, twenty-six, twenty-two.
//
// From tier IV the road goes back up, to thirty-six, thirty-four, thirty-two and
// thirty (owner, 2026-09-25: "wolves attack too soon, double their run"). Those
// tiers carry nine to twelve relays of four or five wolves, and on an eighteen-unit
// road every relay was on the knights six seconds after it appeared. The late
// tiers get harder through the size of the pack instead.
//
// The arithmetic of the crossing, which is geometry rather than feel:
//   * A keg reaches 2.8 units (EnemyKegCart) and deals 15.
//   * A relay released 0.8s apart runs 2.4 units apart, so one blast covering
//     5.6 units of road takes THREE of them at once. That is why the release
//     interval is tight and why widening it in a tier is a real nerf to the
//     player rather than a kindness.
//   * 15 is half a brown wolf. So one keg on a trio is forty-five damage — four
//     and a half arrows — and two kegs on the SAME trio is three dead wolves for
//     two shots. Keeping a trio together long enough to eat a second charge is
//     the ceiling this wave is reaching for.
//   * The knights are inside all of this. A keg popped on the stretch over or
//     under a knight catches the knight too, which is the standing tax on
//     impatience and is the same rule every keg in the mine follows.
//
// Relays are AMBUSHES: one goes out, and the next does not set off until the
// last wolf of the last one is down. The debt that owes the player — a group
// left alive has to cost something — is paid by the ring itself, because the
// traffic carries riders. They throw for as long as they are up, and they are
// still circling when the pack arrives.
//
// What ends the wave is the pack AND those riders, and nothing else. Kegs,
// empties and spent wrecks are not kills the player owes (EnemyMineCart), so the
// last thing a run of this wave usually needs is a rider picked off from behind
// the iron once the dogs are down. Keep the rider count low for exactly that
// reason: every one of them is a coda the players have to play out.
//
// Nothing rolls dice (see the no-randomness pillar). The traffic pattern repeats
// slot for slot, the pack's types come off a rotation, and targets alternate
// left-right. All three rotations reset at the top of every run, because a wave
// asset's state outlives the run that set it.
[CreateAssetMenu(fileName = "RunOfTheMine", menuName = "Waves/Run of the Mine")]
public class RunOfTheMine : BaseWave
{
    // One relay of the pack. What is in it is the counts below; this is where it
    // comes from, how fast, and how far it runs before it turns on you.
    [System.Serializable]
    public class Relay
    {
        [Tooltip("Authoring only — names the relay in the inspector and the dev log")]
        public string label;

        [Tooltip("Index into the layout's runs: the stretch of track this relay comes up. Alternate it between relays (0 = the top run, 2 = the bottom on Mine Circuit) so the pack does not always arrive over the same shoulder.")]
        public int courseRun;

        [Tooltip("Wolves in this relay. These are the only kills that hold the wave open — the iron gates nothing.")]
        public int wolves = 3;

        [Tooltip("Seconds between one wolf entering and the next. At 3 u/s this is also their spacing: 0.8s puts them 2.4 units apart, which is what lets one keg reach three of them. Widening it is a real nerf to the player.")]
        public float wolfInterval = 0.8f;

        [Tooltip("Quiet before this relay's first wolf — the beat after the last one was cleared.")]
        public float leadIn = 1.5f;

        [Tooltip("How far along the rails the pack runs before it peels off and charges. Mine Circuit's lap is 40 units and the off-frame lead-in is another 19, so 59 is the whole ring — in off-frame, the top edge, down the left shaft, the bottom edge, up the right shaft and back along the top, turning every corner. SHORTENING IT IS THE TIER DIAL: it is the time the players have while the pack is still out on the road and still lined up with the powder. 30 is three quarters, and around 19 is the top edge and nothing else.")]
        public float courseDistance = 30f;
    }

    [Header("Track")]
    [Tooltip("A closed loop. Mine Circuit is the one this is tuned against: a 40-unit lap that runs over and under the knights, so both the powder and the pack pass within reach of them.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs. Every cart enters here, off-frame. Point it at the run the pack does NOT open on, so the first relay meets traffic that is already moving rather than watching it be laid.")]
    [SerializeField] private int entryRun = 2;

    [Header("The magazine")]
    [Tooltip("One slot per cart, repeated round the loop. Kegs are the wave's ammunition and empties are the arrows it eats — a null entry costs a slot and spawns nothing, which is how a gap is authored. Add a rider to give the ring some standing pressure.")]
    [SerializeField] private List<GameObject> trafficPattern = new List<GameObject>();

    [Tooltip("How many carts go round. They are spread EVENLY rather than packed, so this also sets how far apart the charges are: 8 on a 40-unit lap is one every 5 units.")]
    [SerializeField] private int trafficCarts = 8;

    [Tooltip("Seconds between carts. Leave at 0 to spread them evenly round the measured lap, which is the only spacing that stays even when the layout or the cart speed moves.")]
    [SerializeField] private float trafficInterval;

    [Tooltip("Seconds after the last rail piece lands before the first cart rolls in")]
    [SerializeField] private float firstCartDelay = 0.5f;

    [Header("The pack")]
    [Tooltip("Quiet between the first cart rolling in and the first relay setting off. The ring is still filling while the pack arrives — on purpose: the early relays meet thin traffic and have less powder to work with, which is the wave's own ramp.")]
    [SerializeField] private float packLeadIn = 6f;

    [Tooltip("In order — each waits for the last to be dead before it sets off.")]
    [SerializeField] private List<Relay> relays = new List<Relay>();

    [Tooltip("Cycled in order as wolves are released, and carried across the whole wave. One entry means a pack of one kind.")]
    [SerializeField] private List<WolfType> packOrder = new List<WolfType> { WolfType.Brown };

    [Tooltip("How far outside the rail the pack runs. Big enough that they are clearly beside the track rather than inside the carts, small enough to stay well within a keg's reach — the iron ends up between the pack and the knights, which is where the wave wants it.")]
    [SerializeField] private float courseOffset = 1.2f;

    [Tooltip("How far off-frame the pack joins the track. Matches MineCart's own lead-in so a wolf appears from exactly where a cart would have.")]
    [SerializeField] private float courseLeadIn = 6f;

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
    [Tooltip("Crosses the band between the knights and the top run by default — the one stripe no cart and no wolf occupies, so an orb is never a shot the ring eats. Keep it off y=3 and y=-3 (the rails) and off y=4.2 and y=-4.2 (the pack's road).")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, 1f), to = new Vector2(12f, 1f) };

    // Where the pack rotation stands — it drives both the wolf type and which
    // knight the next one is aimed at. Reset at the top of every run: SO state
    // outlives the run that set it, and a rotation that carried over would deal
    // a different pack each time the wave came up.
    private int _wolf;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _wolf = 0;

        var rails = spawner.Rails;
        if (rails == null)
        {
            Debug.LogWarning("[RunOfTheMine] No RailNetwork in the scene — there is no road for the pack to take, and no powder to take it with.");
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock, and only
        // the traffic waits it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        // Both beside the wave, never inside it. The wave's length is set by how
        // fast the pack dies, so anything joined to the spawn phase would stretch
        // and shrink with them — and the ring in particular has to keep filling
        // while the first relay is already being fought.
        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));
        Coroutine traffic = spawner.StartCoroutine(FillTheRing(rails, layDuration + firstCartDelay));
        Coroutine brutes = spawner.StartCoroutine(ReleaseTheOgres(spawner));
        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner));

        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, firstCartDelay) + Mathf.Max(0f, packLeadIn));

        for (int i = 0; i < relays.Count; i++)
        {
            Relay relay = relays[i];
            if (relay == null) continue;

            yield return new WaitForSeconds(Mathf.Max(0f, relay.leadIn));

            // The road is worked out ONCE, before the group is declared, and both
            // halves of that matter.
            //
            // Once, because every wolf in a relay runs the same stretch: the
            // course is a property of the relay, not of the animal walking it.
            //
            // Before, because a declared count that the release loop then fails to
            // meet is a wave that hangs forever — IsAmbushClear will not pay out
            // until it has SEEN as many members as were promised. Resolving the
            // road first means the promise is made against a road that exists, and
            // an empty one asks for nothing.
            List<Vector2> course = BuildCourse(rails, relay);
            int count = course.Count > 0 ? Mathf.Max(0, relay.wolves) : 0;

            if (course.Count == 0 && relay.wolves > 0)
            {
                Debug.LogWarning($"[RunOfTheMine] Relay '{relay.label}' has no road to run — run " +
                                 $"{relay.courseRun} did not resolve into track. The relay is skipped.");
            }

            // DECLARED, and it has to be: the relay is paced by this loop's own
            // waits, so without a count a group whose first wolf dies before its
            // second has been released reads as clear and the next relay runs
            // straight over the top of it.
            BeginAmbush(count);

            for (int w = 0; w < count; w++)
            {
                ReleaseWolf(spawner, course);
                if (w < count - 1) yield return new WaitForSeconds(Mathf.Max(0.05f, relay.wolfInterval));
            }

            // As load-bearing as MarkSpawningComplete: a relay that is never
            // marked released can never read as clear, and the wait below would
            // hang on it forever.
            MarkAmbushReleased();

            // Last relay out: if a quick clear beat the orb timer, the orb goes
            // now, with this relay still on the board. See OrbRun.SendFirstIfWaiting.
            if (i == relays.Count - 1) orbs.SendFirstIfWaiting(spawner);

            yield return WaitForAmbushClear();
        }

        // FIRST, before any tidying up. Nothing below gates the wave, and an
        // exception thrown between the last relay and this line would kill the
        // coroutine where it stands, leaving a run hanging on a wave that can
        // never end.
        yield return brutes;
        yield return shafts;

        MarkSpawningComplete();

        // Carts already circling keep circling until the next wave clears the
        // track; what stops here is anything that had not been released yet.
        // Guarded because StartCoroutine hands back null for an enumerator that
        // finished without ever yielding — see OrbRun.Release.
        if (traffic != null) spawner.StopCoroutine(traffic);
        if (orbRun != null) spawner.StopCoroutine(orbRun);

        yield return null;
    }

    // The stretch of rail a relay runs, resolved into a walkable polyline. Once
    // this exists the wolves no longer need the track at all — they follow world
    // points — which is why nothing downstream re-checks that the rails are still
    // there.
    private List<Vector2> BuildCourse(RailNetwork rails, Relay relay)
    {
        if (rails.LineCount == 0) return new List<Vector2>();

        int run = Mathf.Clamp(relay.courseRun, 0, rails.LineCount - 1);

        RailLine line;
        if (!rails.TryGetLine(run, out line)) return new List<Vector2>();

        float from = RailCourse.MouthAlong(line, courseLeadIn, againstTheFlow: true);
        return RailCourse.Build(rails, run, from, relay.courseDistance,
                                againstTheFlow: true, outwardOffset: courseOffset);
    }

    // One wolf onto the road, running the rails backwards into the traffic.
    private void ReleaseWolf(Spawner spawner, List<Vector2> course)
    {
        // Alternated rather than aimed. Both knights have to answer the same
        // relay, and a pack that all went for one of them would be a wave the
        // other player watches.
        Transform knight = (_wolf % 2 == 0) ? spawner.LeftPlayer : spawner.RightPlayer;

        spawner.SpawnWolf(course, knight, NextType(), 0f);
        _wolf++;
    }

    private WolfType NextType()
    {
        if (packOrder == null || packOrder.Count == 0) return WolfType.Brown;
        return packOrder[_wolf % packOrder.Count];
    }

    // Lays the magazine out round the loop, one slot at a time. Runs beside the
    // wave rather than in front of it, so the ring is still coming out while the
    // first relay is being fought.
    private IEnumerator FillTheRing(RailNetwork rails, float trackDelay)
    {
        // One frame before anything is decided, and it is load-bearing for the
        // same reason it is in OrbRun: StartCoroutine hands back NULL for an
        // enumerator that finishes without ever yielding, and this one can — an
        // empty pattern, or no carts asked for. The wave would then
        // StopCoroutine(null), the exception would kill the wave coroutine where
        // it stands, and the run would hang on a wave that cannot end.
        yield return null;

        int count = Mathf.Max(0, trafficCarts);
        if (count == 0 || trafficPattern == null || trafficPattern.Count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay));

        float interval = ResolveInterval(rails, count);

        for (int i = 0; i < count; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[RunOfTheMine] The loop went out from under the magazine — the rest of the powder stays underground.");
                yield break;
            }

            // Every cart from the same mouth. They are NOT dealt across the
            // layout's runs: on a loop every run is the same piece of track a lap
            // apart, so spreading them would have carts appearing in the middle of
            // the board on all four sides at once instead of driving in from
            // off-frame.
            int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);

            GameObject prefab = trafficPattern[i % trafficPattern.Count];
            if (prefab != null) rails.SpawnCart(prefab, line, MineCart.TrackSpeed);

            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
    }

    // The magazine's spacing, in seconds. Authored wins; 0 asks for the carts to
    // be spread evenly round the loop, which is a property of the TRACK and so is
    // measured off it — a figure typed in here stops being even the moment the
    // layout or the speed moves.
    private float ResolveInterval(RailNetwork rails, int carts)
    {
        if (trafficInterval > 0f) return trafficInterval;

        float speed = MineCart.TrackSpeed;
        int line = Mathf.Clamp(entryRun, 0, Mathf.Max(0, rails.LineCount - 1));

        float lap;
        if (carts > 0 && rails.TryMeasureLoop(line, out lap))
        {
            return Mathf.Max(0.1f, lap / carts / speed);
        }

        Debug.LogWarning("[RunOfTheMine] Asked to spread the magazine round the lap, but the layout " +
                         "does not come back on itself. This wave wants a closed loop — check the " +
                         "teleport table. Falling back to 2s between carts.");
        return 2f;
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

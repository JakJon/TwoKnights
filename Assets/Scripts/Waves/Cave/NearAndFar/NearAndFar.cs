using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One loop, two very different stretches of it. The FAR track runs along the top
// edge of the view, as high as carts can ride and still be drawn inside it. The
// NEAR track comes back the other way directly beneath the knights, close enough
// that the shields sweep across the roofs of the carts going under them.
//
// A shift enters at the TOP-LEFT and runs east along the far track, so the first
// thing the knights get is the whole width of the board to read it across. Then
// it turns down the right edge and comes back west underneath them, and that pass
// is not a look, it is a mugging.
//
// The two riders split cleanly along that line, which is the point of the shape.
// A bomb thrower will not release unless it is well above the knights, so it is
// armed on the top pass and inert underneath — its bomb is a long fall you can
// see coming. A pickaxe thrower only has a range check, and the near track runs
// close enough that it is live for most of the underneath pass, from a couple of
// units away. So the top of the loop is the bombing run and the bottom is
// point-blank, and a cart does both every lap.
//
// The monster crate is the sharpest version of that, because its drop flag stands
// on the RIGHT DESCENT — the run immediately AFTER the far track. A crate unloads
// on its first time round, and the last chance to stop it comes once it has
// already been over your head. So a player who put the interception off until the
// cart was close has spent the close pass shooting at the thing that was not
// throwing anything. There is no second chance at a crate the way there is at a
// rider: it is stopped on that lap or it is not stopped at all.
//
// Like Choo Choo this is authored as AMBUSHES rather than as a schedule: a shift
// goes out, and the next one does not set off until the last one is dead. What
// differs is HOW a shift is authored. Choo Choo names its carts one slot at a
// time; here a shift is five counts — gnomes, kegs, empties, crates, bats — and
// the wave builds the running order from them. Tiers of this wave are then the
// same fight with the dial turned, rather than hand-written cart lists that drift.
//
// Nothing rolls dice (see the no-randomness pillar). The running order is derived
// from the counts by a fixed rule, and the cart kinds and bat corners come off
// rotations that carry across the whole wave and reset at the top of every run.
//
// Standing pressure while a shift is alive is the shift itself: the gnomes throw
// on their own cooldowns for as long as they are up, and the bats are part of the
// group rather than a garnish beside it. There is no separate shaft timer here
// and none is wanted — a stalled shift is already costing you.
//
// The lap is 48 units (two 16-unit straights, two 4-unit shafts, and 2 units
// through each of the four elbows), so at 2.4 u/s a cart is 20.0s round and it is
// 28 units from the mouth to the drop flag — 11.7s. Release intervals are
// PERMANENT spacings on a loop — every cart runs at one speed — so leave one at 0
// and the shift is spread evenly round the lap instead.
[CreateAssetMenu(fileName = "NearAndFar", menuName = "Waves/Near and Far")]
public class NearAndFar : BaseWave
{
    // One shift's PACING. What is in it is not authored here — the counts below
    // are, and every shift is the same recipe one cart heavier. See BuildShift.
    [System.Serializable]
    public class Ambush
    {
        [Tooltip("Authoring only — names the shift in the inspector and the dev log")]
        public string label;

        [Tooltip("Quiet before this shift's first cart — the beat after the last one was cleared. On the opening shift it is measured from the track finishing its fall.")]
        public float leadIn = 1f;

        [Tooltip("Seconds between this shift's carts. On a loop this is the spacing they keep forever. 0 spreads the shift evenly round the measured lap instead, which is the only spacing that stays even when the track is retuned.")]
        public float releaseInterval = 1.8f;

        [Tooltip("Seconds between this shift's bats. The first one is held back by this much as well, so the carts are always the thing that arrives first.")]
        public float batInterval = 5f;

        [Tooltip("Seconds of quiet between volleys down the shafts while this shift is alive. 0 turns the shafts off for the shift — which also makes leaving it up free, so use it sparingly.")]
        public float projectileInterval = 5f;
    }

    [Header("Track")]
    [Tooltip("A loop with a near side along the bottom of the view and a far side over the shields. Its drop flag is where a crate unloads.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs. Everything enters here, off-frame. Point it at the FAR track (run 2 on Mine Perimeter) so a shift comes in at the top-left and runs the top edge before it drops to the pass underneath.")]
    [SerializeField] private int entryRun = 2;

    [Header("Rolling stock")]
    [Tooltip("Cycled in order as a shift's gnome slots are filled. One entry means a shift of one kind of thrower; two means they alternate.")]
    [SerializeField] private List<GameObject> gnomeCarts = new List<GameObject>();

    [Tooltip("Fills every keg slot. Needs a MineCart; 10 hit points, so one arrow.")]
    [SerializeField] private GameObject kegCart;

    [Tooltip("Fills every empty slot. Nobody in it — 20 hit points of iron that gates nothing and exists to be in the way of the shot you wanted.")]
    [SerializeField] private GameObject emptyCart;

    [Tooltip("Cycled in order as a shift's crate slots are filled. Each needs an EnemyDeliveryCart carrying ONE passenger — see BuildShift for why a second one strands the crate on a loop.")]
    [SerializeField] private List<GameObject> crateCarts = new List<GameObject>();

    [Header("The opening shift")]
    [Tooltip("Riders in the FIRST shift. These are the kills that hold a shift open, and each one leaves a wreck rolling when he dies.")]
    [SerializeField] private int gnomes = 3;

    [Tooltip("Powder carts in the first shift. They do not gate it — a keg is a tool, not a kill — and they sit directly behind a gnome so setting one off is worth the arrow.")]
    [SerializeField] private int kegs = 2;

    [Tooltip("Empty carts in the first shift. They gate nothing; they are the iron your arrows stop at.")]
    [SerializeField] private int empties = 2;

    [Tooltip("Crates in the first shift, hauling something alive to the drop flag. They gate the shift until they are stopped or they unload.")]
    [SerializeField] private int monsterCrates = 1;

    [Tooltip("Bats per shift, off the rails. These do NOT grow with the shift number — the escalation is on the track, and air that grew with it would double the ramp.")]
    [SerializeField] private int bats = 1;

    [Header("Shifts")]
    [Tooltip("In order — one entry per shift, and the entry is the shift's PACING only. Each one waits for the last to be dead before it sets off, and each is one cart per kind heavier than the one before.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    [Header("Rock")]
    [Tooltip("Cycled in order as the shafts work. The loop already owns straight overhead and straight underneath — the whole wave is a guard flipping between the two — so the rock is what makes that flip cost something: it arrives from ABOVE while the near track is running point-blank beneath you.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("Crosses the band between the shields and the far track by default — the one stripe of the board no cart occupies, so an orb is never a shot the ring eats. Keep it off y=-1..-2 and y=4..5, which is where carts ride.")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, 2f), to = new Vector2(12f, 2f) };

    // Where the three rotations stand. Reset at the top of every run — SO state
    // outlives the run that set it, and a rotation that carried over would deal a
    // different shift each time the wave came up.
    private int _gnome;
    private int _crate;
    private int _bat;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _gnome = 0;
        _crate = 0;
        _bat = 0;

        var rails = spawner.Rails;
        if (rails == null)
        {
            if (railLayout != null)
            {
                Debug.LogWarning("[NearAndFar] No RailNetwork in the scene — there is no loop to run.");
            }
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock, and only
        // the opening shift waits it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        // Beside the wave, never inside it: the shifts are player-paced, so an orb
        // run joined to the spawn phase would stretch or truncate with them
        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush ambush = ambushes[i];
            if (ambush == null) continue;

            BeginAmbush();

            // Track and air are released together, and BOTH are waited on before
            // the shift is declared released. A bat still queued behind its
            // interval is a member that does not exist yet, and a group that reads
            // as clear before its last member has spawned is one the wave runs
            // straight over the top of.
            Coroutine carts = spawner.StartCoroutine(ReleaseCarts(rails, ambush, i, layDuration));
            Coroutine bats = spawner.StartCoroutine(ReleaseBats(spawner, ambush, layDuration));

            // The shift is the clock: the shafts work for exactly as long as it
            // is alive, so a gnome left up on the far track is rock the players
            // keep paying for. Not waited on — it ends itself when the shift does.
            Coroutine shafts = ambush.projectileInterval > 0f
                ? spawner.StartCoroutine(WorkTheShafts(spawner, ambush.projectileInterval))
                : null;

            yield return carts;
            yield return bats;

            // One frame more, and it is load-bearing. Spawner.SpawnBat hands off to
            // its own coroutine, so even at zero delay the bat is not instantiated
            // — and therefore not registered — until the frame after the call. The
            // release coroutine above has already returned by then, so without this
            // beat a shift of nothing but kegs and bats could be declared released
            // with none of its members on the field yet, read as clear on the spot,
            // and be run over by the next shift.
            yield return null;

            layDuration = 0f; // only the opening shift waits out the track

            // Out here rather than at the end of either coroutine, so it still runs
            // when one of them bails early. A shift that is never marked released
            // can never read as clear, and the wave would hang on it forever.
            MarkAmbushReleased();

            // Last shift out: if a quick clear beat the orb timer, the orb goes
            // now, with this shift still on the board. See OrbRun.SendFirstIfWaiting.
            if (i == ambushes.Count - 1) orbs.SendFirstIfWaiting(spawner);

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
    // layout's runs: on a loop every run is the same piece of track a lap apart,
    // so spreading them would have carts appearing in the middle of the board on
    // all four sides at once instead of driving in from off-frame.
    private IEnumerator ReleaseCarts(RailNetwork rails, Ambush ambush, int shiftIndex, float trackDelay)
    {
        List<GameObject> shift = BuildShift(shiftIndex);

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, ambush.leadIn));

        if (shift.Count == 0) yield break;

        float interval = ResolveInterval(rails, ambush, shift.Count);

        for (int i = 0; i < shift.Count; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[NearAndFar] The loop went out from under the wave — the rest of the shift stays underground.");
                yield break;
            }

            int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(shift[i], line, MineCart.TrackSpeed);

            if (i < shift.Count - 1) yield return new WaitForSeconds(interval);
        }
    }

    // The running order, built from the counts by a fixed rule so that a tier is
    // five numbers rather than a hand-written list.
    //
    // EVERY SHIFT IS THE ONE BEFORE IT PLUS ONE OF EACH CART. The authored counts
    // describe the opening shift; shift n adds n to each of them. That is the
    // wave's whole difficulty curve and it is deliberately not authored per shift:
    // the ramp should be a property of the wave, not four numbers a tier can get
    // subtly wrong. Bats are excluded — they are not on the track, and growing the
    // air at the same rate would ramp twice.
    //
    // Crates lead. A crate has three quarters of a lap to run before its flag,
    // which is very nearly as long as the rest of the shift takes to release — so
    // putting it at the front makes the question the wave is asking ("spend the
    // arrows on the crate or on the gnomes?") land while the shift is still
    // arriving, instead of after the knights have already cleared the track.
    //
    // Then gnomes and kegs alternate, and that order is load-bearing too: it puts
    // every keg directly BEHIND a rider. A keg's blast reaches roughly one release
    // interval either side of it, so a keg taken at the right moment is a gnome
    // the player never had to shoot twice — and a keg taken at the wrong one is a
    // keg wasted on empty track. Coupling the two is what makes that a decision.
    //
    // A slot whose prefab is missing is dropped rather than left as a hole: the
    // counts are the contract here, and a silent gap in the spacing reads as a
    // release that misfired.
    private List<GameObject> BuildShift(int shiftIndex)
    {
        var shift = new List<GameObject>();
        int step = Mathf.Max(0, shiftIndex);

        // One passenger per crate, and the count is how you ask for more than one.
        // A delivery cart serves a given flag ONCE (EnemyDeliveryCart remembers the
        // drops it has already been past), so on a loop a two-passenger crate
        // unloads on its first pass and then circles for the rest of the wave still
        // holding a passenger — and still holding the shift open, because a loaded
        // crate gates until it is emptied or shot. Two crates is the same two
        // passengers with none of that.
        for (int i = 0; i < Mathf.Max(0, monsterCrates) + step; i++)
        {
            Add(shift, NextOf(crateCarts, ref _crate));
        }

        int gnomesLeft = Mathf.Max(0, gnomes) + step;
        int kegsLeft = Mathf.Max(0, kegs) + step;
        int emptiesLeft = Mathf.Max(0, empties) + step;

        // Gnome, then keg, then empty, round and round. The gnome-then-keg
        // adjacency is the load-bearing part: a keg's blast reaches roughly one
        // release interval either side, so a keg taken at the right moment is a
        // rider the player never had to shoot twice. The empty then rides between
        // that pair and the next, which is what stops the whole train being one
        // long fuse and makes a well-timed keg a measured hole rather than a clear.
        while (gnomesLeft > 0 || kegsLeft > 0 || emptiesLeft > 0)
        {
            if (gnomesLeft > 0)
            {
                Add(shift, NextOf(gnomeCarts, ref _gnome));
                gnomesLeft--;
            }

            if (kegsLeft > 0)
            {
                Add(shift, kegCart);
                kegsLeft--;
            }

            if (emptiesLeft > 0)
            {
                Add(shift, emptyCart);
                emptiesLeft--;
            }
        }

        return shift;
    }

    // Bats are the shift's air. Held back by one interval on purpose: the carts
    // are what the wave wants read first, and a bat that arrives with them just
    // splits the read.
    private IEnumerator ReleaseBats(Spawner spawner, Ambush ambush, float trackDelay)
    {
        int count = Mathf.Max(0, bats);
        if (count == 0) yield break;

        float interval = Mathf.Max(0.1f, ambush.batInterval);

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay)
                                        + Mathf.Max(0f, ambush.leadIn)
                                        + interval);

        for (int i = 0; i < count; i++)
        {
            spawner.SpawnBat(BatEntry(spawner), 0f);
            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
    }

    // THREE corners against the Mine's every-fourth-bat dark cadence, and the
    // count is the point: three and four are coprime, so the dark bat walks all
    // three entries over the wave instead of always coming from the same one. Two
    // of the three are up over the far track and one comes up past the near one,
    // which is the same near/far alternation the loop itself is doing.
    private Vector2 BatEntry(Spawner spawner)
    {
        switch (_bat++ % 3)
        {
            case 0: return spawner.topLeftCorner;
            case 1: return spawner.bottomRightCorner;
            default: return spawner.topRightCorner;
        }
    }

    // A shift's spacing, in seconds. Authored wins; 0 asks for the shift to be
    // spread evenly round the loop, which is a property of the TRACK and so is
    // measured off it rather than worked out by hand — a figure typed in here
    // stops being even the moment the layout or the speed moves.
    private float ResolveInterval(RailNetwork rails, Ambush ambush, int carts)
    {
        if (ambush.releaseInterval > 0f) return ambush.releaseInterval;

        float speed = MineCart.TrackSpeed;
        int line = Mathf.Clamp(entryRun, 0, Mathf.Max(0, rails.LineCount - 1));

        float lap;
        if (carts > 0 && rails.TryMeasureLoop(line, out lap))
        {
            return Mathf.Max(0.1f, lap / carts / speed);
        }

        Debug.LogWarning("[NearAndFar] Asked to spread a shift round the lap, but the layout " +
                         "does not come back on itself. Falling back to 2s between carts.");
        return 2f;
    }

    private static void Add(List<GameObject> shift, GameObject prefab)
    {
        if (prefab != null) shift.Add(prefab);
    }

    // Round the authored list, carrying the cursor across the whole wave so that
    // two shifts of the same size are not the same shift twice.
    private static GameObject NextOf(List<GameObject> list, ref int cursor)
    {
        if (list == null || list.Count == 0) return null;
        return list[cursor++ % list.Count];
    }

    private IEnumerator WorkTheShafts(Spawner spawner, float interval)
    {
        if (volleys == null || volleys.Count == 0) yield break;

        float wait = Mathf.Max(0.1f, interval);
        int shot = 0;

        while (!IsAmbushClear())
        {
            RockVolley volley = volleys[shot++ % volleys.Count];
            float busy = RockVolley.Fire(spawner, volley, rockSpeed);
            yield return new WaitForSeconds(busy + wait);
        }
    }
}

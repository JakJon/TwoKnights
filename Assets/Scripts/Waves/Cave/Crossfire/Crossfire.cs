using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rock in straight lines, from a new angle every few seconds, and the two knights
// always get the same line handed to them backwards.
//
// One BURST is a file of rocks fired from a single point off-frame straight down
// the line to a knight — same origin, same heading, evenly spaced, so it reads on
// screen as a lit fuse coming in rather than as a scatter. Bursts go out in
// MIRRORED PAIRS: the left knight's line arrives at some angle and the right
// knight's arrives at its reflection across x=0, at the same instant. Whatever
// the left player is doing, the right player is doing in a mirror. That is the
// whole social content of the wave and it is why the pair is the unit here.
//
// The mirror also does the safety work for free. Every angle is drawn from the
// half a knight owns — the left knight is only ever shot at from the left side of
// the board — so a line aimed at one knight physically cannot pass through the
// other and be eaten by the wrong shield (the hard rule about volleys crossing
// the middle). Reflecting a left-side angle lands exactly on a right-side one, so
// the mirror and the rule are the same constraint.
//
// FLIGHT TIME IS FIXED, SPEED IS DERIVED. Rocks spawn on the off-frame rectangle
// (x = +/-12, y = +/-7), which is between 7 and about 11.6 units out depending on
// the angle, so a shared speed would make a burst from overhead land seconds
// before one from the side and the cadence the player feels would not be the
// cadence authored. Each burst instead flies at distance / flightSeconds, which
// puts every rock of every angle on its knight after exactly the same beat. It
// also keeps the telegraph honest: the rectangle sits a fairly even margin
// outside the view, so a constant flight time is also a near-constant time on
// screen. The knock-on is what makes the tuning readable — spawn cadence IS
// landing cadence, so switchWindow below is a real number of seconds.
//
// What the wave is actually asking is a question about FACING. Shield facing is
// also shooting direction, so a knight aimed at the incoming line cannot be aimed
// at the gnomes riding the rim, and the only moment to turn, fire, and come back
// is the gap between one burst finishing and the next arriving. Tightening that
// gap per tier is therefore not just less reaction time, it is fewer arrows: at
// two seconds there is room for a shot every pair, at eight tenths there is not,
// and the rim has to be paid for in blocked rocks.
//
// Nothing rolls dice (see the no-randomness pillar). Angles come off a golden
// stride across the legal half, which is fixed, reproducible, and has the
// property the wave wants: successive samples are never nearer than about 69
// degrees apart, so every pair is a real swing of the stick and no two
// consecutive bursts can arrive from nearly the same place. The stride counter
// resets at the top of every run, because a wave asset's state outlives the run
// that set it.
[CreateAssetMenu(fileName = "Crossfire", menuName = "Waves/Crossfire")]
public class Crossfire : BaseWave
{
    // The off-frame rectangle every rock is born on. Same figures the Spawner's
    // own named positions use, so a burst from straight overhead spawns exactly
    // where aboveLeftPlayer would have put it.
    private const float SpawnHalfWidth = 12f;
    private const float SpawnHalfHeight = 7f;

    // Rocks are aimed at the knight's centre, half a unit above the transform
    // (ProjectileMovement.FaceTarget). Measuring the ray from the same point is
    // what makes an authored angle the angle that actually arrives.
    private static readonly Vector2 AimOffset = new Vector2(0f, 0.5f);

    // The half of the board a knight is shot at from, in degrees: 90 is straight
    // overhead, 180 is level from his own side, 270 is straight below. Reflecting
    // any of it across x=0 gives the other knight's half exactly.
    private const float BandStart = 90f;
    private const float BandWidth = 180f;

    private const float Golden = 0.6180339887f;

    [Header("Track")]
    [Tooltip("A loop that runs the rim of the view — over the knights, down one edge, back underneath, up the other. Laid as the wave starts; cleared when the next one begins.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs. Riders enter here, off-frame. Point it at the TOP run (2 on Mine Rim): a bomb thrower only releases well above the knights, so entering up there is what arms him on his first crossing instead of his second.")]
    [SerializeField] private int entryRun = 2;

    [Header("Riders")]
    [Tooltip("Cycled in order as the rider slots are filled. Bomb throwers — the rim carries track over the knights, which is the one thing a bomb gnome needs. One entry means every rider is the same kind; add a pickaxe gnome to alternate them.")]
    [SerializeField] private List<GameObject> gnomeCarts = new List<GameObject>();

    [Tooltip("Riders this tier sends out, one at a time. These are the wave's only kills — the bursts never end while one is still up, so this is also what sets how long the wave runs.")]
    [SerializeField] private int gnomes = 1;

    [Tooltip("Seconds between one rider entering and the next. The wave cannot be shorter than this times the riders, which is how a tier's length is authored.")]
    [SerializeField] private float gnomeInterval = 10f;

    [Tooltip("Quiet between the track finishing its fall and the first rider entering. The bursts do not wait on it — rock is already in the air while the rails are still landing.")]
    [SerializeField] private float firstRiderAt = 1f;

    [Header("Bursts")]
    [Tooltip("Rocks in one burst. Both knights get this many at once, from mirrored angles.")]
    [SerializeField] private int rocksPerBurst = 3;

    [Tooltip("Seconds between one rock of a burst landing and the next. The file's spacing on screen is this times the burst's speed.")]
    [SerializeField] private float rockSpacing = 0.35f;

    [Tooltip("Seconds between a pair's LAST rock landing and the next pair's first — the whole time a knight has to read the new angle, swing to it, and get a shot off on the way. This is the tier dial.")]
    [SerializeField] private float switchWindow = 2f;

    [Tooltip("Seconds every rock spends in the air, whatever angle it came from. Speed is derived from it per burst, so this is the telegraph — the 4s house figure leaves a rock visible for a little over three of them.")]
    [SerializeField] private float flightSeconds = 4f;

    [Header("Orbs")]
    [Tooltip("The rim encircles the fight, so an orb has to come through the ring — its line crosses the two side rails, and an arrow spent on it while a cart is sitting there is an arrow the iron eats. Keep the line off y=4 and y=-4, which is where the long tracks ride.")]
    [SerializeField] private OrbRun orbs = new OrbRun
    {
        count = 0, firstAt = 12f, interval = 12f,
        from = new Vector2(-12f, 2f), to = new Vector2(12f, 2f)
    };

    // Where the two rotations stand. Reset at the top of every run — SO state
    // outlives the run that set it, and a stride that carried over would open the
    // wave on a different angle each time it came up.
    private int _burst;
    private int _gnome;

    // The riders themselves, and whether the last one is out. This is the wave's
    // stop condition stated outright: the bursts run until every rider has been
    // sent AND every one of them is dead. It is deliberately NOT the ambush
    // machinery — an ambush answers "is this group down, so may I send the next
    // one", and this wave never sends a next one. What it needs to know is
    // whether a gnome is still alive, which is this list and nothing else.
    //
    // A rider and his cart are one GameObject (EnemyMineCart requires MineCart on
    // itself), and death destroys it while the wreck it leaves is a separate
    // object — so a null slot here means that gnome is dead, and a wreck circling
    // the rim forever can never be mistaken for one.
    private readonly List<GameObject> _riders = new List<GameObject>();
    private bool _allRidersReleased;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _burst = 0;
        _gnome = 0;
        _riders.Clear();
        _allRidersReleased = false;

        var rails = spawner.Rails;
        if (rails == null && railLayout != null)
        {
            Debug.LogWarning("[Crossfire] No RailNetwork in the scene — there is no rim to ride, so this is bursts alone.");
        }

        // Fire-and-forget: the cascade runs on the network's own clock. Only the
        // riders wait it out — the first pair of bursts goes out over a track
        // that is still falling, which is the wave stating its business up front.
        float layDuration = rails != null ? rails.Lay(railLayout) : 0f;

        // Beside the wave, never inside it: the wave's length is set by how fast
        // the riders die, so an orb run joined to the spawn phase would stretch
        // and shrink with them.
        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        spawner.StartCoroutine(ReleaseRiders(rails, layDuration));

        // Standing pressure, and here it is the wave's whole body: pairs keep
        // landing endlessly for as long as a single rider is up, and the wave is
        // over the moment the last one dies. That is the debt a player-paced wave
        // owes — a rider left alive has to cost something — and it is also why a
        // tier's length is honest. Four riders ten seconds apart cannot be over
        // before the fortieth second no matter how well the knights shoot.
        float ridersWouldTake = Mathf.Max(0, gnomes) * Mathf.Max(0.1f, gnomeInterval);
        float fallbackUntil = Time.time + layDuration + firstRiderAt + ridersWouldTake;

        while (!PressureOver(rails, fallbackUntil))
        {
            FirePair(spawner);
            yield return new WaitForSeconds(PairPeriod);
        }

        // FIRST, before any tidying up. Nothing below gates the wave, and this
        // wave has already been stranded once by an exception thrown between the
        // end of the bursts and this line: the coroutine dies where it stands,
        // the flag never gets set, and a run that can never end is the worst
        // failure this file can produce. Anything that can throw goes after it.
        MarkSpawningComplete();

        // Orbs already crossing finish on their own; what stops here is any that
        // had not been released yet. Guarded because that null handle is exactly
        // what stranded it — see OrbRun.Release.
        if (orbRun != null) spawner.StopCoroutine(orbRun);

        yield return null;
    }

    // Seconds from one pair going out to the next. Every burst flies for the same
    // time, so this is equally the gap between one pair LANDING and the next —
    // which is the only reason switchWindow can be authored as a plain number of
    // seconds of thinking time.
    private float PairPeriod =>
        Mathf.Max(0, rocksPerBurst - 1) * Mathf.Max(0f, rockSpacing) + Mathf.Max(0.1f, switchWindow);

    // Is the fight over? Only when every rider has been sent and every one of
    // them is dead — until then the bursts keep coming, however long the knights
    // take. A rider still queued behind his ten seconds counts as alive, or a
    // shift would fall quiet in the gaps between entrances.
    //
    // With no track under the wave — this asset played somewhere with no rails —
    // there are no riders to gate on, so the bursts run out the schedule the
    // riders would have taken rather than stopping after one pair.
    private bool PressureOver(RailNetwork rails, float fallbackUntil)
    {
        if (rails == null) return Time.time >= fallbackUntil;
        return _allRidersReleased && !AnyRiderAlive();
    }

    // Live riders, compacting the dead out as it goes. A destroyed GameObject
    // compares equal to null through Unity's overload, which is exactly the test
    // wanted here: the rider's object is destroyed on death and his wreck is a
    // different object, so this counts gnomes rather than iron.
    private bool AnyRiderAlive()
    {
        for (int i = _riders.Count - 1; i >= 0; i--)
        {
            if (_riders[i] == null) _riders.RemoveAt(i);
        }
        return _riders.Count > 0;
    }

    // One pair: a line at some angle for the left knight, and its reflection for
    // the right, in the air at the same instant.
    private void FirePair(Spawner spawner)
    {
        float angle = NextAngle();
        FireBurst(spawner, spawner.LeftPlayer, angle);
        FireBurst(spawner, spawner.RightPlayer, 180f - angle);
    }

    private void FireBurst(Spawner spawner, Transform knight, float angleDegrees)
    {
        if (knight == null) return;

        Vector2 aim = (Vector2)knight.position + AimOffset;
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 outward = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        Vector2 from = EdgePoint(aim, outward);

        // Derived per burst rather than authored: the rectangle is not a circle,
        // so the same flight time is a different speed from overhead than from
        // the side. Each knight's own distance is used, so the two halves of a
        // pair land together even if the knights are not perfectly symmetric.
        float speed = Vector2.Distance(from, aim) / Mathf.Max(0.1f, flightSeconds);

        spawner.SpawnProjectileStraight(from, knight, Mathf.Max(1, rocksPerBurst),
                                        Mathf.Max(0f, rockSpacing), 0f, speed);
    }

    // Where a ray out of the knight leaves the spawn rectangle. The nearer of the
    // two axis crossings is the one on the boundary; the other is past a corner.
    private static Vector2 EdgePoint(Vector2 from, Vector2 outward)
    {
        float tx = Mathf.Abs(outward.x) > 0.0001f
            ? ((outward.x > 0f ? SpawnHalfWidth : -SpawnHalfWidth) - from.x) / outward.x
            : float.PositiveInfinity;

        float ty = Mathf.Abs(outward.y) > 0.0001f
            ? ((outward.y > 0f ? SpawnHalfHeight : -SpawnHalfHeight) - from.y) / outward.y
            : float.PositiveInfinity;

        return from + outward * Mathf.Min(tx, ty);
    }

    // The next angle for the LEFT knight, walking the half of the board he owns
    // by a golden stride. Fixed sequence, no dice — and the stride's own property
    // is the design here: consecutive samples are never closer than 0.382 of the
    // range, which across 180 degrees is a swing of at least 69, so no pair ever
    // arrives from roughly where the last one did.
    private float NextAngle()
    {
        _burst++;
        float t = _burst * Golden;
        return BandStart + (t - Mathf.Floor(t)) * BandWidth;
    }

    // One rider at a time, all from the same mouth. They are NOT dealt across the
    // layout's runs: on a loop every run is the same track a lap apart, so
    // spreading them would have gnomes appearing on all four sides of the board
    // at once instead of driving in from off-frame.
    private IEnumerator ReleaseRiders(RailNetwork rails, float trackDelay)
    {
        int count = Mathf.Max(0, gnomes);

        if (rails == null || count == 0)
        {
            // Set on every path out, and as load-bearing as anything here: while
            // this flag is false the bursts are still waiting on a rider who is
            // yet to enter, so a release that returns without setting it leaves
            // the wave firing rock forever with nothing left to kill.
            _allRidersReleased = true;
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, firstRiderAt));

        for (int i = 0; i < count; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[Crossfire] The rim went out from under the wave — the rest of the shift stays underground.");
                break;
            }

            GameObject prefab = NextOf(gnomeCarts, ref _gnome);
            if (prefab != null)
            {
                int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);
                MineCart cart = rails.SpawnCart(prefab, line, MineCart.TrackSpeed);
                // The rider IS the cart object — EnemyMineCart requires MineCart
                // on itself — so this is the thing that goes null when he dies
                if (cart != null) _riders.Add(cart.gameObject);
            }

            if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.1f, gnomeInterval));
        }

        _allRidersReleased = true;
    }

    // Round the authored list, carrying the cursor across the whole wave so that
    // two riders of the same tier are not always the same rider twice.
    private static GameObject NextOf(List<GameObject> list, ref int cursor)
    {
        if (list == null || list.Count == 0) return null;
        return list[cursor++ % list.Count];
    }
}

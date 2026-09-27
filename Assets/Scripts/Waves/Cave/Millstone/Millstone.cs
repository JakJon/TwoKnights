using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Mine's gate: two rings of iron, one inside the other, packed nose to tail
// and turning against each other with the knights caught in the middle.
//
// It is a boss without a boss in it. The Rat King is one thing with a lot of hit
// points; this is a hundred small things arranged into a single problem, and the
// problem is a WALL. Both rings close completely — there is not one gap anywhere
// on either of them when they finish laying — so the fight opens with every shot
// the knights own stopping dead in the inner ring, one unit from where it left
// the bow. Nothing on the outside can be reached until the knights have made a
// hole, and holes are made by spending arrows on iron that is worth nothing.
//
// THE POWDER IS THE WAY THROUGH, and that is the whole design. The inner ring is
// empty carts with a keg every ninth slot: twenty hit points of nothing, or ten
// hit points that answers back. Popping one costs a single arrow, takes its own
// slot out of the wall, and puts fifteen damage into everything within reach —
// which, at one unit of separation, is five or six riders on the outer ring going
// past at that moment. One arrow for a third of a shift is the best trade in the
// mine, and the knights have exactly five kegs to spend.
//
// It is also the worst trade in the mine if taken carelessly. The inner ring runs
// at the same height as the Mine Circuit, so a keg popped while it is over a
// knight's head catches the knight for the same fifteen (EnemyKegCart's radius is
// measured for exactly this). The wave asks one question over and over: is this
// keg where I need the hole, or where I am standing?
//
// The rings turn OPPOSITE ways, which is what stops that question from having a
// permanent answer. A hole burned in the inner wall does not stay pointed at
// anything — it sweeps one way while the shift it uncovered sweeps the other, so
// a firing line the knights opened is a firing line they have to keep re-earning.
//
// What ends the wave is the riders and nothing else. Empty carts, wrecks and
// spent kegs are not kills the player owes (EnemyMineCart), so the fight finishes
// with both rings still turning, full of iron, exactly as they arrived.
//
// Everything here is fixed (no dice — see the no-randomness pillar): the ring
// patterns repeat slot for slot, the fill order is inner then outer, and the
// shafts work a four-position rotation for as long as the shift is up.
//
// The arithmetic of a closed ring, which is geometry and not feel:
//   * Slot spacing is one cell over the cart speed. Any longer and the ring does
//     not close; any shorter and the carts shoulder each other apart on arrival.
//   * How many slots there ARE is a property of the ring, so it is measured off
//     the track (TryMeasureLoop) rather than authored. Mine Millstone comes out
//     at 52 cells outside and 44 inside. Neither divides its pattern exactly, so
//     one block round the back of each ring runs short — a lone rider outside, a
//     slightly early keg inside. That is a seam, not a bug, and it is the only
//     place either ring is not perfectly regular.
[CreateAssetMenu(fileName = "Millstone", menuName = "Waves/The Millstone (Boss)")]
public class Millstone : BaseWave
{
    // One closed ring of track and what goes round it. Both rings are the same
    // machine with different cargo, so they are one authored type rather than two
    // halves of the wave script.
    [System.Serializable]
    public class Ring
    {
        [Tooltip("Authoring only — names the ring in the inspector and the dev log")]
        public string label;

        [Tooltip("Index into the layout's runs. Every cart on this ring enters here, off-frame, and rolls in.")]
        public int entryRun;

        [Tooltip("One slot per cart, repeated round the ring. A null entry costs a slot and spawns nothing, which is how a ring is authored with a permanent hole in it.")]
        public List<GameObject> pattern = new List<GameObject>();

        [Tooltip("Fallback slot count, used only if this run is not a closed loop. On a real ring the count is measured off the track so the ring always closes exactly.")]
        public int fallbackSlots = 40;
    }

    // One gear the wheel changes into as the shift is cut down. Authored as data
    // because the whole ladder wants to be read at once — "half of them, then
    // three, then the last one" is a shape, and a shape belongs in a list rather
    // than in three fields with three names.
    [System.Serializable]
    public class Surge
    {
        [Tooltip("Authoring only — names the step in the inspector and the dev log")]
        public string label;

        [Tooltip("Fires once this many riders or fewer are left. -1 uses the fraction below instead, which is how a step says 'half of them' without knowing how big the shift is.")]
        public int atRidersLeft = -1;

        [Tooltip("Used only when At Riders Left is -1: fires at this share of the shift that came out, or fewer. 0.5 = half of them down.")]
        public float atFractionOfShift = 0.5f;

        [Tooltip("What the whole track runs at from here on, as a multiple of the mine's ordinary speed")]
        public float speedScale = 1.5f;
    }

    [Header("Track")]
    [Tooltip("Must carry BOTH rings as one layout — a RailNetwork lays one layout at a time, so two concentric rings are eight runs in a single asset.")]
    [SerializeField] private RailLayout railLayout;

    [Header("The wheels")]
    [Tooltip("Closed first, and closed completely: the wall goes up before anything arrives to hide behind it.")]
    [SerializeField] private Ring innerRing = new Ring();

    [Tooltip("The shift. These are the only kills the wave waits on.")]
    [SerializeField] private Ring outerRing = new Ring();

    [Tooltip("Seconds after the last rail piece lands before the first cart rolls in")]
    [SerializeField] private float firstCartDelay = 0.5f;

    [Tooltip("Quiet between the inner ring closing and the shift setting off. The one beat the knights get to read the wall before there is anything behind it.")]
    [SerializeField] private float betweenRings = 1f;

    [Header("The shafts")]
    [Tooltip("Seconds between shots down the shafts. They work from the moment the track lands until the last rider is down — the inner ring takes twenty seconds to close, and without this that is twenty seconds of nothing to do.")]
    [SerializeField] private float projectileInterval = 6f;

    [Tooltip("Seconds after the track lands before the first shot")]
    [SerializeField] private float firstShotAt = 3f;

    [Header("The wheel spins up")]
    [Tooltip("Gears the whole track changes into as the shift thins, in order. The wheel never slows back down — each step is earned by kills and kept.")]
    [SerializeField] private List<Surge> surges = new List<Surge>();

    [Tooltip("One blast of the whistle every time the wheel changes gear. It is the only warning the players get that the ring they just learned to read has stopped being the ring they learned.")]
    [SerializeField] private bool whistleOnSurge = true;

    [Header("Orbs")]
    [Tooltip("Crosses INSIDE both rings by default. Anywhere else and the orb is behind two walls of iron, which is not a trade — it is simply unreachable.")]
    [SerializeField] private OrbRun orbs = new OrbRun();

    // Where the projectile rotation stands. Reset at the top of every run — SO
    // state outlives the run that set it.
    private int _shot;

    // Riders actually put on the track this run, which is what "half the shift"
    // is half OF. Counted rather than read off the pattern so a ring whose fill
    // was cut short still surges against the shift the players really met.
    private int _ridersReleased;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _shot = 0;
        _ridersReleased = 0;

        var rails = spawner.Rails;
        if (rails == null)
        {
            Debug.LogWarning("[Millstone] No RailNetwork in the scene — there are no rings, so there is no gate.");
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock and the
        // first cart waits it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        // Opened before anything is released so the shafts have something to ask
        // about: the group is "the shift", and it stays open across both fills.
        // The inner ring joins nothing — carts do not gate wave completion, so
        // they never register — which is exactly what we want it to mean.
        BeginAmbush();

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));
        Coroutine shafts = projectileInterval > 0f
            ? spawner.StartCoroutine(WorkTheShafts(spawner, layDuration))
            : null;

        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, firstCartDelay));

        yield return spawner.StartCoroutine(Fill(rails, innerRing));
        yield return new WaitForSeconds(Mathf.Max(0f, betweenRings));
        yield return spawner.StartCoroutine(Fill(rails, outerRing));

        // As load-bearing as MarkSpawningComplete: a group that is never released
        // can never read as clear, and both the wait below and the shafts above
        // hang on it forever.
        MarkAmbushReleased();

        // The whole shift is out: if the orb timer has not paid yet, it pays now,
        // with the rings still full. See OrbRun.SendFirstIfWaiting.
        orbs.SendFirstIfWaiting(spawner);

        // Started only now, with the whole shift out. Watching the rider count
        // while the ring was still filling would read the first cart of a
        // forty-strong shift as "one gnome left" and put the wheel into top gear
        // before the fight had begun.
        Coroutine wheel = spawner.StartCoroutine(SpinUp(rails));

        yield return WaitForAmbushClear();

        if (wheel != null) spawner.StopCoroutine(wheel);
        if (shafts != null) spawner.StopCoroutine(shafts);
        spawner.StopCoroutine(orbRun);

        MarkSpawningComplete();
        yield return null;
    }

    // Lays one ring down slot by slot until it closes on itself. Nulls in the
    // pattern are honoured: an empty slot still costs its interval, so a ring can
    // be authored with a gap in it that survives the whole fight.
    private IEnumerator Fill(RailNetwork rails, Ring ring)
    {
        if (ring == null || ring.pattern == null || ring.pattern.Count == 0) yield break;

        if (rails.LineCount == 0)
        {
            Debug.LogWarning("[Millstone] The laid layout has no runs — there is no track to fill.");
            yield break;
        }

        // One cell of track per slot is what makes the carts touch. Derived rather
        // than authored: the spacing is a function of the track and the speed, and
        // a hardcoded figure stops closing the ring the moment either changes.
        float speed = MineCart.TrackSpeed;
        float cell = railLayout != null ? railLayout.CellSize : 1f;
        float interval = Mathf.Max(0.05f, cell / speed);

        int line = Mathf.Clamp(ring.entryRun, 0, rails.LineCount - 1);
        int slots = Mathf.Max(1, ring.fallbackSlots);

        float loop;
        if (rails.TryMeasureLoop(line, out loop))
        {
            slots = Mathf.Max(1, Mathf.RoundToInt(loop / cell));
        }
        else
        {
            Debug.LogWarning($"[Millstone] Run {line} ('{ring.label}') does not come back on itself, so the " +
                             $"ring cannot be measured and {slots} slots are being laid on faith. This wave " +
                             "wants a closed loop — check the layout's teleport table.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int remainder = slots % ring.pattern.Count;
        if (remainder != 0)
        {
            Debug.Log($"[Millstone] '{ring.label}' is {slots} cells round and the pattern is " +
                      $"{ring.pattern.Count} slots, so the block that closes the ring runs {remainder} " +
                      "slot(s) long instead of a full one. Expected — see the note above the class.");
        }
#endif

        for (int i = 0; i < slots; i++)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning($"[Millstone] The track went out from under '{ring.label}' — the rest of the ring stays underground.");
                yield break;
            }

            line = Mathf.Clamp(ring.entryRun, 0, rails.LineCount - 1);

            GameObject prefab = ring.pattern[i % ring.pattern.Count];
            if (prefab != null)
            {
                // Counted off what actually rolled out rather than off the
                // authored prefab: the mine may serve a nastier version of a
                // cart than the wave asked for (RailNetwork.Substitute), and a
                // shift is however many riders the players are really facing.
                MineCart rolled = rails.SpawnCart(prefab, line, speed);
                if (rolled != null && rolled.GetComponent<EnemyGnomeCart>() != null) _ridersReleased++;
            }

            if (i < slots - 1) yield return new WaitForSeconds(interval);
        }
    }

    // Winds the wheel up as the shift is cut down, one gear per step, and never
    // back. Watched per frame off the ambush's own live count — the group is the
    // shift, so this is asking the same question the wave already ends on.
    //
    // The gear change goes to the WHOLE track and not to the riders alone, which
    // is not a shortcut: both rings are packed nose to tail, so a subset running
    // faster would only shoulder the iron ahead of it (see RailNetwork.SpeedScale).
    // What the player is meant to see is a millstone turning faster because there
    // is less left in it, and that is a property of the wheel.
    private IEnumerator SpinUp(RailNetwork rails)
    {
        // One frame before anything is decided, and it is load-bearing for the
        // same reason it is in OrbRun: StartCoroutine hands back NULL for an
        // enumerator that finishes without ever yielding, and this one can — an
        // empty ladder, or a shift already cut past its last step before the
        // first frame. The wave would then StopCoroutine(null), the exception
        // would kill the wave coroutine where it stands, MarkSpawningComplete
        // would never run, and the run would hang on a wave that cannot end.
        yield return null;

        if (surges == null || surges.Count == 0 || rails == null) yield break;

        // Resolved once, against the shift that actually came out. Doing it per
        // frame would let a step's meaning drift while the fight ran.
        var thresholds = new int[surges.Count];
        for (int i = 0; i < surges.Count; i++)
        {
            Surge step = surges[i];
            thresholds[i] = step.atRidersLeft >= 0
                ? step.atRidersLeft
                : Mathf.Max(1, Mathf.FloorToInt(_ridersReleased * Mathf.Clamp01(step.atFractionOfShift)));
        }

        int next = 0;
        while (next < surges.Count)
        {
            // Several steps can fall to one kill — the last two riders dying
            // together crosses "three left" and "one left" on the same frame.
            // The wheel takes the highest gear earned and the whistle blows once,
            // rather than stacking two blasts on top of each other.
            int reached = -1;
            while (next < surges.Count && AmbushEnemiesRemaining <= thresholds[next])
            {
                reached = next;
                next++;
            }

            if (reached >= 0)
            {
                rails.SetSpeedScale(surges[reached].speedScale);
                Whistle();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Millstone] '{surges[reached].label}' — {AmbushEnemiesRemaining} of " +
                          $"{_ridersReleased} rider(s) left (step fires at {thresholds[reached]}); " +
                          $"the wheel is now at {surges[reached].speedScale:F2}x.");
#endif
            }

            if (next >= surges.Count) yield break;
            yield return null;
        }
    }

    private void Whistle()
    {
        if (!whistleOnSurge || AudioManager.Instance == null) return;
        AudioManager.Instance.PlaySFX(AudioManager.Instance.cartWhistle);
    }

    // Works the shafts from the moment the track lands until the shift is down.
    // Self-terminating as well as explicitly stopped, so a wave cut short cannot
    // leave it running on the Spawner into the next one.
    private IEnumerator WorkTheShafts(Spawner spawner, float layDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, layDuration) + Mathf.Max(0f, firstShotAt));

        float wait = Mathf.Max(0.1f, projectileInterval);

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
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.belowRightPlayer);
                break;
            case 2:
                spawner.SpawnProjectile(spawner.RightPlayer, spawner.aboveRightPlayer);
                break;
            default:
                spawner.SpawnProjectile(spawner.LeftPlayer, spawner.belowLeftPlayer);
                break;
        }
    }
}

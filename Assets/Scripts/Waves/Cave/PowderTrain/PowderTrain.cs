using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A ring of carts turning tight around the knights, most of it powder, and rats
// and bats closing in from outside it.
//
// Named for the real thing: a powder train is a trail of gunpowder laid down to
// carry a flame to a charge. That is exactly what the ring is — kegs packed nose
// to tail, so one arrow into the right cart runs the length of the train.
//
// THE WALL ONLY WORKS ONE WAY, and that is the whole wave. A cart eats arrows
// (EnemyMineCart) but nothing else collides with it, so the knights' shots stop
// at the ring while the rats walk straight through it. The knights are inside a
// fence that keeps them in and keeps nothing out. What they get instead is the
// gaps: two empty slots in every eight, coming round on a fixed schedule, and a
// shot has to be taken through one or not at all.
//
// And the ring is drawn tight on purpose. A keg going up on it is inside its own
// blast radius of the knights — so the answer to being fenced in is a thing that
// hurts to use. Blow the train to clear your firing line and you will pay for it.
//
// Every cadence is fixed (no dice — see the no-randomness pillar): the cart
// pattern repeats slot for slot around the loop, and the enemies outside arrive
// on a four-position rotation.
[CreateAssetMenu(fileName = "PowderTrain", menuName = "Waves/Powder Train")]
public class PowderTrain : BaseWave
{
    [Header("Track")]
    [Tooltip("A tight circuit — tight enough that a keg detonating on it reaches the knights")]
    [SerializeField] private RailLayout railLayout;

    [Header("The train")]
    [Tooltip("One slot per cart, repeated around the loop. Empty entries are the gaps the knights shoot through.")]
    [SerializeField] private List<GameObject> cartPattern = new List<GameObject>();

    [Tooltip("Fallback only, for a track that does not come back on itself. On a real loop the slot count is measured off the track so the train always closes exactly.")]
    [SerializeField] private int repeats = 3;

    [Tooltip("Index into the layout's runs — where the train enters")]
    [SerializeField] private int entryRun;

    [Tooltip("Seconds after the last rail piece lands before the train sets off")]
    [SerializeField] private float firstCartDelay = 0.5f;

    [Tooltip("Seconds between slots. Leave at 0 to derive it from the layout's cell size and the cart speed, which is the only way the train reliably closes up — hand-tuning it means re-tuning it every time either of those moves.")]
    [SerializeField] private float slotInterval;

    [Header("The enemies outside")]
    [Tooltip("Seconds between arrivals")]
    [SerializeField] private float enemyInterval = 3f;

    [Tooltip("Total arrivals over the wave. These are what the wave waits on.")]
    [SerializeField] private int enemyCount = 12;

    [Tooltip("Held back until the train has closed the ring, so the knights meet the fence before they meet the fight")]
    [SerializeField] private float enemyStartDelay = 6f;


    [Header("Rock")]
    [Tooltip("Cycled in order for the window below. THE FENCE ONLY WORKS ONE WAY — that is the wave's whole thesis, and rock is the sharpest statement of it: a cart eats the knights' arrows, but nothing stops a rock coming the other way through the ring. The guard has to answer while the gap it needed rotates past.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Length of the firing window, from the ring finishing its fall.")]
    [SerializeField] private float projectileWindow = 30f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("Crosses INSIDE the ring by default. Outside it the orb would sit behind the fence, shootable only through a gap — which turns a reward into a second puzzle competing with the one the wave is already asking.")]
    [SerializeField] private OrbRun orbs = new OrbRun();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        var rails = spawner.Rails;
        float layDuration = 0f;

        if (rails != null)
        {
            layDuration = rails.Lay(railLayout);
        }
        else if (railLayout != null)
        {
            Debug.LogWarning("[PowderTrain] No RailNetwork in the scene — there is no ring.");
        }

        Coroutine train = null;
        if (rails != null && cartPattern != null && cartPattern.Count > 0)
        {
            train = spawner.StartCoroutine(RunTrain(rails, layDuration));
        }

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));
        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner, layDuration));

        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, enemyStartDelay));

        float interval = Mathf.Max(0.1f, enemyInterval);
        for (int i = 0; i < enemyCount; i++)
        {
            SpawnOutsider(spawner, i);
            if (i < enemyCount - 1) yield return new WaitForSeconds(interval);
        }

        // Last outsider out: if the orb timer has not paid yet, it pays now,
        // with the ring still full. See OrbRun.SendFirstIfWaiting.
        orbs.SendFirstIfWaiting(spawner);

        if (train != null) yield return train;
        yield return shafts;

        spawner.StopCoroutine(orbRun);

        MarkSpawningComplete();
        yield return null;
    }

    // The pattern is laid down slot by slot, gaps included: a null entry costs an
    // interval and spawns nothing, which is what carves the holes in the wall.
    private IEnumerator RunTrain(RailNetwork rails, float layDuration)
    {
        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, firstCartDelay));

        if (rails.LineCount == 0)
        {
            Debug.LogWarning("[PowderTrain] The laid layout has no runs — there is no track for a train.");
            yield break;
        }

        // One cell of track per slot is what makes the carts touch. Derived rather
        // than authored: the spacing is a function of the track and the speed, and
        // a hardcoded figure silently stops closing the ring the moment either
        // changes — leaving a train with a seam in it that looks like a bug.
        float speed = MineCart.TrackSpeed;
        float cell = railLayout != null ? railLayout.CellSize : 1f;
        float interval = Mathf.Max(0.05f, slotInterval > 0f ? slotInterval : cell / speed);

        // How many carts it takes to fill the ring is a property of the RING, so
        // ask it. Authoring the repeat count by hand means re-deriving it every
        // time the loop is resized, and getting it wrong leaves a seam that reads
        // as a bug rather than as a gap.
        int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);
        int slots = cartPattern.Count * Mathf.Max(1, repeats);

        float loop;
        if (rails.TryMeasureLoop(line, out loop))
        {
            slots = Mathf.Max(1, Mathf.RoundToInt(loop / cell));
            if (slots % cartPattern.Count != 0)
            {
                Debug.LogWarning($"[PowderTrain] The ring is {slots} cells round, which the " +
                                 $"{cartPattern.Count}-slot pattern does not divide — the train " +
                                 "will have an odd join. Resize the loop or the pattern.");
            }
        }

        for (int i = 0; i < slots; i++)
        {
            if (rails.LineCount == 0) yield break;
            line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);

            GameObject prefab = cartPattern[i % cartPattern.Count];
            if (prefab != null) rails.SpawnCart(prefab, line, MineCart.TrackSpeed);

            if (i < slots - 1) yield return new WaitForSeconds(interval);
        }
    }

    // Four-position rotation, alternating sides and alternating ground with air,
    // so both knights are worked and neither the shield nor the bow goes idle
    private void SpawnOutsider(Spawner spawner, int index)
    {
        switch (index % 4)
        {
            case 0:
                spawner.SpawnRat(spawner.leftOfLeftPlayer, 0f, spawner.LeftPlayer);
                break;
            case 1:
                spawner.SpawnBat(spawner.topRightCorner, 0f);
                break;
            case 2:
                spawner.SpawnRat(spawner.rightOfRightPlayer, 0f, spawner.RightPlayer);
                break;
            default:
                spawner.SpawnBat(spawner.topLeftCorner, 0f);
                break;
        }
    }

    // Held until the ring has finished landing: rock arriving through a fence
    // that is not there yet reads as the wave misfiring rather than as the point.
    private IEnumerator WorkTheShafts(Spawner spawner, float layDuration)
    {
        if (layDuration > 0f) yield return new WaitForSeconds(layDuration);
        yield return RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed);
    }
}

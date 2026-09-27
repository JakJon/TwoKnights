using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A supply run. Carts come up out of the dark carrying something under a grate,
// take the long way round a horseshoe, and let it out at a flag near the far end.
//
// The wave is a race, and it is deliberately a race the player can see all of.
// The route is long on purpose: a cart is in view for the better part of fifteen
// seconds, the flag is planted where everyone can read it, and the arithmetic —
// can I spare the arrows to stop that one before it gets there? — is the whole
// wave. Every cart stopped short is an enemy that never exists.
//
// Nothing here rolls dice. Which cart carries what is authored slot by slot, so
// the same run of this wave always brings the same things in the same order (see
// the no-randomness pillar); what changes between attempts is only how much of
// it the knights managed to intercept.
[CreateAssetMenu(fileName = "Delivery", menuName = "Waves/Delivery")]
public class Delivery : BaseWave
{
    // What one volley out of the shafts looks like. The rocks travel at 1.75 u/s
    // (rockSpeed, a per-wave override of the shared prefab's 1) and a shaft is 7
    // units up, so every one of these is FOUR SECONDS in the air and visible for
    // all of it — which is why the patterns can afford to be intricate. It was
    // seven, and seven was three seconds of a rock the player had already solved
    // still hanging between them and the carts they were trying to read.
    //
    // Nothing here is a reflex test; the pattern is not deciding whether a knight
    // can react, it is deciding when a knight is allowed to look away from the
    // track. Every shape below is built out of shots at the same 7-unit radius, so
    // they all land in the same order they were fired.
    [Header("Track")]
    [Tooltip("A horseshoe: up one side, across, down the other. Its drop flags are where the cargo comes out.")]
    [SerializeField] private RailLayout railLayout;

    [Header("Carts")]
    [Tooltip("Released in this order from the mouth of the route. Each needs an EnemyDeliveryCart. Empty entries leave a gap.")]
    [SerializeField] private List<GameObject> cartOrder = new List<GameObject>();

    [Tooltip("Index into the layout's runs — the mouth of the horseshoe")]
    [SerializeField] private int entryRun;

    [Tooltip("Seconds after the last rail piece lands before the first cart sets off")]
    [SerializeField] private float firstCartDelay = 0.5f;

    [Tooltip("Seconds between carts. The route is open rather than a loop, so this really is a gap and not a permanent spacing.")]
    [SerializeField] private float cartInterval = 4f;

    [Header("Pressure")]
    [Tooltip("Cycled in order for as long as the firing window lasts, then round again from the top. Empty turns the shafts off entirely.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Length of the firing window. New volleys stop being issued once it elapses; whatever is already in the air still falls.")]
    [SerializeField] private float projectileWindow = 24f;

    [Tooltip("World units per second for THIS wave's rocks, overriding the prefab's 1. A shaft is 7 units up, so 1.75 puts a rock on the knight in four seconds instead of seven. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("The flanks")]
    [Tooltip("Rats dropped in off the TOP edge, in groups. The horseshoe and the shafts both live overhead, so the vermin arrive into the same half of the dial the rocks do — the wave stops being 'watch up' and becomes 'watch up at what'. Each group carries its own lead-in, which is how they are staggered.")]
    [SerializeField] private List<RatFlank> flanks = new List<RatFlank>();

    [Tooltip("Once the authored consignment is spent, carts keep setting off while a flank rat is still alive. Leaving the vermin means answering deliveries for as long as you take over them.")]
    [SerializeField] private bool freightWaitsOnTheVermin = true;

    [Header("Ogres")]
    [Tooltip("Brutes walking in from the edges, alternating sides. They ignore everything this wave is about and come straight for whichever knight they entered nearest — see OgreBand. Leave the count at 0 for a tier that should not have any.")]
    [SerializeField] private OgreBand ogres = new OgreBand();

    // Every ogre this wave put out. Cleared as the band is released — the asset
    // is a ScriptableObject and outlives the run.
    private readonly List<GameObject> _ogres = new List<GameObject>();

    [Header("Orbs")]
    [Tooltip("Crosses the bottom of the view, well clear of the horseshoe overhead — so taking one means turning a knight all the way down and off the route he is supposed to be watching.")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, -4.5f), to = new Vector2(12f, -4.5f) };

    // Every rat this wave put out. Cleared at the top of the wave — the asset is
    // a ScriptableObject and outlives the run.
    private readonly List<GameObject> _vermin = new List<GameObject>();

    // False until every flank has finished arriving. Without it an empty
    // roster during a group's lead-in reads as "the vermin are dead".
    private bool _verminReleased;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _vermin.Clear();
        _verminReleased = false;

        var rails = spawner.Rails;
        float layDuration = 0f;

        if (rails != null)
        {
            layDuration = rails.Lay(railLayout);
        }
        else if (railLayout != null)
        {
            Debug.LogWarning("[Delivery] No RailNetwork in the scene — there is no route to run.");
            MarkSpawningComplete();
            yield break;
        }

        Coroutine carts = null;
        if (rails != null && cartOrder != null && cartOrder.Count > 0)
        {
            carts = spawner.StartCoroutine(ReleaseCarts(rails, layDuration));
        }

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));
        Coroutine vermin = spawner.StartCoroutine(ReleaseTheFlanks(spawner));
        Coroutine brutes = spawner.StartCoroutine(ReleaseTheOgres(spawner));

        // The shafts keep the shields honest while the knights are busy doing
        // arithmetic about the carts — without it, a delivery route is a shooting
        // gallery with a timer
        yield return spawner.StartCoroutine(WorkTheShafts(spawner));

        if (carts != null) yield return carts;
        yield return vermin;
        yield return brutes;

        // Everything is out. If the orb timer somehow has not paid yet, it pays
        // now rather than being stopped unpaid. See OrbRun.SendFirstIfWaiting.
        orbs.SendFirstIfWaiting(spawner);
        spawner.StopCoroutine(orbRun);

        MarkSpawningComplete();
        yield return null;
    }

    private IEnumerator ReleaseCarts(RailNetwork rails, float layDuration)
    {
        yield return new WaitForSeconds(layDuration + Mathf.Max(0f, firstCartDelay));

        if (rails.LineCount == 0)
        {
            Debug.LogWarning("[Delivery] The laid layout has no runs — there is no track for a cart to ride.");
            yield break;
        }

        float interval = Mathf.Max(0.1f, cartInterval);

        for (int i = 0; i < cartOrder.Count; i++)
        {
            if (rails.LineCount == 0) yield break;
            int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);

            GameObject prefab = cartOrder[i];
            if (prefab != null) rails.SpawnCart(prefab, line, MineCart.TrackSpeed);

            if (i < cartOrder.Count - 1) yield return new WaitForSeconds(interval);
        }

        yield return KeepDeliveringWhileVerminLive(rails, interval);
    }

    // The consignment is spent but the vermin are not, so the route keeps
    // running. That is what makes the flank a decision: every second the rats
    // are left alive is another delivery to stop.
    private IEnumerator KeepDeliveringWhileVerminLive(RailNetwork rails, float interval)
    {
        if (!freightWaitsOnTheVermin || cartOrder == null || cartOrder.Count == 0) yield break;

        int i = 0;
        while (!_verminReleased || !RatFlank.AllDead(_vermin))
        {
            yield return new WaitForSeconds(interval);
            if (rails.LineCount == 0) yield break;

            GameObject prefab = cartOrder[i++ % cartOrder.Count];
            if (prefab == null) continue;

            int line = Mathf.Clamp(entryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(prefab, line, MineCart.TrackSpeed);
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

    // Round and round the authored list until the window runs out. The rest is
    // taken AFTER the volley has finished spawning rather than from when it
    // started, so restAfter means what it says however long the volley itself
    // takes — otherwise a five-rock fan would silently eat its own window.
    private IEnumerator WorkTheShafts(Spawner spawner)
    {
        yield return RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed);
    }


    // Beside the wave, never inside it. Ogres do not belong to any shift (see
    // EnemyOgre.JoinsAmbushes) — they are a clock running underneath whatever
    // else the wave is doing, and the wave is not finished until they are down.
    private IEnumerator ReleaseTheOgres(Spawner spawner)
    {
        _ogres.Clear();
        yield return ogres.Release(spawner, _ogres);
    }
}

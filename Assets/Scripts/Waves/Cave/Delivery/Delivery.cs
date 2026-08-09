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
    public enum VolleyShape
    {
        Single, // one rock, one knight
        Pair,   // two down the same shaft — the second lands while the first is still being answered
        Split,  // both knights at once. Nobody is shooting anything during this one
        Flip,   // same knight, above then below: the guard has to cross the whole dial
        Fan     // a spread that walks outward around the knight's own half
    }

    [System.Serializable]
    public class Volley
    {
        [Tooltip("Authoring only")]
        public string label;

        public VolleyShape shape = VolleyShape.Single;

        [Tooltip("Which knight. Split takes both and ignores this.")]
        public bool rightKnight;

        [Tooltip("Mirror the volley to come up from underneath. Flip always uses both and ignores this.")]
        public bool fromBelow;

        [Tooltip("Seconds between shots inside the volley — used by Pair, Flip and Fan. Because flight time is identical for every shot, this is also the gap between their ARRIVALS.")]
        public float spacing = 1f;

        [Tooltip("Fan only: how many rocks in the spread")]
        public int count = 3;

        [Tooltip("Fan only: degrees it sweeps, from straight overhead outward to the knight's own side. Hard-capped at 80 — past that the spread reaches round to the other knight's half and gets absorbed by the wrong guard.")]
        public float fanDegrees = 60f;

        [Tooltip("THE FIRING WINDOW: quiet after this volley's last shot. This is the knob that keeps the wave fair — a delivery cart is 20 HP, two arrows, one and a half seconds apart, so anything under ~3s is a window nobody can kill a cart in.")]
        public float restAfter = 3.5f;
    }

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
    [SerializeField] private List<Volley> volleys = new List<Volley>();

    [Tooltip("Length of the firing window. New volleys stop being issued once it elapses; whatever is already in the air still falls.")]
    [SerializeField] private float projectileWindow = 24f;

    [Tooltip("World units per second for THIS wave's rocks, overriding the prefab's 1. A shaft is 7 units up, so 1.75 puts a rock on the knight in four seconds instead of seven. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("Crosses the bottom of the view, well clear of the horseshoe overhead — so taking one means turning a knight all the way down and off the route he is supposed to be watching.")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, -4.5f), to = new Vector2(12f, -4.5f) };

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

        // The shafts keep the shields honest while the knights are busy doing
        // arithmetic about the carts — without it, a delivery route is a shooting
        // gallery with a timer
        yield return spawner.StartCoroutine(WorkTheShafts(spawner));

        if (carts != null) yield return carts;

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
    }

    // Round and round the authored list until the window runs out. The rest is
    // taken AFTER the volley has finished spawning rather than from when it
    // started, so restAfter means what it says however long the volley itself
    // takes — otherwise a five-rock fan would silently eat its own window.
    private IEnumerator WorkTheShafts(Spawner spawner)
    {
        if (volleys == null || volleys.Count == 0) yield break;

        float until = Time.time + Mathf.Max(0f, projectileWindow);
        int index = 0;

        while (Time.time < until)
        {
            Volley volley = volleys[index % volleys.Count];
            index++;

            if (volley == null) continue;

            float busy = Fire(spawner, volley);
            yield return new WaitForSeconds(busy + Mathf.Max(0f, volley.restAfter));
        }
    }

    // Every shape is built from shots aimed at ONE knight and originating on that
    // knight's own half, so a volley never crosses the other guard (hard rule) —
    // the Fan's sweep is clamped for the same reason.
    private float Fire(Spawner spawner, Volley volley)
    {
        Transform knight = volley.rightKnight ? spawner.RightPlayer : spawner.LeftPlayer;
        Vector2 above = volley.rightKnight ? spawner.aboveRightPlayer : spawner.aboveLeftPlayer;
        Vector2 below = volley.rightKnight ? spawner.belowRightPlayer : spawner.belowLeftPlayer;
        Vector2 from = volley.fromBelow ? below : above;
        float spacing = Mathf.Max(0.05f, volley.spacing);

        switch (volley.shape)
        {
            case VolleyShape.Pair:
                spawner.SpawnProjectileStraight(from, knight, 2, spacing, 0f, rockSpeed);
                return spacing;

            case VolleyShape.Split:
                spawner.SpawnProjectile(spawner.LeftPlayer,
                    volley.fromBelow ? spawner.belowLeftPlayer : spawner.aboveLeftPlayer, 0f, rockSpeed);
                spawner.SpawnProjectile(spawner.RightPlayer,
                    volley.fromBelow ? spawner.belowRightPlayer : spawner.aboveRightPlayer, 0f, rockSpeed);
                return 0f;

            case VolleyShape.Flip:
                spawner.SpawnProjectile(knight, above, 0f, rockSpeed);
                spawner.SpawnProjectile(knight, below, spacing, rockSpeed);
                return spacing;

            case VolleyShape.Fan:
            {
                int count = Mathf.Max(2, volley.count);

                // Outward, into the knight's own half. Which way that is depends on
                // BOTH which knight it is and which side of him the fan starts on —
                // sweeping the wrong way walks the spread across the middle of the
                // board and into the other knight's shield.
                var direction = (volley.rightKnight ^ volley.fromBelow)
                    ? Spawner.ArcDirection.Clockwise
                    : Spawner.ArcDirection.CounterClockwise;

                spawner.SpawnProjectileArc(knight, direction, from,
                    Mathf.Clamp(volley.fanDegrees, 10f, 80f), count, spacing, 1, 0f, rockSpeed);
                return spacing * (count - 1);
            }

            default:
                spawner.SpawnProjectile(knight, from, 0f, rockSpeed);
                return 0f;
        }
    }
}

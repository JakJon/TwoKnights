using UnityEngine;

// The knight's Ember stat sheet: every Ember Order upgrade writes into this
// component. PlayerShooter reads it when arrows fire (ignite roll + fireball
// cadence), SwordSwing reads it per swing (Firebrand), and EnemyBase reads the
// igniting knight's sheet while an enemy burns (trail size, panic speed).
//
// THE IGNITION PILLAR: an enemy can only be ignited by a fireball or an arrow
// that rolled ignite. Fire zones deal flat damage and NEVER ignite. Every
// ignition on the field is one the player chose to grant — see
// Docs/Design/ember-order.md. Nothing here or in FireField may relax that.
public class EmberBoost : MonoBehaviour
{
    // --- Ignited Tips: the arrow door ---
    private float igniteChance = 0f; // Percentage chance (0-100) per projectile

    // --- Fireball: the rhythm door ---
    private int fireballEveryNShots = 0; // 0 = no fireballs
    private float fireballBlastRadius = 1.5f;
    private GameObject fireballPrefab;
    private int shotCounter = 0;

    // --- Firebrand: the sword door ---
    private float firebrandChance = 0f; // Percentage chance (0-100) per swing
    private int firebrandCount = 0; // Fireballs hurled when it procs

    // --- Fire Trail / Searing Panic ---
    private int fireTrailLevel = 0; // 0 = off, 1-2
    private int searingPanicLevel = 0; // 0 = off, 1-2

    // --- Scorched Earth (capstone) ---
    private bool scorchedEarth = false;

    // How long an enemy stays lit. Deliberately short, and deliberately NOT a knob
    // to reach for when fire feels weak: 8s is about how long an enemy is in front
    // of you, so a burn is spent inside the encounter that started it rather than
    // trailing off after. Poison is the one that outlives its target.
    public const float IgniteDuration = 8f;

    // Zone damage: base 3.0, +0.5 per Fire Trail rank, +0.5 per Searing Panic
    // rank, so a full Ember build burns at 5.0 dps. Tuning knob #1. This is also
    // the dps ONE burn stack contributes to an ignited enemy — each extra ignite
    // adds another stack of this size (see EnemyBase burn stacks).
    //
    // Sits ABOVE poison's per-tick rate on purpose. The two are meant to feel
    // different rather than balanced tick-for-tick: fire kills a 20 HP rat 1.7s
    // sooner after the same arrow, poison carries far more total damage it can only
    // collect from something that lives long enough.
    public const float BaseZoneDps = 3f;
    private const float DpsPerRank = 0.5f;

    // ---- Fire spread ----
    //
    // NOTHING CATCHES FIRE FROM FIRE any more (owner's call, 2026-09-07). Fire spread
    // is now purely a DAMAGE rule: a burning body cooks the bodies pressed against it
    // at its own dps, and that is all it does to them. They never light, so they never
    // cook their own neighbours in turn.
    //
    // It used to hand over a burn, kept finite by a source rule — every burn had an id
    // and could light a given enemy only once. Finite is not the same as controlled:
    // one ignited arrow into a packed lane still lit the lane, and each newly-lit body
    // lit the ones behind it, so the fire on the board stopped being anything the
    // player had chosen to grant. The ignition doors are back to arrow and fireball,
    // for the field (see GroundFireIgnites) and for bodies alike.
    //
    // The source rule and its ids survive because ground fire still uses them if
    // GroundFireIgnites is ever turned back on. Turn THIS off and a burning body stops
    // touching its neighbours at all; the dps and duration numbers above stand alone.
    // A static rather than a const so it can be flipped mid-run from a debug console.
    public static bool FireSpreadEnabled = true;

    // GROUND FIRE DOES NOT LIGHT ANYTHING (owner's call, 2026-09-06). Burning
    // ground deals its damage and nothing else, whatever laid it down - a Fire
    // Trail lane, a placed zone, and A FIREBALL'S CRATER included. The crater was
    // the specific one worth naming: it was the one zone that always minted itself
    // a fresh ignition source, so a fireball into a crowd used to light everything
    // that walked over the mark for the next six seconds, and the fire on the board
    // stopped being anything the player had chosen to grant.
    //
    // This is the arrow-and-fireball ignition pillar back in force
    // (Docs/Design/ember-order.md): the only things that ignite an enemy are an
    // arrow that rolled ignite and a fireball landing on it. Body-to-body contact
    // was the last exception and it went the same way a day later - see the fire
    // spread block above.
    public static bool GroundFireIgnites = false;

    /// <summary>
    /// The source id a zone should carry. 0 means "this zone never ignites", which
    /// is what every zone gets while <see cref="GroundFireIgnites"/> is off - so
    /// the rule holds at the point zones are CREATED as well as where they are
    /// sampled, and a zone laid under the old rule cannot outlive the change.
    /// </summary>
    public static int NextZoneSourceId()
    {
        return (FireSpreadEnabled && GroundFireIgnites) ? NextFireSourceId() : 0;
    }

    // How close a burning body has to be to scorch another one
    public const float ContactSpreadRadius = 0.6f;

    // How often spread is evaluated, matching the fire damage tick so a burning enemy
    // costs one overlap query per beat rather than one per frame.
    public const float SpreadCheckInterval = 0.25f;

    // Identity for the source rule. Every burn and every igniting crater draws one,
    // so "has this fire already lit that enemy" is a set lookup and never a guess.
    // Minted here rather than in EnemyBase because craters have no burn to belong to.
    private static int _nextFireSourceId = 1;

    public static int NextFireSourceId()
    {
        return _nextFireSourceId++;
    }

    // Fireball crater. Twelve seconds rather than six (owner's call, 2026-09-06):
    // now that the crater cannot light anything, the only thing it contributes
    // after the blast is the ground it denies, and six seconds of that was gone
    // before the next thing walked over it. Doubling the burn is what the crater
    // is paid in instead of ignitions.
    public const float CraterRadius = 1.0f;
    public const float CraterDuration = 12f;

    // Equipment (Emberbrand, Cinder Crown) adds on top of what the Order earned.
    // Separate fields rather than inflating fireTrailLevel, which also controls
    // trail radius and whether trails drop at all.
    private float zoneDpsBonus;
    private float trailDurationBonus;

    public void AddZoneDpsBonus(float amount)
    {
        zoneDpsBonus = Mathf.Max(zoneDpsBonus, amount);
    }

    public void AddTrailDurationBonus(float seconds)
    {
        trailDurationBonus = Mathf.Max(trailDurationBonus, seconds);
    }

    public float ZoneDps
    {
        get { return BaseZoneDps + DpsPerRank * (fireTrailLevel + searingPanicLevel) + zoneDpsBonus; }
    }

    // ---- Ignited Tips ----

    // Tiers set absolute values (30/60/100); Max keeps a late re-pick from downgrading
    public void SetIgniteChance(float chance)
    {
        igniteChance = Mathf.Clamp(Mathf.Max(igniteChance, chance), 0f, 100f);
    }

    public bool ShouldIgnite()
    {
        return igniteChance > 0f && Random.Range(0f, 100f) < igniteChance;
    }

    public float IgniteChance { get { return igniteChance; } }

    // ---- Fireball ----

    public void SetFireball(int everyNShots, float blastRadius, GameObject prefab)
    {
        // Lower N = more frequent, so Min is the upgrade direction here
        fireballEveryNShots = fireballEveryNShots == 0
            ? Mathf.Max(1, everyNShots)
            : Mathf.Min(fireballEveryNShots, Mathf.Max(1, everyNShots));
        fireballBlastRadius = Mathf.Max(fireballBlastRadius, blastRadius);
        if (prefab != null) fireballPrefab = prefab;
    }

    // Deterministic cadence — the player can count to five and time the big one.
    // Called once per shot; returns true on every Nth.
    public bool AdvanceShotAndCheckFireball()
    {
        if (fireballEveryNShots <= 0) return false;
        shotCounter++;
        if (shotCounter >= fireballEveryNShots)
        {
            shotCounter = 0;
            return true;
        }
        return false;
    }

    // The tell that makes the cadence readable: true when the very next shot will
    // leave the shield as a fireball. PlayerShooter smoulders the shield while it
    // holds, so the player can read the rhythm off the screen instead of counting.
    public bool NextShotIsFireball
    {
        get { return fireballEveryNShots > 0 && shotCounter >= fireballEveryNShots - 1; }
    }

    // Firebrand also throws fireballs, so it supplies the prefab too — the chain is
    // gated behind Fireball I, but neither upgrade should depend on the other having
    // wired the reference.
    public void SetFireballPrefab(GameObject prefab)
    {
        if (prefab != null) fireballPrefab = prefab;
    }

    public bool HasFireball { get { return fireballEveryNShots > 0; } }
    public int FireballEveryNShots { get { return fireballEveryNShots; } }
    public float FireballBlastRadius { get { return fireballBlastRadius; } }
    public GameObject FireballPrefab { get { return fireballPrefab; } }

    // ---- Firebrand ----

    // Rank II deliberately does NOT raise the odds — it raises the count, so
    // Firebrand stays a payoff you can't fish for.
    public void SetFirebrand(float chance, int count)
    {
        firebrandChance = Mathf.Clamp(Mathf.Max(firebrandChance, chance), 0f, 100f);
        firebrandCount = Mathf.Max(firebrandCount, count);
    }

    public bool ShouldHurlFirebrand()
    {
        return firebrandCount > 0 && firebrandChance > 0f
            && Random.Range(0f, 100f) < firebrandChance;
    }

    public int FirebrandCount { get { return firebrandCount; } }

    // ---- Fire Trail ----

    public void SetFireTrailLevel(int level)
    {
        fireTrailLevel = Mathf.Max(fireTrailLevel, level);
    }

    public int FireTrailLevel { get { return fireTrailLevel; } }
    public bool HasFireTrail { get { return fireTrailLevel > 0; } }

    // Seconds between trail drops while an ignited enemy is moving
    public const float TrailDropInterval = 0.35f;

    public float TrailZoneRadius { get { return fireTrailLevel >= 2 ? 0.75f : 0.5f; } }
    public float TrailZoneDuration { get { return (fireTrailLevel >= 2 ? 7f : 4f) + trailDurationBonus; } }

    // ---- Searing Panic ----

    public void SetSearingPanicLevel(int level)
    {
        searingPanicLevel = Mathf.Max(searingPanicLevel, level);
    }

    public int SearingPanicLevel { get { return searingPanicLevel; } }

    // Movement multiplier applied to ignited enemies. Reads as a downside; with
    // Fire Trail it means more ground covered while burning = more field painted.
    public float PanicSpeedMultiplier
    {
        get
        {
            if (searingPanicLevel >= 2) return 1.6f;
            if (searingPanicLevel >= 1) return 1.35f;
            return 1f;
        }
    }

    // ---- Scorched Earth (capstone) ----

    public void EnableScorchedEarth()
    {
        scorchedEarth = true;
    }

    public bool ScorchedEarth { get { return scorchedEarth; } }

    // Convenience for every zone-placing site: one call, correct dps and persistence.
    // igniteSourceId is the fire this zone belongs to (0 = it never ignites), and
    // sourceEnemyId the body that dripped it, so that body can walk its own trail
    // without relighting itself. See the fire spread block above.
    public void PlaceZone(Vector2 position, float radius, float duration,
        int igniteSourceId = 0, int sourceEnemyId = 0)
    {
        FireField.AddZone(position, radius, duration, gameObject.tag, ZoneDps, scorchedEarth,
            false, igniteSourceId, sourceEnemyId);
    }

    /// <summary>
    /// Same as PlaceZone, but tagged as a Fire Trail drop so the Ember quest can
    /// ask for several burning at once without counting craters.
    /// </summary>
    public void PlaceTrailZone(Vector2 position, float radius, float duration,
        int igniteSourceId = 0, int sourceEnemyId = 0)
    {
        FireField.AddZone(position, radius, duration, gameObject.tag, ZoneDps, scorchedEarth,
            true, igniteSourceId, sourceEnemyId);
    }
}

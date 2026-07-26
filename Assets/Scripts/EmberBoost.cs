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

    // How long an enemy stays lit. Re-igniting refreshes rather than stacking:
    // ignition is a carrier state, not a damage-over-time effect.
    public const float IgniteDuration = 8f;

    // Zone damage: base 2.0, +0.5 per Fire Trail rank, +0.5 per Searing Panic
    // rank, so a full Ember build burns at 4.0 dps. Tuning knob #1. This is also
    // the dps ONE burn stack contributes to an ignited enemy — each extra ignite
    // adds another stack of this size (see EnemyBase burn stacks).
    public const float BaseZoneDps = 2f;
    private const float DpsPerRank = 0.5f;

    // Fireball crater
    public const float CraterRadius = 1.0f;
    public const float CraterDuration = 6f;

    public float ZoneDps
    {
        get { return BaseZoneDps + DpsPerRank * (fireTrailLevel + searingPanicLevel); }
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
    public float TrailZoneDuration { get { return fireTrailLevel >= 2 ? 7f : 4f; } }

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

    // Convenience for every zone-placing site: one call, correct dps and persistence
    public void PlaceZone(Vector2 position, float radius, float duration)
    {
        FireField.AddZone(position, radius, duration, gameObject.tag, ZoneDps, scorchedEarth);
    }
}

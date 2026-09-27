using UnityEngine;

// The knight's Ember stat sheet: every Ember Order upgrade writes into this
// component. PlayerShooter reads it when arrows fire (ignite roll + fireball
// cadence), SwordSwing reads it per swing (Firebrand), and EnemyBase reads the
// igniting knight's sheet while an enemy burns (trail size, panic speed).
//
// THE IGNITION PILLAR: an enemy can only be ignited by a fireball, an arrow that
// rolled ignite, or the Fire Sight beam. Nothing on the ground lights anything, and
// body-to-body contact does not either: burning ground and a burning body both COOK
// what they touch and nothing more. Every ignition on the field is one the player
// aimed. See Docs/Design/ember-order.md.
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
    private int firebrandLevel = 0; // 0 = off, 1-3

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

    // Zone damage, and the whole of the Order's dps ladder (retuned down again
    // 2026-09-08, ceiling 6.0 -> 4.5):
    //
    //   Base                3.00
    //   Fire Trail I       +0.00   (buys the lane, not the number)
    //   Fire Trail II      +0.25
    //   Searing Panic I    +0.25
    //   Searing Panic II   +0.50
    //   Scorched Earth     +0.50
    //   Full build          4.50
    //
    // Two thirds of the ceiling is now the BASE. Ember's upgrades buy reach — wider
    // lanes, more of them, fire that stays — and only trim the rate, which is what
    // keeps a fully-built field from deleting anything that touches it.
    //
    // Fire Trail I is the one pick in the Order worth no dps at all. It buys the lane
    // itself, which is the whole of what it is for.
    //
    // Tuning knob #1. This is also the dps ONE burn stack contributes to an ignited
    // enemy — each extra ignite adds another stack of this size (see EnemyBase burn
    // stacks) — and, separately, what the field channel bills for standing in it.
    //
    // Searing Panic is the only chain whose two ranks are worth different amounts. It
    // is the pick that reads as a downside, so rank I has to pay something the frame
    // it lands, and rank II is where it actually gets paid.
    //
    // Sits ABOVE poison's per-tick rate on purpose. The two are meant to feel
    // different rather than balanced tick-for-tick: fire kills a 20 HP rat 1.7s
    // sooner after the same arrow, poison carries far more total damage it can only
    // collect from something that lives long enough.
    public const float BaseZoneDps = 3f;
    private const float DpsFireTrail2 = 0.25f; // rank I is worth no dps at all
    private const float DpsPanicRank1 = 0.25f;
    private const float DpsPanicRank2 = 0.5f;
    private const float ScorchedEarthDps = 0.5f;

    // ---- Fire spread ----
    //
    // NO BODY CATCHES FIRE FROM ANOTHER BODY (owner's call, 2026-09-07, reaffirmed
    // 2026-09-08). Contact spread is purely a DAMAGE rule: a burning body cooks the
    // bodies pressed against it at its own dps, and that is all it does to them. An
    // ignited mob never ignites an unignited one.
    //
    // It used to hand over a burn, kept finite by a source rule — every burn had an id
    // and could light a given enemy only once. Finite is not the same as controlled:
    // one ignited arrow into a packed lane still lit the lane, and each newly-lit body
    // lit the ones behind it, so the fire on the board stopped being anything the
    // player had chosen to grant.
    //
    // Ground fire was briefly bought back by Scorched Earth and is shut again
    // (owner's call, 2026-09-08). Both halves of fire-from-fire are now off: neither
    // the floor nor a neighbouring body can light anything, whatever the player has
    // bought. The capstone keeps its eternal zones and its damage and gives up the
    // ignition.
    //
    // Turn THIS off and a burning body stops touching its neighbours at all, and zones
    // stop lighting anything whatever the capstone says; the dps and duration numbers
    // above stand alone. A static rather than a const so it can be flipped mid-run from
    // a debug console.
    public static bool FireSpreadEnabled = true;

    // GROUND FIRE DOES NOT LIGHT ANYTHING BY DEFAULT (owner's call, 2026-09-06).
    // Burning ground deals its damage and nothing else, whatever laid it down - a
    // Fire Trail lane, a placed zone, and A FIREBALL'S CRATER included. The crater
    // was the specific one worth naming: it was the one zone that always minted
    // itself a fresh ignition source, so a fireball into a crowd used to light
    // everything that walked over the mark for the next six seconds, and the fire on
    // the board stopped being anything the player had chosen to grant.
    //
    // SCORCHED EARTH BUYS THE EXCEPTION (owner's call, 2026-09-08). The capstone
    // turns the knight's own fire back into an ignition door: their zones are
    // eternal AND they light what stands in them. That is the whole shape of the
    // pick - the player stops aiming ignitions and starts painting a floor that
    // grants them. It stays bounded by the source rule (a given fire lights a given
    // body at most once), by the zone cap, and by the wave-end clear.
    //
    // Body-to-body contact is NOT part of that and does not come back: an ignited mob
    // cooks its neighbours, it does not light them - see the fire spread block above.
    // A mob reaches another mob only the long way round, by dripping a trail that mob
    // later walks into, which is ground fire doing it and is the point of the pick.
    //
    // This static is the ONLY thing that opens the ground-ignition door, and it is
    // off. No upgrade turns it on — it exists so the behaviour can be tried from a
    // debug console without restoring the machinery by hand. The plumbing behind it
    // (the zone's ignites bit, EnemyBase's fire-AoE flag) is live and correct; it
    // simply never fires.
    public static bool GroundFireIgnites = false;

    /// <summary>
    /// Whether THIS knight's zones light what stands in them — the global override and
    /// nothing else. Read where zones are CREATED as well as where they are sampled,
    /// so flipping the toggle mid-run cannot retroactively arm fire already on the
    /// floor.
    /// </summary>
    public bool ZonesIgnite
    {
        get { return FireSpreadEnabled && GroundFireIgnites; }
    }

    // How long the fire-AoE STATE outlives the fire that set it. Anti-chatter only:
    // it stops the flag flipping on and off as a body clips the edge of a lane. It does
    // NOT extend damage — EnemyBase bills the field channel for the beats a body is
    // actually standing in fire and no longer (owner's report, 2026-09-08; charging the
    // linger meant a graze cost as much as stopping in the fire).
    //
    // With ground ignition off, the state this debounces has no consumer, so the
    // constant is dormant. It is kept because the moment anything reads "is this body
    // alight from the floor" it will need exactly this debounce.
    public const float FireAoeLinger = 2f;

    // How close a burning body has to be to scorch another one
    public const float ContactSpreadRadius = 0.6f;

    // How often spread is evaluated, matching the fire damage tick so a burning enemy
    // costs one overlap query per beat rather than one per frame.
    public const float SpreadCheckInterval = 0.25f;

    // Fireball crater. Twelve seconds rather than six (owner's call, 2026-09-06):
    // now that the crater cannot light anything, the only thing it contributes
    // after the blast is the ground it denies, and six seconds of that was gone
    // before the next thing walked over it. Doubling the burn is what the crater
    // is paid in instead of ignitions.
    public const float CraterRadius = 1.0f;
    public const float CraterDuration = 12f;

    // Equipment (Emberbrand, Cinder Crown) adds on top of what the Order earned.
    // Separate fields rather than inflating fireTrailLevel, which also controls
    // trail radius and whether trails drop at all. Two items both add.
    private float zoneDpsBonus;
    private float zoneRadiusBonus;

    public void AddZoneDpsBonus(float amount)
    {
        zoneDpsBonus += Mathf.Max(0f, amount);
    }

    // Widens EVERY fire zone this knight lays down — trail drops and fireball
    // craters alike. Added where the zone is placed rather than folded into
    // TrailZoneRadius, so the crater gets it too and the trail ladder below stays
    // the Order's own numbers.
    public void AddZoneRadiusBonus(float units)
    {
        zoneRadiusBonus += Mathf.Max(0f, units);
    }

    public float ZoneDps
    {
        get
        {
            return BaseZoneDps
                + (fireTrailLevel >= 2 ? DpsFireTrail2 : 0f)
                + PanicZoneDps
                + (scorchedEarth ? ScorchedEarthDps : 0f)
                + zoneDpsBonus;
        }
    }

    // Not a per-rank multiple: rank II is worth twice rank I, so the ranks are added
    // up rather than scaled. Cumulative — buying II keeps what I gave.
    private float PanicZoneDps
    {
        get
        {
            if (searingPanicLevel >= 2) return DpsPanicRank1 + DpsPanicRank2;
            if (searingPanicLevel >= 1) return DpsPanicRank1;
            return 0f;
        }
    }

    // ---- Ignited Tips ----

    // Tiers set absolute values (30/60/100); Max keeps a late re-pick from downgrading
    public void SetIgniteChance(float chance)
    {
        igniteChance = Mathf.Clamp(Mathf.Max(igniteChance, chance), 0f, 100f);
    }

    // Equipment's ignite chance adds on top of the Ignited Tips tier, rather than
    // going through the monotonic setter above, which would swallow it the moment
    // a tier matched it. Items always stack.
    private float equipmentIgniteChance;

    public void AddIgniteChanceFromEquipment(float chance)
    {
        equipmentIgniteChance += Mathf.Max(0f, chance);
    }

    public bool ShouldIgnite()
    {
        float chance = IgniteChance;
        return chance > 0f && Random.Range(0f, 100f) < chance;
    }

    public float IgniteChance { get { return Mathf.Clamp(igniteChance + equipmentIgniteChance, 0f, 100f); } }

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

    // Firebrand also throws fireballs, so it supplies the prefab too — it is a
    // starting pick of its own (owner's call, 2026-09-25), so it cannot count on
    // Fireball having wired the reference.
    public void SetFireballPrefab(GameObject prefab)
    {
        if (prefab != null) fireballPrefab = prefab;
    }

    public bool HasFireball { get { return fireballEveryNShots > 0; } }
    public int FireballEveryNShots { get { return fireballEveryNShots; } }
    public float FireballBlastRadius { get { return fireballBlastRadius; } }
    public GameObject FireballPrefab { get { return fireballPrefab; } }

    // ---- Firebrand ----

    // Every swing lobs fire (owner's call, 2026-09-25 - it used to be a 20% roll
    // for one, two or three fireballs flying four seconds). The catch is reach: a
    // lobbed fireball comes down and bursts a short step in front of the blade.
    //   I   one fireball, lands 0.5u out
    //   II  one fireball, lands 1.5u out (three times as far)
    //   III two fireballs, 1.5u out
    public const float FirebrandShortLob = 0.5f;
    public const float FirebrandLongLob = 1.5f;

    public void SetFirebrand(int level)
    {
        firebrandLevel = Mathf.Max(firebrandLevel, level);
    }

    public bool ShouldHurlFirebrand() { return firebrandLevel > 0; }

    public int FirebrandLevel { get { return firebrandLevel; } }

    public int FirebrandCount { get { return firebrandLevel >= 3 ? 2 : (firebrandLevel > 0 ? 1 : 0); } }

    /// <summary>How far a Firebrand fireball travels before it comes down and bursts.</summary>
    public float FirebrandLobDistance { get { return firebrandLevel >= 2 ? FirebrandLongLob : FirebrandShortLob; } }

    // A lob is a smaller bang than a shot fireball (owner's call, 2026-09-25): half
    // the blast radius, and a crater half as wide that burns half as long. The blast
    // still grows with the Fireball chain, at half of whatever it has reached.
    public const float FirebrandBlastScale = 0.5f;
    public const float FirebrandCraterRadius = CraterRadius * 0.5f;
    public const float FirebrandCraterDuration = CraterDuration * 0.5f;

    public float FirebrandBlastRadius { get { return fireballBlastRadius * FirebrandBlastScale; } }

    // ---- Fire Trail ----

    public void SetFireTrailLevel(int level)
    {
        fireTrailLevel = Mathf.Max(fireTrailLevel, level);
    }

    public int FireTrailLevel { get { return fireTrailLevel; } }
    public bool HasFireTrail { get { return fireTrailLevel > 0; } }

    // Seconds between trail drops while an ignited enemy is moving
    public const float TrailDropInterval = 0.35f;

    // The lane-width ladder (retuned down 2026-09-08, ceiling 2.25u -> 1.75u). Four
    // picks widen a trail, each by its own amount rather than by a shared per-rank
    // step:
    //
    //   Fire Trail I     0.50   (the base lane)
    //   Fire Trail II   +0.25
    //   Searing Panic I +0.25
    //   Searing Panic II+0.25
    //   Scorched Earth  +0.25
    //   Top of the Order 1.50
    //
    // Every step is a quarter now, so the ladder is the base lane plus four equal
    // rungs. Fire Trail I still owns two thirds of the ceiling by itself: the lane is
    // the pick, and everything after it is a trim.
    //
    // Additive rather than multiplied so a step is worth the same wherever it is
    // bought, and so the table above IS the implementation.
    //
    // NOTE: FireField spreads a fixed particle budget over the whole field, so zones
    // this size draw somewhat thinner than small ones. The damage is unaffected — the
    // hitbox is the radius — but the fire looks sparser than it bites.
    private const float TrailRadiusBase = 0.5f;
    private const float TrailRadiusFireTrail2 = 0.25f;
    private const float TrailRadiusPanic1 = 0.25f;
    private const float TrailRadiusPanic2 = 0.25f;
    private const float TrailRadiusScorchedEarth = 0.25f;

    public float TrailZoneRadius
    {
        get
        {
            return TrailRadiusBase
                + (fireTrailLevel >= 2 ? TrailRadiusFireTrail2 : 0f)
                + PanicTrailRadius
                + (scorchedEarth ? TrailRadiusScorchedEarth : 0f);
        }
    }

    // Cumulative, and uneven between the ranks — buying II keeps what I gave
    private float PanicTrailRadius
    {
        get
        {
            if (searingPanicLevel >= 2) return TrailRadiusPanic1 + TrailRadiusPanic2;
            if (searingPanicLevel >= 1) return TrailRadiusPanic1;
            return 0f;
        }
    }
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

    // Convenience for every zone-placing site: one call, correct dps, persistence and
    // ignition. Whether the zone lights things is THIS knight's business and is baked
    // in here rather than asked of every caller — sourceEnemyId is the only thing a
    // caller supplies, being the body that dripped it, so that body can walk its own
    // trail without paying for it twice.
    public void PlaceZone(Vector2 position, float radius, float duration,
        int sourceEnemyId = 0)
    {
        FireField.AddZone(position, radius + zoneRadiusBonus, duration, gameObject.tag,
            ZoneDps, scorchedEarth, false, ZonesIgnite, sourceEnemyId);
    }

    /// <summary>
    /// Same as PlaceZone, but tagged as a Fire Trail drop so the Ember quest can
    /// ask for several burning at once without counting craters.
    /// </summary>
    public void PlaceTrailZone(Vector2 position, float radius, float duration,
        int sourceEnemyId = 0)
    {
        FireField.AddZone(position, radius + zoneRadiusBonus, duration, gameObject.tag,
            ZoneDps, scorchedEarth, true, ZonesIgnite, sourceEnemyId);
    }
}

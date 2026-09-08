using UnityEngine;

// The Frigid Order's per-knight stat sheet, mirroring EmberBoost / DawnBoost /
// NinjaBoost. Every Frigid upgrade writes into this and nothing else stores
// Frigid state; the combat systems read it.
//
// THE PILLAR, in three parts (see Docs/Design/frigid-order.md):
//
//   1. Frost has no damage of its own. Every number here is a second, a
//      percentage of speed, or a multiplier on a blow the PLAYER lands. Frigid
//      never ticks. Serpent and Ember already own damage-over-time between them,
//      and a third one would be a re-skin wearing a new colour.
//   2. Only a blow can freeze. Fields, auras and splinters chill; they never
//      stop anything. The ward is a floor of cold, not a lockdown engine - this
//      is the direct analogue of Ember's "only your shots can ignite", and it
//      exists for the same reason.
//   3. One hit does one thing. An arrow that chills cannot also freeze on the
//      same hit; the second touch has to be a second blow. That is what keeps
//      "the first slows, the second stops" true rather than "arrows freeze".
//
// The one exception to (1) is Shatter, and it proves the rule: that is damage
// the player delivered with a shot they aimed, not a timer paying out.
public class FrigidBoost : MonoBehaviour
{
    // ---- tuning knobs, all in one place so the balance pass is one file ----

    /// <summary>How long a body that thawed on its OWN shrugs off cold. A freeze
    /// broken by damage earns no reprieve, which is what lets a frost arrow
    /// shatter a statue and re-chill it in the same hit.</summary>
    public const float ThawReprieveSeconds = 2f;

    /// <summary>Any blow of this size or larger ends a freeze, whether or not the
    /// knight owns Shatter. A statue you can hit for real damage and watch stand
    /// there reads as a bug; chip damage (poison and fire ticks) stays under it
    /// deliberately, so a burning body still burns through the whole hold.</summary>
    public const int FreezeBreakDamage = 5;

    /// <summary>Radius of the cold thrown off by a body Shatter II breaks.</summary>
    public const float SplinterRadius = 1.8f;

    /// <summary>The ward sweeps on a fixed beat rather than every frame - one
    /// OverlapCircle per knight per quarter second, not sixty.</summary>
    public const float WardTickInterval = 0.25f;

    /// <summary>How long the ward's touch lasts once something leaves it. Short,
    /// because standing in the ring is what is being paid for.</summary>
    public const float WardChillSeconds = 0.6f;

    /// <summary>What the ward does to enemy ammunition crossing it at rank II.</summary>
    public const float WardProjectileSpeedMultiplier = 0.5f;

    // ---- owned ranks ----
    private int frostTipLevel;
    private int glacialWardLevel;
    private int deepFreezeLevel;
    private int shatterLevel;
    private int rimebladeLevel;
    private bool permafrost;

    // Equipment adds time on separate fields rather than inflating a rank. Rank
    // also drives chill DEPTH and whether the knight can freeze at all, so a band
    // that "gives you Frost Tip II" to buy two seconds would quietly hand over the
    // slow as well. Same reason Ember keeps its zone dps bonus off its level.
    // Chill is scaled rather than topped up, because the chain's whole shape is in
    // how long the cold lasts - 3s to 6s to 10s. A flat two seconds would be most
    // of rank one and almost nothing by rank three, so the item would quietly be a
    // beginner's trinket. Doubling means it is worth the same at every rank.
    private float chillDurationMultiplier = 1f;
    private float freezeDurationBonus;

    public void MultiplyChillDuration(float factor) { chillDurationMultiplier *= Mathf.Max(1f, factor); }
    public void AddFreezeDurationBonus(float seconds) { freezeDurationBonus += Mathf.Max(0f, seconds); }

    // Setters are monotonic, the house rule: a re-pick or an out-of-order draft
    // can never walk a knight's cold backwards.
    public void SetFrostTip(int level) { frostTipLevel = Mathf.Max(frostTipLevel, level); }
    public void SetGlacialWard(int level) { glacialWardLevel = Mathf.Max(glacialWardLevel, level); }
    public void SetDeepFreeze(int level) { deepFreezeLevel = Mathf.Max(deepFreezeLevel, level); }
    public void SetShatter(int level) { shatterLevel = Mathf.Max(shatterLevel, level); }
    public void SetRimeblade(int level) { rimebladeLevel = Mathf.Max(rimebladeLevel, level); }
    public void SetPermafrost() { permafrost = true; }

    public int FrostTipLevel { get { return frostTipLevel; } }
    public int GlacialWardLevel { get { return glacialWardLevel; } }
    public int DeepFreezeLevel { get { return deepFreezeLevel; } }
    public int ShatterLevel { get { return shatterLevel; } }
    public int RimebladeLevel { get { return rimebladeLevel; } }
    public bool HasPermafrost { get { return permafrost; } }

    // How cold this knight is, full stop. Frost Tip is the chain that deepens it,
    // but the depth carries to EVERYTHING the knight's cold touches - the ward and
    // the blade get colder too. A knight who took only Glacial Ward still chills
    // at rank one rather than at nothing, which is why this floors at 1.
    private int ColdRank { get { return Mathf.Clamp(Mathf.Max(frostTipLevel, 1), 1, 3); } }

    /// <summary>What a chilled body's movement is multiplied by: a third off, then
    /// half, then seventy percent at the top of the chain.
    ///
    /// Deliberately short of stopping something. The Order already HAS a way to
    /// stop a body and it costs a second blow - if a chill alone left a wolf
    /// crawling, Deep Freeze would be buying almost nothing and the whole
    /// two-state reading would collapse into "cold is a slow that gets slower".
    /// The gap between rank III and frozen is what Deep Freeze is paid for.</summary>
    public float ChillSpeedMultiplier
    {
        get
        {
            if (ColdRank >= 3) return 0.30f;
            if (ColdRank == 2) return 0.50f;
            return 0.70f;
        }
    }

    /// <summary>How long a chill lasts before it wears off. Without decay every
    /// body on screen is permanently chilled after one volley and the second
    /// state stops being something the player earns.</summary>
    public float ChillSeconds
    {
        get
        {
            if (ColdRank >= 3) return 10f * chillDurationMultiplier;
            if (ColdRank == 2) return 6f * chillDurationMultiplier;
            return 3f * chillDurationMultiplier;
        }
    }

    /// <summary>Whether this knight's shots carry cold at all. The ward and the
    /// blade chill on their own; arrows need the chain.</summary>
    public bool ArrowsChill { get { return frostTipLevel > 0; } }

    /// <summary>How long a freeze holds, or zero while the knight cannot freeze.
    /// Deep Freeze is the upgrade that turns the Order on: everything before it
    /// is a slow, everything after it is a stop.</summary>
    public float FreezeSeconds
    {
        get
        {
            // Zero stays zero: a knight who cannot freeze is not handed a freeze
            // by an item, however long the item says it lasts.
            if (deepFreezeLevel >= 2) return 6f + freezeDurationBonus;
            if (deepFreezeLevel == 1) return 3f + freezeDurationBonus;
            return 0f;
        }
    }

    public bool CanFreeze { get { return deepFreezeLevel > 0; } }

    /// <summary>What a blow landed on a frozen body is multiplied by. One means
    /// the knight has no Shatter and the hit is just a hit.</summary>
    public float ShatterMultiplier
    {
        get
        {
            if (shatterLevel >= 2) return 3f;
            if (shatterLevel == 1) return 2f;
            return 1f;
        }
    }

    /// <summary>Rank II throws splinters. They CHILL and never freeze - see
    /// pillar 2; this is the Order's only way to reach more than one body at once
    /// and it stays inside the rule.</summary>
    public bool ShatterSplinters { get { return shatterLevel >= 2; } }

    // Tight, and tighter than it first shipped. The ward is a LAST line - the cold
    // a body walks into when it has already got close enough to be a problem. At
    // the old radius it covered so much of the approach that everything arrived
    // pre-chilled and every arrow that landed anywhere near a knight froze, which
    // quietly made Deep Freeze automatic rather than something to aim.
    public float WardRadius
    {
        get
        {
            if (glacialWardLevel >= 2) return 2.4f;
            if (glacialWardLevel == 1) return 1.6f;
            return 0f;
        }
    }

    /// <summary>Rank II slows enemy ammunition crossing the ring. Not damage and
    /// not defence - it is time to get the shield there, which is the only
    /// currency this Order deals in.</summary>
    public bool WardSlowsProjectiles { get { return glacialWardLevel >= 2; } }

    public float RimebladeChance
    {
        get
        {
            if (rimebladeLevel >= 3) return 100f;
            if (rimebladeLevel == 2) return 60f;
            if (rimebladeLevel == 1) return 33f;
            return 0f;
        }
    }

    public float RimebladeRadius { get { return rimebladeLevel >= 2 ? 2.3f : 1.6f; } }

    public bool ShouldReleaseRimeblade()
    {
        float chance = RimebladeChance;
        if (chance <= 0f) return false;
        if (chance >= 100f) return true;
        return Random.Range(0f, 100f) < chance;
    }

    /// <summary>
    /// The one door cold comes through, so every source spends the same rules.
    ///
    /// <paramref name="isBlow"/> is pillar 2 made structural: true only for a hit
    /// the knight landed with an arrow or the blade. Everything else - the ward,
    /// splinters - passes false and can never do more than chill.
    /// </summary>
    /// <returns>True if this touch froze the target.</returns>
    public bool TouchWithCold(EnemyBase enemy, bool isBlow)
    {
        if (enemy == null || enemy.IsDead) return false;

        float freezeFor = (isBlow && CanFreeze) ? FreezeSeconds : 0f;
        return enemy.ApplyCold(ChillSpeedMultiplier, ChillSeconds, freezeFor, permafrost);
    }

    /// <summary>Cold onto anything that is not an enemy - an orb, a rock in the
    /// air. Never freezes; there is nothing there to hold still.</summary>
    public void TouchWithCold(IChillable thing, float speedMultiplier)
    {
        if (thing == null) return;
        thing.ApplyChill(speedMultiplier, ChillSeconds);
    }

    /// <summary>Whether any knight on the field is running Frigid at all. Used by
    /// the cheap early-outs in the per-frame paths so a run with no Frigid picks
    /// pays nothing for the Order existing.</summary>
    public static bool AnyKnightIsFrigid()
    {
        return FindKnightBoost("PlayerLeft") != null || FindKnightBoost("PlayerRight") != null;
    }

    public static FrigidBoost FindKnightBoost(string knightTag)
    {
        GameObject knight = GameObject.FindWithTag(knightTag);
        return knight != null ? knight.GetComponent<FrigidBoost>() : null;
    }

    /// <summary>
    /// Take the cold off everything still standing. Called at the top of a wave,
    /// the way FireField.ClearAll is, and for the same reason: a Permafrost hold
    /// has no deadline of its own, so a statue made in one wave would still be
    /// standing in the next one and the difficulty curve would invert.
    ///
    /// Ice dies with the wave, not with the run.
    /// </summary>
    public static void ClearFieldFrost()
    {
        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null) enemies[i].PurgeFrost();
        }

        // Last, not first: each PurgeFrost above hands its own tally back, so
        // zeroing beforehand would leave the count chasing itself negative. This
        // is the belt to that braces - anything the sweep could not reach (a body
        // destroyed on the same frame) is forgotten here.
        EnemyBase.ResetLiveFrozenCount();
    }
}

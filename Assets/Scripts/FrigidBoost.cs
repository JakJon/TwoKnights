using UnityEngine;

// The Frigid Order's per-knight stat sheet, mirroring EmberBoost / DawnBoost /
// NinjaBoost. Every Frigid upgrade writes into this and nothing else stores
// Frigid state; the combat systems read it.
//
// THE PILLAR, in three parts (see Docs/Design/frigid-order.md):
//
//   1. Frost deals no damage until the capstone, and then it deals ALL of it.
//      Every other number here is a second, a percentage of speed, or a
//      multiplier on a blow the PLAYER lands - so a Frigid knight without Frost
//      Bite is still buying time and nothing else, which is what the whole
//      roster below is priced against. The one exception is Rimeblade, whose
//      burst adds frost damage to the swing that throws it (owner's call,
//      2026-09-25) - still a blow the player landed, never a timer paying out.
//   2. Only a blow can freeze. Fields, auras and splinters chill; they never
//      stop anything. The ward is a floor of cold, not a lockdown engine - this
//      is the direct analogue of Ember's "only your shots can ignite", and it
//      exists for the same reason.
//   3. One hit does one thing. An arrow that chills cannot also freeze on the
//      same hit; the second touch has to be a second blow. That is what keeps
//      "the first slows, the second stops" true rather than "arrows freeze".
//
// Part 1 was rewritten on 2026-09-08 (owner's call) and used to read "frost has
// no damage of its own, Frigid never ticks". That is no longer true and the
// change was deliberate, not a leak: Frost Bite is a DAMAGE-OVER-TIME capstone,
// and the Order was short of any way to convert all that held time into dead
// bodies. What the old rule was protecting - that Frigid is not a blue re-skin of
// Serpent - is now carried by the shape instead of by the absence: poison is
// bought per-arrow and rides one target, while Frost Bite is bought ONCE and then
// bills every chilled body on the field at a rate the player raises by freezing.
//
// The ceiling, written down so it is known rather than so it gets tuned away: a
// freeze lasts whatever chill was left plus 3s per Deep Freeze rank. Since
// 2026-10-05 Deep Freeze carries the chill as well (see ColdRank), so the longest
// drafted freeze is Deep Freeze III's 13s chill (3 + 10) + its 9s = 22s, at 3 a
// second = 66, and Frost Bite ticks never wear the ice down. That kills a bat, a
// rat, a grey wolf and a black wolf (60) outright. Hoarfrost Band doubles the
// chill, which makes it 35s and 105 - an ogre dies inside one freeze.
// Bosses are held half as long (BossFreezeMultiplier).
//
// A knight who got here spent most of a run on one idea and the board going quiet is
// what they bought. Don't add guardrails to it.
public class FrigidBoost : MonoBehaviour
{
    // ---- tuning knobs, all in one place so the balance pass is one file ----

    /// <summary>How much damage ice takes before it breaks, for a knight with no
    /// Deep Freeze. Damage adds up across hits, and counts whether or not the
    /// knight owns Shatter. Poison and fire ticks never count.</summary>
    public const int FreezeBreakDamage = 5;

    /// <summary>Extra damage the ice takes before breaking, per Deep Freeze rank
    /// (owner's call, 2026-09-11): 5, then 15, 25, 35.</summary>
    public const int FreezeBreakDamagePerDeepFreeze = 10;

    /// <summary>Seconds each Deep Freeze rank adds on top of the chill the body
    /// had left when it froze (owner's call, 2026-09-11): +3, +6, +9.</summary>
    public const float FreezeSecondsPerDeepFreeze = 3f;

    /// <summary>What a BOSS's freeze duration is multiplied by (owner's call,
    /// 2026-09-08). Frigid's hold is the one effect in the game that removes a
    /// fight rather than shortening it, and a full-rank freeze is up to nineteen
    /// seconds of a duel simply not happening. Halved, the Order stays strong against a
    /// crowd — which is what it is for — without switching off the encounters the
    /// run is built around. Applied in EnemyBase.Freeze so every source of cold
    /// there will ever be is covered by it.</summary>
    public const float BossFreezeMultiplier = 0.5f;

    // The three sizes a burst of cold comes in. Rimeblade walks all three, and a
    // Shatter blast borrows the top two (owner, 2026-10-05: "same effect from
    // rime blade"), so "large" on one card is the same ring as "large" on the other.
    public const float FrostBlastSmall = 1.6f;
    public const float FrostBlastLarge = 2.3f;
    public const float FrostBlastExtraLarge = 3f;

    /// <summary>The ward sweeps on a fixed beat rather than every frame - one
    /// OverlapCircle per knight per quarter second, not sixty.</summary>
    public const float WardTickInterval = 0.25f;

    /// <summary>How long the ward's touch lasts once something leaves it. Short,
    /// because standing in the ring is what is being paid for.</summary>
    public const float WardChillSeconds = 0.6f;

    /// <summary>What the ward does to enemy ammunition crossing it, at every rank.</summary>
    public const float WardProjectileSpeedMultiplier = 0.5f;

    /// <summary>Frost Bite: what a CHILLED body loses per second once the capstone
    /// is owned. Level with a poisoned arrow's 2/s, and that is the point of
    /// comparison to hold on to - the difference is that poison is bought per shot
    /// and rides one target, while this is bought once and bills the whole field.</summary>
    public const float FrostBiteChilledDps = 2f;

    /// <summary>What a FROZEN body loses per second. Higher than the chill, so the
    /// Order's own cycle - slow it, then stop it - is also the damage upgrade, and
    /// the player is paid for landing the second blow rather than for standing near
    /// things.</summary>
    public const float FrostBiteFrozenDps = 3f;

    /// <summary>Cold bills once a second, so the number that pops IS the dps. Same
    /// reasoning as Ember's FireFlushInterval: flushing on every accumulator
    /// crossing shows a stream of 1s that reads as 1 dps whatever the real rate.</summary>
    public const float FrostBiteFlushInterval = 1f;

    // ---- owned ranks ----
    private int frostTipLevel;
    private int glacialWardLevel;
    private int deepFreezeLevel;
    private int shatterLevel;
    private int rimebladeLevel;
    private bool frostBite;

    // Equipment adds time on separate fields rather than inflating a rank. Rank
    // also drives chill DEPTH and whether the knight can freeze at all, so a band
    // that "gives you a rank of cold" to buy two seconds would quietly hand over the
    // slow as well. Same reason Ember keeps its zone dps bonus off its level.
    // Chill is scaled rather than topped up, because the chain's whole shape is in
    // how long the cold lasts - 3s to 6s to 10s. A flat two seconds would be most
    // of rank one and almost nothing by rank three, so the item would quietly be a
    // beginner's trinket. Doubling means it is worth the same at every rank.
    private float chillDurationMultiplier = 1f;
    private float freezeDurationBonus;

    public void MultiplyChillDuration(float factor) { chillDurationMultiplier *= Mathf.Max(1f, factor); }
    public void AddFreezeDurationBonus(float seconds) { freezeDurationBonus += Mathf.Max(0f, seconds); }

    // Frost Tip ranks from equipment (Winter's Tooth) sit on top of the drafted
    // rank instead of going through SetFrostTip, whose Max would swallow the
    // item the moment the draft caught up with it. Items always stack, which is
    // what makes rank IV reachable — see ColdRank.
    private int frostTipBonus;
    public void AddFrostTipFromEquipment(int ranks) { frostTipBonus += Mathf.Max(0, ranks); }
    private int TotalFrostTip { get { return frostTipLevel + frostTipBonus; } }

    // Setters are monotonic, the house rule: a re-pick or an out-of-order draft
    // can never walk a knight's cold backwards.
    public void SetFrostTip(int level) { frostTipLevel = Mathf.Max(frostTipLevel, level); }
    public void SetGlacialWard(int level) { glacialWardLevel = Mathf.Max(glacialWardLevel, level); }
    public void SetDeepFreeze(int level) { deepFreezeLevel = Mathf.Max(deepFreezeLevel, level); }
    public void SetShatter(int level) { shatterLevel = Mathf.Max(shatterLevel, level); }
    public void SetRimeblade(int level) { rimebladeLevel = Mathf.Max(rimebladeLevel, level); }
    public void SetFrostBite() { frostBite = true; }

    public int FrostTipLevel { get { return TotalFrostTip; } }
    public int GlacialWardLevel { get { return glacialWardLevel; } }
    public int DeepFreezeLevel { get { return deepFreezeLevel; } }
    public int ShatterLevel { get { return shatterLevel; } }
    public int RimebladeLevel { get { return rimebladeLevel; } }

    /// <summary>Frost Bite, the capstone: cold stops being purely time and starts
    /// killing. Deliberately NOT gated on Deep Freeze - a knight who only ever
    /// chills still gets the chilled rate, which is what keeps the Glacial Ward
    /// build a real way into the Order rather than a dead end.</summary>
    public bool HasFrostBite { get { return frostBite; } }

    /// <summary>What this knight's cold bills the body it is sitting on, per second.
    /// Zero for everyone who has not taken the capstone, which is what keeps the
    /// per-frame path free for every other build.</summary>
    public float FrostBiteDps(bool frozen)
    {
        if (!frostBite) return 0f;
        return frozen ? FrostBiteFrozenDps : FrostBiteChilledDps;
    }

    // How cold this knight is, full stop. The depth carries to EVERYTHING the
    // knight's cold touches - the ward and the blade get colder too. A knight who
    // took only Glacial Ward still chills at rank one rather than at nothing,
    // which is why this floors at 1.
    //
    // DEEP FREEZE IS THE CHAIN THAT DEEPENS IT (owner, 2026-10-05). Frost Tip used
    // to: it had three ranks and they were the slow and the chill time. Frost Tip
    // is now one card, the door into the Order - arrows chill at rank one and a
    // second hit freezes, exactly as its rank I always did - and what its ranks II
    // and III bought moved onto Deep Freeze II and III. Deep Freeze I sits at the
    // same depth as the door: its card says -30%, which is rank one.
    //
    // The draft alone tops out at III. Rank IV exists only for equipment stacked on
    // a full chain (Winter's Tooth + Deep Freeze III), so that the last pick is
    // never a dead one. Each Frost Tip rank an item grants is still one rank
    // colder, as it was when the ranks were Frost Tip's own.
    private int ColdRank
    {
        get
        {
            int deep = Mathf.Max(Mathf.Min(deepFreezeLevel, 3) - 1, 0);
            return Mathf.Clamp(Mathf.Max(TotalFrostTip, 1) + deep, 1, 4);
        }
    }

    /// <summary>What a chilled body's movement is multiplied by: a third off, then
    /// half, then seventy percent at the top of the chain.
    ///
    /// Deliberately short of stopping something. The Order already HAS a way to
    /// stop a body and it costs a second blow - if a chill alone left a wolf
    /// crawling, the freeze would be buying almost nothing and the whole
    /// two-state reading would collapse into "cold is a slow that gets slower".
    ///
    /// Rank IV (owner's call, 2026-09-10) is the one exception: 85% off, the same
    /// ten seconds as III. It is only reachable by stacking equipment on the full
    /// chain, so it is a reward for building into cold, not the Order's baseline.</summary>
    public float ChillSpeedMultiplier
    {
        get
        {
            if (ColdRank >= 4) return 0.15f;
            if (ColdRank == 3) return 0.30f;
            if (ColdRank == 2) return 0.50f;
            return 0.70f;
        }
    }

    /// <summary>How long a chill lasts before it wears off. Without decay every
    /// body on screen is permanently chilled after one volley and the second
    /// state stops being something the player earns.
    ///
    /// Three seconds at the door, and Deep Freeze ADDS to it: +3s, +6s, +10s by
    /// rank, which is what its cards say (owner, 2026-10-05). Those are the three
    /// figures Frost Tip's ranks used to set outright, now stacked on the base
    /// rather than replacing it, so a full chain chills for 13s where it was 10.
    /// A Frost Tip rank beyond the first is worth three seconds, as rank I to II
    /// was. Only equipment can supply one now: Winter's Tooth alone IS the first
    /// rank and chills for the base three, and worn with Frost Tip it is a second.</summary>
    public float ChillSeconds
    {
        get
        {
            int extraRanks = Mathf.Max(TotalFrostTip - 1, 0);
            float seconds = BaseChillSeconds + DeepFreezeChillBonus + extraRanks * ChillSecondsPerItemRank;
            return seconds * chillDurationMultiplier;
        }
    }

    public const float BaseChillSeconds = 3f;
    public const float ChillSecondsPerItemRank = 3f;

    /// <summary>Seconds of chill a Deep Freeze rank adds: 3, 6, 10.</summary>
    public float DeepFreezeChillBonus
    {
        get
        {
            if (deepFreezeLevel >= 3) return 10f;
            if (deepFreezeLevel == 2) return 6f;
            if (deepFreezeLevel == 1) return 3f;
            return 0f;
        }
    }

    /// <summary>Whether this knight's shots carry cold at all. The ward and the
    /// blade chill on their own; arrows need the chain.</summary>
    public bool ArrowsChill { get { return TotalFrostTip > 0; } }

    /// <summary>Seconds this knight's freezes hold ON TOP of the chill the body had
    /// left when it froze: +3 per Deep Freeze rank, plus Heart of Ice. Zero while
    /// the knight cannot freeze - an item never hands out a freeze on its own.</summary>
    public float FreezeBonusSeconds
    {
        get
        {
            if (!CanFreeze) return 0f;
            return Mathf.Min(deepFreezeLevel, 3) * FreezeSecondsPerDeepFreeze + freezeDurationBonus;
        }
    }

    /// <summary>How much damage this knight's ice takes, in total, before it breaks:
    /// 5, then +10 per Deep Freeze rank.</summary>
    public int IceBreakDamage
    {
        get { return FreezeBreakDamage + Mathf.Min(deepFreezeLevel, 3) * FreezeBreakDamagePerDeepFreeze; }
    }

    /// <summary>Any Frost Tip rank (drafted or from Winter's Tooth) lets this
    /// knight's blows freeze a chilled body. Deep Freeze is checked too so the
    /// rule never depends on the draft order.</summary>
    public bool CanFreeze { get { return TotalFrostTip > 0 || deepFreezeLevel > 0; } }

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

    /// <summary>How far the cold reaches when this knight breaks a frozen body:
    /// Rimeblade's large ring at rank I, its extra large at rank II. Zero without
    /// Shatter.
    ///
    /// Both ranks blast since 2026-10-05 (owner's call); before that only rank II
    /// threw anything, and only off an arrow. The blast CHILLS and never freezes,
    /// and it deals no damage - Shatter's damage is the multiplier on the blow and
    /// nothing else (owner). It is still the Order's only way to reach more than
    /// one body at once, and it stays inside pillar 2 by doing it with cold.</summary>
    public float ShatterBlastRadius
    {
        get
        {
            if (shatterLevel >= 2) return FrostBlastExtraLarge;
            if (shatterLevel == 1) return FrostBlastLarge;
            return 0f;
        }
    }

    /// <summary>
    /// The frost blast off a body this knight just shattered: Rimeblade's burst and
    /// ring, and a chill on everything else inside it. Never onto the thing that
    /// was shattered - the blow that broke its ice deliberately leaves it free
    /// rather than chilled, and cold landing back on it would undo that and let
    /// one hit do the Order's whole cycle alone.
    /// </summary>
    public void ReleaseShatterBlast(Vector2 center, EnemyBase shattered)
    {
        float radius = ShatterBlastRadius;
        if (radius <= 0f) return;

        FrostFx.Burst(center, radius);
        FrostFx.BurstRing(center, radius);

        Collider2D[] caught = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < caught.Length; i++)
        {
            EnemyBase other = caught[i] != null ? caught[i].GetComponent<EnemyBase>() : null;
            if (other == null || other == shattered || other.IsDead) continue;
            TouchWithCold(other, false);
        }
    }

    // Tight, and tighter than it first shipped. The ward is a LAST line - the cold
    // a body walks into when it has already got close enough to be a problem. At
    // the old radius it covered so much of the approach that everything arrived
    // pre-chilled and every arrow that landed anywhere near a knight froze, which
    // quietly made freezing automatic rather than something to aim.
    public float WardRadius
    {
        get
        {
            // Rank III is the one tier that deliberately breaks the "last line"
            // framing above: it buys reach out past the shield itself, which is
            // why it costs an Epic slot rather than sitting on the common ladder.
            if (glacialWardLevel >= 3) return 3.9f;
            if (glacialWardLevel == 2) return 2.4f;
            if (glacialWardLevel == 1) return 1.6f;
            return 0f;
        }
    }

    /// <summary>The ward slows enemy ammunition crossing the ring, from rank I
    /// (owner, 2026-10-05; it used to start at rank II). Not damage and not
    /// defence - it is time to get the shield there, which is the only currency
    /// this Order deals in.</summary>
    public bool WardSlowsProjectiles { get { return glacialWardLevel >= 1; } }

    /// <summary>Rimeblade's frost damage per rank, dealt ON TOP of the sword's own
    /// hit to everything the burst catches: 5, then 10, then 15.</summary>
    public const int RimebladeFrostDamagePerRank = 5;

    // Every rank fires on every swing (owner's call, 2026-09-25 - it used to be
    // 33% / 60% / 100%). What a rank buys now is reach and frost damage.
    public bool ShouldReleaseRimeblade() { return rimebladeLevel > 0; }

    public float RimebladeRadius
    {
        get
        {
            if (rimebladeLevel >= 3) return FrostBlastExtraLarge;
            if (rimebladeLevel == 2) return FrostBlastLarge;
            return FrostBlastSmall;
        }
    }

    public int RimebladeFrostDamage
    {
        get { return Mathf.Clamp(rimebladeLevel, 0, 3) * RimebladeFrostDamagePerRank; }
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

        return enemy.ApplyCold(ChillSpeedMultiplier, ChillSeconds, isBlow && CanFreeze,
                               FreezeBonusSeconds, IceBreakDamage, gameObject.tag, frostBite);
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
    /// the way FireField.ClearAll is, and for the same reason: a rank three hold
    /// runs fifteen seconds, so a statue made at the end of one wave would still
    /// be standing well into the next one and the difficulty curve would invert.
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

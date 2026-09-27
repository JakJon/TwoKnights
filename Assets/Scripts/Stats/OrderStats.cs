/// <summary>
/// Stat keys for the Order quest lines, kept in one place so a quest and the
/// detector that feeds it cannot drift apart. Same rule as <see cref="Feats"/>.
///
/// Most of these are a shape the game did not previously measure. The old Order
/// quests asked "how many, ever" — a running total that a long enough session
/// answers on its own. These ask "how many at once", "how many in one wave", or
/// "how far without failing", which is the difference between a quest you finish
/// by playing and a quest you finish by trying.
///
/// Three families, and the suffix says which:
///   .max          a high-water mark. Written with PlayerStats.Raise, never
///                 Increment — the record only moves up, and a bad wave must not
///                 undo a good one.
///   (bare total)  cumulative across every run, written with Increment.
///   .streak.max   the longest unbroken run of something; the live streak is held
///                 in memory by the detector and only its record is persisted.
///
/// A .max key needs a per-wave or per-run accumulator somewhere that resets; the
/// key here is only where the record lands.
/// </summary>
public static class OrderStats
{
    // ---- Serpent ----

    /// <summary>Most enemies carrying poison at the same instant.</summary>
    public const string PoisonSimultaneousMax = "poison.simultaneous.max";

    /// <summary>Largest single poison tick ever dealt, in damage.</summary>
    public const string PoisonTickMax = "poison.tick.max";

    /// <summary>Kills credited to the Acid Dagger's follow-up jab specifically.</summary>
    public const string AcidDaggerKills = "kills.acid_dagger";

    // ---- Shadow ----

    /// <summary>Most shadow arrows landed within one wave. Boss waves excluded — a boss
    /// stands still and soaks, which would make the record about patience, not aim.</summary>
    public const string ShadowArrowWaveMax = "shadowarrow.wave.max";

    /// <summary>Times Thousand Cuts has fired. Cumulative.</summary>
    public const string ThousandCutsActivations = "thousand_cuts.activations";

    /// <summary>Most shurikens landed within one wave.</summary>
    public const string ShurikenWaveMax = "shuriken.wave.max";

    /// <summary>Most kills scored by phantom sword echoes within one wave.</summary>
    public const string PhantomKillsWaveMax = "phantom.kills.wave.max";

    // ---- Ember ----

    /// <summary>Greatest share of the visible arena on fire at once, as a whole percent.</summary>
    public const string FireCoveragePercentMax = "fire.coverage.percent.max";

    /// <summary>Most damage burned into one boss without the burn ever lapsing. The run
    /// resets the moment the target stops being alight, which is the whole test.</summary>
    public const string BossBurnUninterruptedMax = "ember.boss_burn.max";

    /// <summary>Most fireballs landed within one run.</summary>
    public const string FireballRunMax = "fireball.run.max";

    /// <summary>Longest a single fire field has burned, in whole seconds.</summary>
    public const string FireFieldSecondsMax = "firefield.seconds.max";

    // ---- Frigid ----

    /// <summary>Total seconds enemies have spent frozen, across every run.</summary>
    public const string FrozenSecondsTotal = "frigid.frozen.seconds";

    /// <summary>Most shatter damage dealt within one wave.</summary>
    public const string ShatterDamageWaveMax = "frigid.shatter.wave.max";

    /// <summary>Most enemies frozen inside a Glacial Ward within one run.</summary>
    public const string WardFrozenRunMax = "frigid.ward_frozen.run.max";

    /// <summary>Total damage dealt to targets while they were frozen.</summary>
    public const string FrozenTargetDamage = "frigid.frozen_damage";

    // ---- Guardian ----

    /// <summary>Most damage dealt by reflected projectiles within one wave.</summary>
    public const string ReflectDamageWaveMax = "guardian.reflect_damage.wave.max";

    /// <summary>Bodies thrown off the guard by Bulwark. Cumulative.
    /// This counter already existed — EnemyBase writes it on every shove — so the
    /// quest reads that rather than starting a second tally of the same event.</summary>
    public const string BulwarkShoves = "guardian.shoved";

    /// <summary>Longest run of guided shots that all connected. One knight's streak —
    /// the other missing is explicitly allowed, so this is tracked per knight and only
    /// the better of the two is ever recorded.</summary>
    public const string GuidedNoMissStreakMax = "guardian.guided.streak.max";

    /// <summary>Most kills made by the sword at long reach within one wave.</summary>
    public const string LongRangeSwordKillsWaveMax = "sword.longrange.wave.max";

    // ---- Dawn ----

    /// <summary>Most health recovered within one wave, across both knights.</summary>
    public const string HealedInWaveMax = "dawn.healed.wave.max";

    /// <summary>Longest run of consecutive waves ended with BOTH knights at full health.
    /// Taking damage mid-wave is fine; the wave has to END clean.</summary>
    public const string FlawlessWaveStreakMax = "dawn.flawless.streak.max";

    /// <summary>Health restored by Lifebloom specifically. Opens the Dawn initiation for a
    /// knight who went down the mending branch rather than the shared-light one.</summary>
    public const string LifebloomHealing = "dawn.lifebloom_healing";

    /// <summary>Times Last Light has saved the other knight. Cumulative.
    /// Also pre-existing — DawnBoost.TrySpendLastLight writes it.</summary>
    public const string LastLightActivations = "dawn.last_light";

    // ---- Combination upgrades ----

    /// <summary>
    /// Set when ONE knight first qualifies for a given combination upgrade on both of
    /// its Orders at once — that is, when the upgrade would be draftable for them but
    /// for the quest gate hiding it.
    ///
    /// Per upgrade rather than per pair of Orders, because each combination upgrade
    /// states its own requirement in its own asset and reading that is what keeps the
    /// quest and the card in step.
    /// </summary>
    public static string ComboReady(string upgradeSlug)
    {
        return "combo.ready." + upgradeSlug;
    }
}

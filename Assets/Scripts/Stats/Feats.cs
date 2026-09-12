using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-off things a player DID, as opposed to things they accumulated.
///
/// Kill counters answer "how much"; feats answer "can you". They are the
/// objectives worth writing a quest around, and every one of them needs a
/// deliberate detector somewhere in the combat code — none falls out of an
/// existing counter. Keys live here so the detector and the quest that reads it
/// cannot drift apart.
/// </summary>
public static class Feats
{
    /// <summary>Rat King put down with no equipment carried and no special fired.</summary>
    public const string RatKingBare = "feats.ratking_bare";
    /// <summary>Crimson Twins put down under the same restriction.</summary>
    public const string TwinsBare = "feats.twins_bare";

    /// <summary>Four enemies poisoned by one cloud.</summary>
    public const string PoisonCloudFour = "feats.poison_cloud_4";
    /// <summary>Four fire trail zones burning at once.</summary>
    public const string FireTrailsFour = "feats.fire_trails_4";
    /// <summary>Four shurikens from one volley all landing.</summary>
    public const string ShurikenVolleyFour = "feats.shuriken_volley_4";
    /// <summary>A swing plus both its phantom echoes all connecting.</summary>
    public const string PhantomFullThree = "feats.phantom_full_3";
    /// <summary>Four enemies held in ice at the same moment.</summary>
    public const string FrozenFour = "feats.frozen_4";
    /// <summary>A wave that drove a knight low and still ended with both at full.</summary>
    public const string DawnPairPulledBack = "feats.dawn_pair_pulled_back";
    /// <summary>One reflected powder rock catching three bodies at once.</summary>
    public const string ReflectThree = "feats.reflect_3";
    /// <summary>One knight, in one run, two Guardian picks deep and using them.</summary>
    public const string GuardianAwoken = "feats.guardian_awoken";

    /// <summary>Running total of shadow arrows that connected.</summary>
    public const string ShadowArrowHits = "hits.shadowarrow";

    public static void Record(string key)
    {
        PlayerStats.Increment(key);
    }
}

/// <summary>
/// Whether the current run is being played "bare" — neither knight carrying any
/// equipment, and neither carrying a special.
///
/// This is a LOADOUT restriction, settled once at run start, not a promise to
/// avoid pressing a button. That is deliberate: the player knows going in whether
/// they are attempting the challenge, rather than discovering halfway through
/// that a reflex voided it. It became expressible once the equipment screen let
/// the stock specials be unequipped.
/// </summary>
public static class RunPurity
{
    /// <summary>Both knights started this run carrying nothing at all.</summary>
    public static bool Bare { get; private set; }

    /// <summary>
    /// Called once from Spawner.Start, after loadouts are applied. Static state
    /// survives scene reloads, so this must run every run — the same discipline
    /// UpgradeManager.ResetRunState needs.
    /// </summary>
    public static void BeginRun(GameObject leftKnight, GameObject rightKnight)
    {
        Bare = IsBare(leftKnight, Loadout.LeftKnight)
            && IsBare(rightKnight, Loadout.RightKnight);
    }

    private static bool IsBare(GameObject knight, string knightId)
    {
        // A knight who never opened the equipment screen still has their stock
        // special, so this has to be an explicit "none", not merely unset
        if (!Loadout.HasNoSpecial(knightId)) return false;

        // Reads the applied boost rather than the save, so a knight who somehow
        // had an item applied by another route still counts as carrying something
        if (knight == null) return true;
        var boost = knight.GetComponent<EquipmentBoost>();
        return boost == null || !boost.CarriesAnything;
    }
}

/// <summary>
/// The Guardian Order's door, and the one reveal condition in the game that asks
/// about a RUN rather than a lifetime.
///
/// Every other quest gate reads an accumulated stat. This one wants "you took two
/// Guardian upgrades on one knight and then actually used them, in the same run",
/// which no counter can express - a player who took a Guardian pick twenty runs ago
/// and reflected a rock today has not done the thing.
///
/// Both halves are tracked PER KNIGHT TAG, so two knights cannot each contribute
/// half of it. Static, and therefore reset from Spawner.BeginRun every run, the same
/// discipline RunPurity above needs.
/// </summary>
public static class GuardianAwakening
{
    private static readonly Dictionary<string, int> Picks = new Dictionary<string, int>();
    private static readonly HashSet<string> Used = new HashSet<string>();
    private static bool _recorded;

    /// <summary>How many Guardian picks one knight needs before using them counts.</summary>
    private const int PicksNeeded = 2;

    /// <summary>Called once from Spawner.Start. See RunPurity.BeginRun.</summary>
    public static void BeginRun()
    {
        Picks.Clear();
        Used.Clear();
        _recorded = false;
    }

    /// <summary>A Guardian upgrade landed on this knight. Called from UpgradeManager,
    /// which is the one place that knows both the knight and the Order.</summary>
    public static void NoteUpgrade(string knightTag)
    {
        if (string.IsNullOrEmpty(knightTag)) return;
        Picks.TryGetValue(knightTag, out int count);
        Picks[knightTag] = count + 1;
        Check(knightTag);
    }

    /// <summary>This knight steered a shot or turned a rock around. Called from
    /// GuidedShot and ProjectileSettings - the two things the Order actually does
    /// that a player can see happen.</summary>
    public static void NoteUse(string knightTag)
    {
        if (string.IsNullOrEmpty(knightTag)) return;
        Used.Add(knightTag);
        Check(knightTag);
    }

    private static void Check(string knightTag)
    {
        if (_recorded) return;
        if (!Used.Contains(knightTag)) return;
        Picks.TryGetValue(knightTag, out int count);
        if (count < PicksNeeded) return;

        _recorded = true;
        Feats.Record(Feats.GuardianAwoken);
    }
}

/// <summary>
/// One thrown fan. The shurikens of a volley share an instance, so counting
/// "four from the same volley landed" needs no registry and no cleanup — the
/// object dies with its projectiles.
/// </summary>
public class ShurikenVolley
{
    private int _hits;
    private bool _recorded;

    public void NoteHit()
    {
        _hits++;
        if (_hits >= 4 && !_recorded)
        {
            _recorded = true;
            Feats.Record(Feats.ShurikenVolleyFour);
        }
    }
}

/// <summary>
/// Watches one wave for the Dawn Order's signature moment: a knight driven low,
/// and both of them standing at full when the wave ends.
///
/// It is a feat rather than a counter because it is a "can you", not a "how
/// much" - surviving is common, pulling the pair back from the edge inside the
/// same wave is a thing you either managed or did not. It also cannot fall out
/// of any existing tally: nothing else in the game knows what the pair looked
/// like at the start of a wave and again at the end of it.
///
/// Wave-scoped static state, the same discipline RunPurity uses. Spawner opens
/// and closes the window; PlayerHealth reports the low-water mark as it happens,
/// because a knight can dip and be healed back inside a single second and no
/// end-of-wave reading would ever see it.
/// </summary>
public static class DawnVigil
{
    /// <summary>Fraction of max health that counts as "driven low".</summary>
    private const float LowWaterFraction = 0.25f;

    private static bool _someoneWentLow;

    /// <summary>Called from Spawner as a wave begins.</summary>
    public static void BeginWave()
    {
        _someoneWentLow = false;
    }

    /// <summary>Called from PlayerHealth whenever a knight's health changes downward.</summary>
    public static void NoteHealth(int current, int max)
    {
        if (max <= 0 || _someoneWentLow) return;
        if (current > 0 && current <= max * LowWaterFraction) _someoneWentLow = true;
    }

    /// <summary>Called from Spawner once the wave's last enemy is down.</summary>
    public static void EndWave(PlayerHealth left, PlayerHealth right)
    {
        if (!_someoneWentLow) return;
        _someoneWentLow = false;

        // A dead knight ends the run, so "both at full" can only be read off two
        // living ones. Missing either is not a pass.
        if (left == null || right == null) return;
        if (left.CurrentHealth < left.MaxHealth) return;
        if (right.CurrentHealth < right.MaxHealth) return;

        Feats.Record(Feats.DawnPairPulledBack);
    }
}

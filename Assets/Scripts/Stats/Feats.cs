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

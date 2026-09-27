using System.Collections.Generic;

/// <summary>
/// What the camp menu is allowed to show, and what currently has a notification dot.
///
/// One place for it so the reveal rule and the dot rule cannot disagree — the shop's
/// dot is meaningless if the button isn't there yet, and both are driven by the same
/// save state. Mirrors <see cref="QuestProgress.HasUnseen"/> / MarkSeen, which is the
/// pattern the quests dot already uses.
/// </summary>
public static class CampNotices
{
    /// <summary>
    /// Lifetime crystals the player must have earned before the shop opens. Two,
    /// not one: everything on the shelves costs more than a single crystal, so a
    /// shop opened at one is a price list the player still cannot shop from.
    /// </summary>
    public const int ShopCrystalThreshold = 2;

    /// <summary>
    /// The shop reads as "???" until the player has earned
    /// <see cref="ShopCrystalThreshold"/> crystals — a price list is noise to
    /// someone with no way to pay.
    /// </summary>
    public static bool ShopUnlocked
    {
        get
        {
            var data = SaveManager.Data;
            if (data == null) return false;
            // The lifetime total never falls, so it is the latch: spending back
            // down to nothing cannot close the shop again. Anyone already holding
            // that many, or owning equipment at all, has plainly earned them on a
            // save written before the total was tracked.
            return data.totalCrystalsEarned >= ShopCrystalThreshold
                   || data.crystals >= ShopCrystalThreshold
                   || (data.ownedEquipment != null && data.ownedEquipment.Count > 0);
        }
    }

    /// <summary>Lit the first time the shop becomes available, until it is opened.</summary>
    public static bool ShopHasNotice => ShopUnlocked && !SaveManager.Data.shopSeen;

    public static void MarkShopSeen()
    {
        if (SaveManager.Data.shopSeen) return;
        SaveManager.Data.shopSeen = true;
        SaveManager.Save();
    }

    /// <summary>
    /// Lit while the player owns anything they have not opened the Equipment screen
    /// to look at. Covers both shop purchases and quest grants, because it asks about
    /// ownership rather than listening for an acquisition event.
    /// </summary>
    public static bool EquipmentHasNotice
    {
        get
        {
            var owned = SaveManager.Data.ownedEquipment;
            if (owned == null || owned.Count == 0) return false;
            var seen = SaveManager.Data.seenEquipment;
            for (int i = 0; i < owned.Count; i++)
            {
                if (seen == null || !seen.Contains(owned[i])) return true;
            }
            return false;
        }
    }

    /// <summary>Called when the player actually opens the Equipment screen.</summary>
    public static void MarkEquipmentSeen()
    {
        var owned = SaveManager.Data.ownedEquipment;
        if (owned == null || owned.Count == 0) return;

        var seen = SaveManager.Data.seenEquipment
                   ?? (SaveManager.Data.seenEquipment = new List<string>());
        bool changed = false;
        for (int i = 0; i < owned.Count; i++)
        {
            if (seen.Contains(owned[i])) continue;
            seen.Add(owned[i]);
            changed = true;
        }
        if (changed) SaveManager.Save();
    }
}

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
    /// The shop stays hidden until the first crystal is earned — a price list is
    /// noise to someone with no way to pay.
    /// </summary>
    public static bool ShopUnlocked
    {
        get
        {
            var data = SaveManager.Data;
            if (data == null) return false;
            // The latch is new; saves written before it exists have it false even
            // for players who are well past their first crystal. Anyone holding
            // crystals or owning equipment has plainly earned some, so treat that
            // as unlocked rather than yanking the shop back from them.
            return data.everHadCrystals
                   || data.crystals > 0
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

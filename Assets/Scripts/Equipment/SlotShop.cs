using System;

/// <summary>
/// The two things the camp shop sells that are not objects: another equipment
/// slot, and another special slot. Both apply to BOTH knights, the same way the
/// quest-granted ones do.
///
/// Slots are sold rather than only granted because the quest that hands each one
/// out sits behind a boss (the Crimson Twins for equipment, The Gold Cart for
/// specials), and a player who is stuck on that fight had no way to spend
/// crystals on the one thing that would help them get past it. This is the
/// crystal sink for a full shop: once every item on the list is owned, the
/// balance had nowhere to go.
///
/// ONCE EACH, deliberately. A repeatable slot would turn the loadout screen into
/// a list rather than a choice, and the whole point of a slot is that filling it
/// costs you something you also wanted. Bought and quest-granted slots stack, so
/// a player who does both ends the game carrying three.
///
/// Lives beside Loadout rather than inside it: Loadout is about what is owned and
/// what is carried and knows nothing about money. This is the one place that
/// knows a slot has a price.
/// </summary>
public static class SlotShop
{
    // Priced against the item list rather than against each other: the dearest
    // equipment is a handful of crystals, so a slot has to cost more than any
    // single thing you would put in it or buying it first is never wrong. The
    // special slot is dearer again because two specials fire on ONE bar - it is
    // the only purchase in the shop that doubles something.
    public const int EquipmentSlotPrice = 8;
    public const int SpecialSlotPrice = 12;

    public static bool EquipmentSlotBought => SaveManager.Data.boughtEquipmentSlot;
    public static bool SpecialSlotBought => SaveManager.Data.boughtSpecialSlot;

    /// <summary>
    /// Buys the equipment slot. Refuses - changing nothing - when it is already
    /// bought or the balance is short, so callers can use this as the whole gate.
    /// </summary>
    public static bool TryBuyEquipmentSlot()
    {
        if (EquipmentSlotBought) return false;
        if (!CrystalBank.TrySpend(EquipmentSlotPrice)) return false;

        SaveManager.Data.boughtEquipmentSlot = true;
        // ONE MORE, never "set it to two" - a player who already earned the slot
        // from the Crimson Twins has to get something for their crystals. Same
        // rule QuestProgress follows when it pays the quest out.
        SaveManager.Data.equipmentSlots = Math.Max(1, SaveManager.Data.equipmentSlots) + 1;
        SaveManager.Save();
        return true;
    }

    /// <summary>Buys the special slot. Same refusal rules as the equipment slot.</summary>
    public static bool TryBuySpecialSlot()
    {
        if (SpecialSlotBought) return false;
        if (!CrystalBank.TrySpend(SpecialSlotPrice)) return false;

        SaveManager.Data.boughtSpecialSlot = true;
        SaveManager.Data.specialSlots = Math.Max(1, SaveManager.Data.specialSlots) + 1;
        SaveManager.Save();
        return true;
    }
}

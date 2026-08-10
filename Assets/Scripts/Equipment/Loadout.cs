using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each knight owns and carries. Static and save-backed: the camp edits it,
/// the run reads it once at Spawner.Start.
///
/// Ownership is separate from equipping — buying or being granted an item never
/// auto-equips it. Equipping is EXCLUSIVE across knights: you own one Gnawed
/// Crown, so putting it on the right knight takes it off the left. That is what
/// makes a single equipment slot a real choice rather than a formality.
/// </summary>
public static class Loadout
{
    public const string LeftKnight = "left";
    public const string RightKnight = "right";

    public static event Action OnLoadoutChanged;

    /// <summary>Equipment slots per knight. The Crimson Twins quest raises this to 2.</summary>
    public static int SlotCount => Mathf.Max(1, SaveManager.Data.equipmentSlots);

    /// <summary>Special slots per knight. The Gold Cart quest raises this to 2.</summary>
    public static int SpecialSlotCount => Mathf.Max(1, SaveManager.Data.specialSlots);

    // ---------- ownership ----------

    public static bool IsOwned(string equipmentId)
    {
        if (string.IsNullOrEmpty(equipmentId)) return false;
        var owned = SaveManager.Data.ownedEquipment;
        return owned != null && owned.Contains(equipmentId);
    }

    /// <summary>Idempotent, so a quest that somehow completes twice can't duplicate a reward.</summary>
    public static void Own(string equipmentId)
    {
        if (string.IsNullOrEmpty(equipmentId)) return;
        var owned = EnsureOwned();
        if (owned.Contains(equipmentId)) return;
        owned.Add(equipmentId);
        SaveManager.Save();
        OnLoadoutChanged?.Invoke();
    }

    public static IEnumerable<EquipmentDefinition> OwnedEquipment()
    {
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) yield break;
        var owned = SaveManager.Data.ownedEquipment;
        if (owned == null) yield break;
        for (int i = 0; i < owned.Count; i++)
        {
            var def = catalog.Find(owned[i]);
            if (def != null) yield return def;
        }
    }

    // ---------- equipping ----------

    public static IReadOnlyList<string> EquippedFor(string knightId)
    {
        return EnsureLoadout(knightId).equipped;
    }

    /// <summary>The item in one slot, or null when the slot is empty or out of range.</summary>
    public static EquipmentDefinition EquippedAt(string knightId, int slot)
    {
        var list = EnsureLoadout(knightId).equipped;
        if (slot < 0 || slot >= list.Count) return null;
        var catalog = EquipmentCatalog.Instance;
        return catalog != null ? catalog.Find(list[slot]) : null;
    }

    public static string KnightHolding(string equipmentId)
    {
        if (string.IsNullOrEmpty(equipmentId)) return null;
        if (EnsureLoadout(LeftKnight).equipped.Contains(equipmentId)) return LeftKnight;
        if (EnsureLoadout(RightKnight).equipped.Contains(equipmentId)) return RightKnight;
        return null;
    }

    /// <summary>
    /// Puts an owned item in a slot, taking it off the other knight first if
    /// they were carrying it. Returns false when the item isn't owned or the
    /// slot is out of range.
    /// </summary>
    public static bool Equip(string knightId, int slot, string equipmentId)
    {
        if (slot < 0 || slot >= SlotCount) return false;
        if (!IsOwned(equipmentId)) return false;

        // One copy of the item exists, so it can only be in one place
        string holder = KnightHolding(equipmentId);
        if (holder != null) RemoveFrom(holder, equipmentId);

        var list = EnsureLoadout(knightId).equipped;
        while (list.Count <= slot) list.Add("");
        list[slot] = equipmentId;

        SaveManager.Save();
        OnLoadoutChanged?.Invoke();
        return true;
    }

    public static void Unequip(string knightId, int slot)
    {
        var list = EnsureLoadout(knightId).equipped;
        if (slot < 0 || slot >= list.Count) return;
        if (string.IsNullOrEmpty(list[slot])) return;
        list[slot] = "";
        SaveManager.Save();
        OnLoadoutChanged?.Invoke();
    }

    // ---------- specials ----------

    /// <summary>
    /// Stored when the player deliberately empties the special slot. Distinct
    /// from "" — an empty string means "never chosen", which still resolves to
    /// the knight's stock special, and old saves are full of them.
    /// </summary>
    public const string NoSpecial = "none";

    /// <summary>The special chosen in one slot, or empty for the prefab default.</summary>
    public static string SpecialIdFor(string knightId, int slot = 0)
    {
        var list = EnsureLoadout(knightId).specials;
        if (slot < 0 || slot >= list.Count) return "";
        return list[slot] ?? "";
    }

    /// <summary>The player has explicitly emptied this slot.</summary>
    public static bool IsSpecialSlotEmpty(string knightId, int slot)
    {
        string id = SpecialIdFor(knightId, slot);
        if (id == NoSpecial) return true;
        // Only slot 0 falls back to the prefab's stock special, so an unset
        // second slot is already empty without anyone having said so
        return slot > 0 && string.IsNullOrEmpty(id);
    }

    /// <summary>
    /// This knight fires nothing at all: every unlocked slot is empty. What the
    /// bare-run feat asks about, so it has to mean "no special reaches the run",
    /// not merely "slot 0 was emptied".
    /// </summary>
    public static bool HasNoSpecial(string knightId)
    {
        int slots = SpecialSlotCount;
        for (int slot = 0; slot < slots; slot++)
        {
            if (!IsSpecialSlotEmpty(knightId, slot)) return false;
        }
        return true;
    }

    /// <summary>
    /// Puts a special in one slot. A knight cannot fire the same special twice
    /// on one bar, so choosing one they already hold in another slot SWAPS the
    /// two rather than duplicating it — the same "one copy, it moves" rule
    /// equipment uses across knights, applied within a knight.
    /// </summary>
    public static void SetSpecial(string knightId, int slot, string specialId)
    {
        if (slot < 0 || slot >= SpecialSlotCount) return;

        var list = EnsureLoadout(knightId).specials;
        while (list.Count <= slot) list.Add("");

        string incoming = specialId ?? "";
        if (!string.IsNullOrEmpty(incoming) && incoming != NoSpecial)
        {
            // An unset slot displaces as an explicit "none", never as "": moving
            // a special off slot 0 must not hand that slot back to the prefab's
            // stock special, which would silently undo the swap for the run
            string displaced = string.IsNullOrEmpty(list[slot]) ? NoSpecial : list[slot];
            for (int i = 0; i < list.Count; i++)
            {
                if (i != slot && list[i] == incoming) list[i] = displaced;
            }
        }

        list[slot] = incoming;
        SaveManager.Save();
        OnLoadoutChanged?.Invoke();
    }

    /// <summary>
    /// The two stock specials are owned from a new game and never appear in the
    /// save's owned list, so ownership has to ask the definition as well.
    /// </summary>
    public static bool IsSpecialOwned(SpecialDefinition special)
    {
        if (special == null) return false;
        return special.OwnedFromTheStart || IsOwned(special.Id);
    }

    public static IEnumerable<SpecialDefinition> OwnedSpecials()
    {
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) yield break;
        var all = catalog.Specials;
        for (int i = 0; i < all.Count; i++)
        {
            if (IsSpecialOwned(all[i])) yield return all[i];
        }
    }

    /// <summary>
    /// What one slot actually holds, or null when it is empty. Specials are NOT
    /// exclusive the way equipment is: both knights firing the same one is a
    /// legitimate loadout, and there is no physical object being shared to argue
    /// otherwise.
    ///
    /// <paramref name="fallback"/> is the knight prefab's stock special and
    /// applies to slot 0 only — a slot the player unlocked later has nothing
    /// stock about it, so it stays empty until they fill it.
    /// </summary>
    public static SpecialDefinition ResolveSpecial(string knightId, SpecialDefinition fallback, int slot = 0)
    {
        // Deliberately empty beats the prefab's stock special. A knight with no
        // special simply never has one to fire.
        if (IsSpecialSlotEmpty(knightId, slot)) return null;

        var catalog = EquipmentCatalog.Instance;
        if (catalog != null)
        {
            var chosen = catalog.FindSpecial(SpecialIdFor(knightId, slot));
            if (chosen != null && IsSpecialOwned(chosen)) return chosen;
        }
        return slot == 0 ? fallback : null;
    }

    /// <summary>
    /// Everything this knight fires on one full bar, in slot order. Two slots
    /// means two specials go off together, which is the whole reward — so this
    /// is a list rather than a choice between them.
    /// </summary>
    public static List<SpecialDefinition> ResolveSpecials(string knightId, SpecialDefinition fallback)
    {
        var result = new List<SpecialDefinition>();
        int slots = SpecialSlotCount;
        for (int slot = 0; slot < slots; slot++)
        {
            var special = ResolveSpecial(knightId, fallback, slot);
            // Belt and braces against a save that somehow holds one special
            // twice: firing it twice on one bar does nothing the once didn't
            if (special != null && !result.Contains(special)) result.Add(special);
        }
        return result;
    }

    // ---------- applying to a run ----------

    /// <summary>"PlayerLeft" -> "left". Null for anything else.</summary>
    public static string KnightIdFromTag(string tag)
    {
        if (tag == "PlayerLeft") return LeftKnight;
        if (tag == "PlayerRight") return RightKnight;
        return null;
    }

    /// <summary>
    /// Writes every item this knight carries into their EquipmentBoost. Called
    /// once per run from Spawner.Start, before the first wave spawns.
    /// </summary>
    public static void ApplyToKnight(GameObject knight, string knightId)
    {
        if (knight == null) return;
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) return;

        var equipped = EnsureLoadout(knightId).equipped;
        int slots = SlotCount;
        for (int slot = 0; slot < equipped.Count && slot < slots; slot++)
        {
            // Slots past the one the player has unlocked are ignored rather than
            // cleared, so losing a slot (a wipe, a rollback) doesn't destroy the
            // arrangement of the ones they keep
            var def = catalog.Find(equipped[slot]);
            if (def == null) continue;
            if (!IsOwned(def.Id)) continue;
            def.Apply(knight);

            // Recorded on the boost rather than counted here, so "carrying
            // nothing" stays true even if an item is applied by another route
            var boost = knight.GetComponent<EquipmentBoost>();
            if (boost != null) boost.NoteCarrying();
        }
    }

    // ---------- storage ----------

    private static List<string> EnsureOwned()
    {
        return SaveManager.Data.ownedEquipment ?? (SaveManager.Data.ownedEquipment = new List<string>());
    }

    private static void RemoveFrom(string knightId, string equipmentId)
    {
        var list = EnsureLoadout(knightId).equipped;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == equipmentId) list[i] = "";
        }
    }

    private static KnightLoadout EnsureLoadout(string knightId)
    {
        var all = SaveManager.Data.loadouts ?? (SaveManager.Data.loadouts = new List<KnightLoadout>());
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].knightId == knightId) return Normalize(all[i]);
        }
        var created = new KnightLoadout { knightId = knightId };
        all.Add(created);
        return Normalize(created);
    }

    // JsonUtility can deserialize a null list where the initializer would have
    // made an empty one, so never trust the field straight off a loaded save
    private static KnightLoadout Normalize(KnightLoadout loadout)
    {
        if (loadout.equipped == null) loadout.equipped = new List<string>();
        if (loadout.specials == null) loadout.specials = new List<string>();

        // Pre-v9 saves kept the one special in its own field. Folding it in here
        // rather than in Migrate covers every route into a loadout — including
        // SaveData's own defaults, and any file that skipped the version bump.
        if (!string.IsNullOrEmpty(loadout.specialId))
        {
            if (loadout.specials.Count == 0) loadout.specials.Add(loadout.specialId);
            loadout.specialId = "";
        }
        return loadout;
    }
}

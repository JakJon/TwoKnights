using System;

/// <summary>
/// Crystals are the quest currency and the camp shop's only price tag: earned
/// exclusively from quest rewards, spent exclusively on equipment.
///
/// Unlike gold there is no scene singleton. Nothing awards crystals mid-run, so
/// a static save-backed bank is enough — and it stays reachable from
/// QuestProgress, which pays rewards out without a live GameObject in sight.
/// </summary>
public static class CrystalBank
{
    public static event Action<int> OnCrystalsChanged;

    public static int Balance => SaveManager.Data.crystals;

    /// <summary>Every crystal this file has ever been granted. Spending never lowers it.</summary>
    public static int TotalEarned => SaveManager.Data.totalCrystalsEarned;

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        SaveManager.Data.crystals += amount;
        // The lifetime total is counted at the only place crystals are ever granted,
        // so it cannot drift from the balance's history the way a derived figure would.
        SaveManager.Data.totalCrystalsEarned += amount;
        // Latch here rather than reading the balance later: this is the only moment
        // that says "has earned crystals", and spending back to zero must not undo it.
        SaveManager.Data.everHadCrystals = true;
        SaveManager.Save();
        OnCrystalsChanged?.Invoke(Balance);
    }

    public static bool CanAfford(int cost)
    {
        return cost >= 0 && Balance >= cost;
    }

    /// <summary>
    /// Spends when affordable, otherwise changes nothing and returns false —
    /// so callers can use this directly as the purchase gate rather than
    /// checking the balance and deducting in two steps.
    /// </summary>
    public static bool TrySpend(int cost)
    {
        if (!CanAfford(cost)) return false;
        SaveManager.Data.crystals -= cost;
        SaveManager.Save();
        OnCrystalsChanged?.Invoke(Balance);
        return true;
    }
}

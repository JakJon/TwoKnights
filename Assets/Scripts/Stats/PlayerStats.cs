using System;
using System.Collections.Generic;

public static class PlayerStats
{
    public static event Action<string, int> OnStatChanged;

    public static int Get(string key)
    {
        var entry = FindEntry(key);
        return entry?.value ?? 0;
    }

    public static void Set(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        var entry = FindEntry(key);
        if (entry == null)
        {
            entry = new StatEntry { key = key, value = value };
            EnsureList().Add(entry);
        }
        else
        {
            entry.value = value;
        }
        SaveManager.Save();
        OnStatChanged?.Invoke(key, entry.value);
    }

    public static void Increment(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key) || amount == 0) return;
        Set(key, Get(key) + amount);
    }

    public static IEnumerable<StatEntry> All => EnsureList();

    private static List<StatEntry> EnsureList()
    {
        return SaveManager.Data.stats ?? (SaveManager.Data.stats = new List<StatEntry>());
    }

    private static StatEntry FindEntry(string key)
    {
        var list = SaveManager.Data.stats;
        if (list == null) return null;
        foreach (var entry in list)
        {
            if (entry != null && entry.key == key) return entry;
        }
        return null;
    }
}

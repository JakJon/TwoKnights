using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerStats
{
    public static event Action<string, int> OnStatChanged;

    // Every Set used to call SaveManager.Save() — a full JSON serialize plus an
    // atomic temp-file replace, per stat write. One kill now touches several
    // counters (per-map, per-family, status applications), so writing through
    // to disk each time meant hundreds of file replaces per wave. Writes land
    // in memory and Flush() persists them at the safe points: wave end, run end,
    // and application quit. A crash costs at most one wave of counters.
    private static bool _dirty;

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
            // A write that changes nothing can't change quest state either, so
            // don't dirty the save or wake every OnStatChanged listener for it
            if (entry.value == value) return;
            entry.value = value;
        }
        _dirty = true;
        OnStatChanged?.Invoke(key, entry.value);
    }

    public static void Increment(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key) || amount == 0) return;
        Set(key, Get(key) + amount);
    }

    /// <summary>
    /// Raises a stat to at least <paramref name="value"/>, never lowering it.
    /// For high-water marks like the furthest wave reached on a map, where a
    /// later worse run must not erase the record.
    /// </summary>
    public static void Raise(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (Get(key) < value) Set(key, value);
    }

    /// <summary>
    /// Persists pending stat writes. Cheap to call when nothing changed.
    /// </summary>
    public static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        SaveManager.Save();
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

    // Quitting mid-run (or leaving play mode in the editor) would otherwise
    // drop everything counted since the last wave ended.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookQuit()
    {
        Application.quitting -= Flush;
        Application.quitting += Flush;
    }
}

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

    // ---- The wave journal ----
    //
    // A wave's counting is PROVISIONAL until the wave is survived. Everything a
    // knight does while a wave is running is recorded here instead of being
    // written, and only a completed wave commits it; dying throws the whole lot
    // away (owner's call, 2026-09-15). Progress toward a quest, and finishing one,
    // are things you carry OUT of a fight, not things you bank mid-swing.
    //
    // It buffers the OPERATIONS rather than final values, so a commit replays
    // Increment/Raise/Set down their ordinary paths and the quest meters, the
    // fan-out and OnStatChanged all fire exactly as they always did - just later,
    // and only if the wave was won. _pending mirrors the effect of the buffered
    // ops so Get still reads back what this wave has counted so far; without that
    // a second Increment in a wave would re-read the pre-wave value and overwrite
    // the first.
    private enum JournalOp { Set, Increment, Raise }
    private struct JournalEntry
    {
        public JournalOp op;
        public string key;
        public int value;
    }
    private static readonly List<JournalEntry> _journal = new List<JournalEntry>();
    private static readonly Dictionary<string, int> _pending = new Dictionary<string, int>();
    private static bool _journaling;

    /// <summary>
    /// Called by the Spawner as a wave begins. Everything counted from here is held
    /// until <see cref="CommitWave"/>, and dropped by <see cref="DiscardWave"/>.
    /// </summary>
    public static void BeginWave()
    {
        _journaling = true;
        _journal.Clear();
        _pending.Clear();
    }

    /// <summary>The wave was survived: replay everything it counted, for real.</summary>
    public static void CommitWave()
    {
        if (!_journaling) return;
        _journaling = false;

        // Copied out first: replaying goes back through Increment/Raise/Set, and
        // those must see journaling already off or they would re-buffer themselves.
        var entries = _journal.ToArray();
        _journal.Clear();
        _pending.Clear();

        for (int i = 0; i < entries.Length; i++)
        {
            switch (entries[i].op)
            {
                case JournalOp.Increment: Increment(entries[i].key, entries[i].value); break;
                case JournalOp.Raise:     Raise(entries[i].key, entries[i].value);     break;
                default:                  Set(entries[i].key, entries[i].value);       break;
            }
        }
    }

    /// <summary>
    /// A knight died, or the run was abandoned part way through a wave. Nothing
    /// that wave counted ever happened.
    /// </summary>
    public static void DiscardWave()
    {
        _journaling = false;
        _journal.Clear();
        _pending.Clear();
    }

    /// <summary>Is a wave's counting currently being held back?</summary>
    public static bool IsJournaling => _journaling;

    public static int Get(string key)
    {
        // Mid-wave, what this wave has counted so far counts as read-back even
        // though none of it has been written yet.
        if (_journaling && key != null && _pending.TryGetValue(key, out int held)) return held;
        var entry = FindEntry(key);
        return entry?.value ?? 0;
    }

    /// <summary>
    /// Writes a lifetime stat. Does NOT feed the per-quest meters — this is the
    /// low-level write and the bookkeeping one, for values that are recomputed
    /// wholesale (how many wave types you have seen) or that no objective reads.
    /// Things that HAPPEN go through <see cref="Increment"/> or <see cref="Raise"/>.
    /// </summary>
    public static void Set(string key, int value)
    {
        if (_journaling && Hold(JournalOp.Set, key, value)) return;
        Write(key, value);
    }

    /// <summary>
    /// Buffers one operation for the wave in progress and mirrors its effect into
    /// _pending. Returns false for keys that must never be held back.
    /// </summary>
    private static bool Hold(JournalOp op, string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return false;

        _journal.Add(new JournalEntry { op = op, key = key, value = value });

        switch (op)
        {
            case JournalOp.Increment:
                _pending[key] = Get(key) + value;
                break;
            case JournalOp.Raise:
                if (Get(key) < value) _pending[key] = value;
                break;
            default:
                _pending[key] = value;
                break;
        }
        return true;
    }

    private static void Write(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;

        // The tutorial run does not count. Every counter routes through here —
        // Increment and Raise both end up here — so this one line is the whole of
        // the rule, and it stops the tally rather than merely stopping quests from
        // reading it. See TutorialStatGate for why the weaker version failed.
        if (!TutorialStatGate.Allows(key)) return;

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

    /// <summary>Something happened and was counted. Feeds the per-quest meters too.</summary>
    public static void Increment(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key) || amount == 0) return;
        // Held, quest meters and all, until the wave is survived. Feeding the
        // meters here would let a quest complete on the swing that killed you.
        if (_journaling && Hold(JournalOp.Increment, key, amount)) return;
        // The quests FIRST, then the lifetime stat. The lifetime write is what
        // wakes QuestProgress.Evaluate, and it must not judge a quest against a
        // meter that has not been fed yet.
        QuestMeters.Add(key, amount);
        Write(key, Get(key) + amount);
    }

    /// <summary>
    /// Raises a stat to at least <paramref name="value"/>, never lowering it.
    /// For high-water marks like the furthest wave reached on a map, where a
    /// later worse run must not erase the record.
    /// </summary>
    public static void Raise(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_journaling && Hold(JournalOp.Raise, key, value)) return;
        // Offered to the quests whether or not it beats the lifetime record —
        // see QuestMeters.Reach. Their copies started later and have their own
        // best to beat.
        QuestMeters.Reach(key, value);
        if (Get(key) < value) Write(key, value);
    }

    /// <summary>
    /// Restores a value the save already knew, on load. Never reaches the per-quest
    /// meters: a map cleared last week is not progress on a quest opened today.
    /// </summary>
    public static void Republish(string key, int value)
    {
        // Never journalled: this is the save telling us what it already knew, not
        // something the current wave did.
        if (string.IsNullOrEmpty(key)) return;
        var entry = FindEntry(key);
        if ((entry?.value ?? 0) < value) Write(key, value);
    }

    /// <summary>
    /// One quest's private copy of a counter. Only <see cref="QuestMeters"/> calls
    /// this; it exists so those writes cannot recurse back into the fan-out.
    /// </summary>
    public static void WriteQuestMeter(string key, int value)
    {
        Write(key, value);
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

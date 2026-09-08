using System.IO;
using UnityEngine;

public static class SaveManager
{
    /// <summary>How many files the file select offers.</summary>
    public const int SlotCount = 3;

    // Slot 1 deliberately keeps the original filename: the file every existing
    // player already has on disk IS file 1, with no copying or renaming step that
    // could half-fail and lose a save.
    private const string FileName = "save.json";
    private const string TempFileName = "save.json.tmp";

    private static SaveData _data;
    private static bool _loaded;

    /// <summary>
    /// Raised after <see cref="SelectSlot"/> switches files, for statics that
    /// cached something save-derived and would otherwise carry it across.
    /// Scene objects need no hook — switching files reloads the scene.
    /// </summary>
    public static event System.Action OnActiveSlotChanged;

    /// <summary>Which file is live. 1-based, matching the bars in the file select.</summary>
    public static int ActiveSlot { get; private set; } = 1;

    /// <summary>Whether <see cref="Data"/> is already resolved for the active slot.</summary>
    public static bool IsLoaded => _loaded;

    public static SaveData Data
    {
        get
        {
            if (!_loaded) Load();
            return _data;
        }
    }

    public static string SavePath => PathForSlot(ActiveSlot);
    private static string TempPath => TempPathForSlot(ActiveSlot);

    private static string PathForSlot(int slot)
    {
        string name = slot <= 1 ? FileName : $"save_{slot}.json";
        return Path.Combine(Application.persistentDataPath, name);
    }

    private static string TempPathForSlot(int slot)
    {
        string name = slot <= 1 ? TempFileName : $"save_{slot}.json.tmp";
        return Path.Combine(Application.persistentDataPath, name);
    }

    /// <summary>True when this file has been played — i.e. it exists on disk.</summary>
    public static bool SlotExists(int slot)
    {
        return File.Exists(PathForSlot(slot));
    }

    /// <summary>
    /// Reads a file for display without disturbing the active one, so the file
    /// select can draw all three bars while one of them is live. Returns null for
    /// an empty (or unreadable) file.
    /// </summary>
    public static SaveData PeekSlot(int slot)
    {
        // Existence on disk is what makes a file a file — checked before the live
        // data, because the active slot holds a default SaveData on a fresh
        // install and that must still read as empty rather than as a played file.
        if (!SlotExists(slot)) return null;
        if (slot == ActiveSlot && _loaded) return _data;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathForSlot(slot)));
            if (data == null) return null;
            // Migrated in the returned copy only. The real migration runs when the
            // file is actually loaded; this just makes an old save display right.
            Migrate(data, slot);
            EnsureProfileName(data, slot);
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Could not read file {slot}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Makes another file live. The outgoing file is deliberately NOT written
    /// first: the only moment this is called is the file select at startup, and
    /// saving there would materialise an empty save.json for a player who has
    /// never played file 1 — an empty file would then show as an existing one.
    /// Anything accumulated before the pick (a few seconds of play time) is
    /// discarded along with the old data, which is the correct attribution.
    /// </summary>
    public static void SelectSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, SlotCount);
        if (_loaded && slot == ActiveSlot) return;

        ActiveSlot = slot;
        _data = null;
        _loaded = false;
        Load();
        OnActiveSlotChanged?.Invoke();
    }

    public static void Load()
    {
        _loaded = true;

        if (!File.Exists(SavePath))
        {
            _data = new SaveData();
            EnsureProfileName(_data, ActiveSlot);
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            _data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            Migrate(_data, ActiveSlot);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to load {SavePath}: {e.Message}. Using defaults.");
            _data = new SaveData();
        }

        EnsureProfileName(_data, ActiveSlot);
    }

    /// <summary>The name a never-named file falls back to.</summary>
    public static string DefaultProfileName(int slot) => $"File {slot}";

    private static void EnsureProfileName(SaveData data, int slot)
    {
        if (data == null) return;
        if (string.IsNullOrEmpty(data.profileName)) data.profileName = DefaultProfileName(slot);
    }

    public static void Save()
    {
        if (!_loaded) Load();

        try
        {
            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(SavePath))
            {
                File.Replace(TempPath, SavePath, null);
            }
            else
            {
                File.Move(TempPath, SavePath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to save {SavePath}: {e.Message}");
        }
    }

    /// <summary>Wipes the file that is currently live. The other files are untouched.</summary>
    public static void DeleteSave()
    {
        // Read the name before the data goes, so the session keeps reading as the
        // same file: a wipe resets progress, not who the file belongs to.
        string name = _loaded && _data != null ? _data.profileName : null;

        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempPath)) File.Delete(TempPath);
        _data = new SaveData();
        _data.profileName = name;
        EnsureProfileName(_data, ActiveSlot);
        _loaded = true;
    }

    private static void Migrate(SaveData data, int slot)
    {
        if (data.version < 2)
        {
            // Was a knightRank floor; the rank system was removed in v7
            data.version = 2;
        }
        if (data.version < 3)
        {
            if (data.completedQuests == null) data.completedQuests = new System.Collections.Generic.List<QuestCompletion>();
            data.version = 3;
        }
        if (data.version < 4)
        {
            if (data.stats == null) data.stats = new System.Collections.Generic.List<StatEntry>();
            data.version = 4;
        }
        if (data.version < 5)
        {
            if (data.maps == null) data.maps = new System.Collections.Generic.List<MapRecord>();
            data.version = 5;
        }
        if (data.version < 6)
        {
            if (data.ownedEquipment == null) data.ownedEquipment = new System.Collections.Generic.List<string>();
            if (data.loadouts == null) data.loadouts = new System.Collections.Generic.List<KnightLoadout>();
            if (data.seenQuests == null) data.seenQuests = new System.Collections.Generic.List<string>();
            if (data.equipmentSlots < 1) data.equipmentSlots = 1;

            // The two rat-slayer quests were cut when the quest system was
            // reworked. Their completion records would otherwise sit in the save
            // forever as orphans no QuestDatabase entry can resolve.
            PruneCompletions(data, "rat_slayer", "rat_slayer_2");

            // seenQuests is deliberately left empty: every quest in the reworked
            // system is new to this save, so all of them should read as unseen
            // and light the notification dot on the first v6 launch.
            data.version = 6;
        }
        if (data.version < 7)
        {
            // Honor and Knight Rank are gone. Nothing to migrate — the fields no
            // longer exist on SaveData, so the old JSON keys are simply dropped
            // on the next write.
            data.version = 7;
        }
        if (data.version < 8)
        {
            // A save written before file slots existed is the one file the player
            // has been playing all along, and it lands in slot 1. It needs no name
            // of its own — EnsureProfileName gives every unnamed file "File N" on
            // the way out of Load, which is the honest label for a file nobody has
            // ever been asked to name.

            // No lifetime record was ever kept, so seed it from the balance. You
            // cannot be holding more crystals than you earned, which makes this a
            // floor rather than a guess, and the counter is exact from here on.
            if (data.totalCrystalsEarned < data.crystals) data.totalCrystalsEarned = data.crystals;

            data.version = 8;
        }
        if (data.version < 9)
        {
            // Special slots joined equipment slots as a thing a quest can grant.
            // The one special every pre-v9 knight had is folded into slot 0 by
            // Loadout.Normalize, which runs on every read — nothing to do here
            // but establish the floor.
            if (data.specialSlots < 1) data.specialSlots = 1;
            data.version = 9;
        }
        if (data.version < 10)
        {
            // Existing players must not be marched through a tutorial for a game
            // they already know — but "the file exists on disk" is NOT the same as
            // "it has been played". A file gets written the moment it is picked, so
            // that test also catches a file somebody opened once and left, which is
            // exactly the file that still wants teaching. Ask for evidence instead.
            data.tutorialCompleted = HasBeenPlayed(data);
            data.version = 10;
        }
    }

    /// <summary>
    /// Whether anything has actually happened on this file. Any one of these is
    /// proof: a wave was reached, currency was earned, or a quest was finished.
    /// Play time alone is not — sitting in the camp menu accrues it.
    /// </summary>
    private static bool HasBeenPlayed(SaveData data)
    {
        if (data.furthestWave > 0) return true;
        if (data.gold > 0) return true;
        if (data.totalCrystalsEarned > 0 || data.crystals > 0) return true;
        if (data.completedQuests != null && data.completedQuests.Count > 0) return true;
        return false;
    }

    private static void PruneCompletions(SaveData data, params string[] questIds)
    {
        if (data.completedQuests == null) return;
        for (int i = data.completedQuests.Count - 1; i >= 0; i--)
        {
            var entry = data.completedQuests[i];
            if (entry == null)
            {
                data.completedQuests.RemoveAt(i);
                continue;
            }
            for (int q = 0; q < questIds.Length; q++)
            {
                if (entry.questId == questIds[q])
                {
                    data.completedQuests.RemoveAt(i);
                    break;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public static class QuestProgress
{
    public static event Action<string> OnQuestCompleted;
    public static event Action<string> OnQuestProgressChanged;
    /// <summary>A quest just became visible. Drives the camp's notification dot.</summary>
    public static event Action<string> OnQuestUnlocked;

    private static bool _subscribed;
    // Quests already known to be unlocked, so becoming unlocked fires exactly once
    private static readonly HashSet<string> _knownUnlocked = new HashSet<string>();
    private static bool _evaluating;
    private static bool _seeding;

    public static void EnsureInitialized()
    {
        if (_subscribed) return;
        _subscribed = true;
        PlayerStats.OnStatChanged += HandleStatChanged;

        SeedUnlocked();

#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            PlayerStats.OnStatChanged -= HandleStatChanged;
            _subscribed = false;
        };
#endif
    }

    // ---------- state ----------

    public static bool IsCompleted(string questId)
    {
        return FindEntry(questId) != null;
    }

    public static string GetCompletionDate(string questId)
    {
        var entry = FindEntry(questId);
        return entry != null ? entry.completedDate : null;
    }

    public static bool IsUnlocked(Quest quest)
    {
        return quest != null && quest.IsUnlocked;
    }

    /// <summary>Quests the player can see: unlocked, completed or not, in database order.</summary>
    public static IEnumerable<Quest> Visible
    {
        get
        {
            foreach (var quest in QuestDatabase.All)
            {
                if (quest.IsUnlocked) yield return quest;
            }
        }
    }

    public static IEnumerable<Quest> VisibleForMap(string mapId)
    {
        foreach (var quest in Visible)
        {
            if (quest.MapId == mapId) yield return quest;
        }
    }

    /// <summary>Total progress across a quest's objectives, for a single summary number.</summary>
    public static int GetProgress(Quest quest)
    {
        if (quest == null || !quest.HasObjectives) return 0;
        int total = 0;
        for (int i = 0; i < quest.Objectives.Length; i++) total += quest.Objectives[i].Current;
        return total;
    }

    // ---------- the notification dot ----------

    /// <summary>An unlocked quest the player has never opened in the log.</summary>
    public static bool HasUnseen
    {
        get
        {
            var seen = SaveManager.Data.seenQuests;
            foreach (var quest in Visible)
            {
                if (seen == null || !seen.Contains(quest.Id)) return true;
            }
            return false;
        }
    }

    public static bool IsSeen(string questId)
    {
        var seen = SaveManager.Data.seenQuests;
        return seen != null && seen.Contains(questId);
    }

    /// <summary>Called when the player actually focuses a quest in the log.</summary>
    public static void MarkSeen(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        var seen = SaveManager.Data.seenQuests ?? (SaveManager.Data.seenQuests = new List<string>());
        if (seen.Contains(questId)) return;
        seen.Add(questId);
        SaveManager.Save();
    }

    // ---------- completion ----------

    public static bool CompleteQuest(Quest quest)
    {
        if (quest == null) return false;
        if (IsCompleted(quest.Id)) return false;

        var list = SaveManager.Data.completedQuests ?? (SaveManager.Data.completedQuests = new List<QuestCompletion>());
        list.Add(new QuestCompletion
        {
            questId = quest.Id,
            completedDate = DateTime.Now.ToString("yyyy-MM-dd"),
        });

        var reward = quest.Reward;
        if (reward != null)
        {
            if (reward.Crystals > 0) CrystalBank.Add(reward.Crystals);
            if (reward.GrantsEquipment) Loadout.Own(reward.EquipmentId);
            if (reward.ExtraEquipmentSlot)
            {
                // ONE MORE, not "raise it to two". Slots used to be a single fact
                // the game could only ever learn once, so a second quest offering
                // one paid out nothing to a player who already had it. The panel
                // builds its rows off Loadout.SlotCount, so it follows on its own.
                SaveManager.Data.equipmentSlots = Mathf.Max(1, SaveManager.Data.equipmentSlots) + 1;
            }
            if (reward.ExtraSpecialSlot)
            {
                // Same "one more" rule as the equipment slot above, for the same
                // reason. The extra slot arrives empty; nothing is auto-filled.
                SaveManager.Data.specialSlots = Mathf.Max(1, SaveManager.Data.specialSlots) + 1;
            }
            if (reward.UnlocksMap)
            {
                // Ordinarily already done: the gate kill this quest asks for opens
                // the map, and only then does the quest complete. Applying it here
                // too is idempotent, and it means the listed reward is true even
                // for a quest that opens a map off something other than a gate.
                MapProgressStore.Unlock(reward.UnlocksMapId);
            }
        }

        // Completion is published as a stat so quest chains gate on it through
        // the same path as everything else — no separate prerequisite mechanism,
        // and the write re-enters Evaluate to open whatever comes next.
        //
        // It goes in BEFORE the file write. A stat write only dirties PlayerStats
        // and waits for the next flush, so publishing after the save meant a
        // session could end holding the completion on disk and its gate only in
        // memory — the record survived, the thing chains actually read did not.
        // Both now land in the same write.
        PlayerStats.Set(QuestDatabase.CompletionStatKey(quest.Id), 1);

        SaveManager.Save();
        OnQuestCompleted?.Invoke(quest.Id);
        return true;
    }

    public static bool CompleteQuest(string questId)
    {
        return CompleteQuest(QuestDatabase.Get(questId));
    }

    // ---------- reactive evaluation ----------

    private static void HandleStatChanged(string key, int value)
    {
        // Seeding republishes a whole file's completions in one go. Letting that
        // run Evaluate would announce every already-open quest as newly unlocked,
        // which is the exact thing the re-seed exists to prevent.
        if (_seeding) return;

        foreach (var quest in QuestDatabase.All)
        {
            if (quest.WatchesStat(key)) OnQuestProgressChanged?.Invoke(quest.Id);
        }
        Evaluate();
    }

    /// <summary>
    /// Opens whatever the latest stats unlock, then completes whatever they
    /// satisfy — looping because completing one quest publishes a stat that can
    /// open the next link in a chain. The guard bounds a data error (a quest
    /// gated on its own completion) to a stall rather than a hang.
    /// </summary>
    private static void Evaluate()
    {
        if (_evaluating) return;
        _evaluating = true;
        try
        {
            bool changed = true;
            int passes = 0;
            while (changed && passes++ < 16)
            {
                changed = false;
                foreach (var quest in QuestDatabase.All)
                {
                    if (!quest.IsUnlocked) continue;

                    if (_knownUnlocked.Add(quest.Id))
                    {
                        OnQuestUnlocked?.Invoke(quest.Id);
                        changed = true;
                    }

                    if (!IsCompleted(quest.Id) && quest.IsSatisfied)
                    {
                        CompleteQuest(quest);
                        changed = true;
                    }
                }
            }
        }
        finally
        {
            _evaluating = false;
        }
    }

    // Seed silently: whatever is already open is not "new", and the notification
    // dot is driven by seenQuests, not by this event
    private static void SeedUnlocked()
    {
        _seeding = true;
        try
        {
            RepublishCompletions();

            _knownUnlocked.Clear();
            foreach (var quest in QuestDatabase.All)
            {
                if (quest.IsUnlocked) _knownUnlocked.Add(quest.Id);
            }
        }
        finally
        {
            _seeding = false;
        }
    }

    /// <summary>
    /// Rebuilds the completion stats from the completion records, making the
    /// record the single source of truth and the stat a cache of it.
    ///
    /// The stat used to be written once, at the moment of completion, and a save
    /// that lost that write — a crash or a quit before the next flush — kept the
    /// completion and lost the gate. Nothing recomputed it, so every quest behind
    /// that link stayed invisible on that file permanently. Deriving it on load
    /// repairs saves already in that state as well as preventing new ones.
    /// </summary>
    private static void RepublishCompletions()
    {
        var list = SaveManager.Data.completedQuests;
        if (list == null) return;
        foreach (var entry in list)
        {
            if (entry == null || string.IsNullOrEmpty(entry.questId)) continue;
            // A no-op on a healthy save: Set ignores a write that changes nothing,
            // so only a file actually missing a gate is dirtied.
            PlayerStats.Set(QuestDatabase.CompletionStatKey(entry.questId), 1);
        }
    }

    // This cache is static, so it outlives the scene reload that a file switch
    // performs. Without re-seeding, quests one file had already opened would be
    // remembered as open on the file switched to, and that file's own unlocks
    // would never announce themselves.
    private static void HandleActiveSlotChanged()
    {
        SeedUnlocked();
        // The newly loaded save may already satisfy quests, exactly as at startup
        Evaluate();
    }

    private static QuestCompletion FindEntry(string questId)
    {
        var list = SaveManager.Data.completedQuests;
        if (list == null) return null;
        foreach (var entry in list)
        {
            if (entry != null && entry.questId == questId) return entry;
        }
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        _subscribed = false;
        EnsureInitialized();
        SaveManager.OnActiveSlotChanged -= HandleActiveSlotChanged;
        SaveManager.OnActiveSlotChanged += HandleActiveSlotChanged;
        // A save loaded from disk may already satisfy quests added since it was
        // written, so settle the whole database once at startup
        Evaluate();
    }
}

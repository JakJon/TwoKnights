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

    /// <summary>
    /// Quests the player actually has: introduced by their NPC, completed or not,
    /// in database order.
    ///
    /// A quest whose gate has opened but whose scene has not played yet is NOT
    /// here. The cutscene is the moment a quest starts existing — before it, the
    /// player has not been asked, and a log listing things nobody has mentioned is
    /// how the old system felt like a spreadsheet.
    /// </summary>
    public static IEnumerable<Quest> Visible
    {
        get
        {
            foreach (var quest in QuestDatabase.All)
            {
                // Completed counts as introduced whatever the record says — an
                // old save has completions and no announcements at all.
                if (IsAnnounced(quest.Id) || IsCompleted(quest.Id)) yield return quest;
            }
        }
    }

    // ---------- announcement ----------

    /// <summary>Has this quest's NPC turned up and introduced it?</summary>
    public static bool IsAnnounced(string questId)
    {
        var list = SaveManager.Data.announcedQuests;
        return list != null && list.Contains(questId);
    }

    /// <summary>
    /// The scene played. From here the quest is in the log, its meters run, and
    /// anything it reveals may be drafted.
    ///
    /// Persisted, because it is the record of a thing that happened. The in-memory
    /// "already queued" set cannot do this job: a run abandoned before the scene
    /// played would leave the quest remembered as introduced and it would never get
    /// its moment.
    /// </summary>
    public static void MarkAnnounced(string questId)
    {
        if (string.IsNullOrEmpty(questId) || IsAnnounced(questId)) return;
        var list = SaveManager.Data.announcedQuests ??
                   (SaveManager.Data.announcedQuests = new List<string>());
        list.Add(questId);
        SaveManager.Save();
        // Opening the log is what the player does next, and the meters that start
        // now want the database settled around them.
        Evaluate();
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
            foreach (var quest in Visible)
            {
                if (!IsSeen(quest.Id)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Whether the log still owes the player a look at this quest.
    ///
    /// A finished quest is always seen, opened or not. The dot and the sheen exist
    /// to point at something the player might still act on; pointing at the
    /// completed shelf is pointing at a receipt, and a dot that only clears by
    /// unfolding an archive is a chore rather than a notice.
    /// </summary>
    public static bool IsSeen(string questId)
    {
        if (IsCompleted(questId)) return true;
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

        // Snapshot every counter that some other quest wants measured FROM here.
        // "Three more Rat Kings after the Crimson Twins" needs to know where the
        // Rat King tally stood at this instant; a minute later is already wrong.
        // Written before the save so both land in the same write, same as above.
        WriteUnlockBaselines(quest.Id);

        SaveManager.Save();
        OnQuestCompleted?.Invoke(quest.Id);
        return true;
    }

    public static bool CompleteQuest(string questId)
    {
        return CompleteQuest(QuestDatabase.Get(questId));
    }

    /// <summary>
    /// Records where each relative unlock condition's stat stood the moment
    /// <paramref name="questId"/> completed, for every quest gated that way.
    ///
    /// Cheap and idempotent: a quest completes once, and Set ignores a write that
    /// changes nothing. Doing it for all conditions rather than only the ones
    /// currently reachable means a quest added to the database later still has its
    /// baseline on an old save, as long as the parent completes after the update.
    /// </summary>
    private static void WriteUnlockBaselines(string questId)
    {
        foreach (var other in QuestDatabase.All)
        {
            if (other.Unlocks == null) continue;
            for (int i = 0; i < other.Unlocks.Length; i++)
            {
                var condition = other.Unlocks[i];
                if (condition == null || condition.SinceQuestId != questId) continue;
                PlayerStats.Set(
                    UnlockCondition.BaselineStatKey(questId, condition.StatKey),
                    PlayerStats.Get(condition.StatKey));
            }
        }
    }

    /// <summary>
    /// The tutorial run started or ended, so what counts as unlocked has changed
    /// wholesale.
    ///
    /// Forget only what the gate has CLOSED, and let Evaluate announce whatever it
    /// has opened. Re-seeding wholesale here was a bug with a very quiet symptom:
    /// SeedUnlocked files everything currently open as already-known WITHOUT
    /// announcing it, so the moment the tutorial run ended and the freeze lifted,
    /// every quest it had opened was marked known and never introduced itself. The
    /// player found them sitting in the log having met nobody.
    /// </summary>
    public static void HandleTutorialGateChanged()
    {
        _knownUnlocked.RemoveWhere(id =>
        {
            var quest = QuestDatabase.Get(id);
            return quest == null || !quest.IsUnlocked;
        });
        Evaluate();
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

    /// <summary>
    /// Rebuilds the "already queued" set from what the save says has been
    /// introduced — NOT from whose gates happen to be open.
    ///
    /// Seeding from the gates was the quiet bug behind "the quest was in my log and
    /// nobody ever came": every quest whose gate had opened was filed as known
    /// without anybody announcing it, and it could never announce itself
    /// afterwards. Seeding from the record instead means a gate that opened while
    /// no scene could play still owes the player a scene, and gets one at the end
    /// of the next wave.
    /// </summary>
    private static void SeedUnlocked()
    {
        _seeding = true;
        try
        {
            RepublishCompletions();

            _knownUnlocked.Clear();
            foreach (var quest in QuestDatabase.All)
            {
                // A quest already finished is behind us whatever the record says
                // about its announcement — an old save has completions and no
                // announcements at all.
                if (IsAnnounced(quest.Id) || IsCompleted(quest.Id)) _knownUnlocked.Add(quest.Id);
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

    /// <summary>
    /// The save was replaced wholesale — a file switch, or a wipe. Re-derive
    /// everything rather than trusting a cache built against data that is gone.
    ///
    /// Without this a wipe left _knownUnlocked holding every quest the old file had
    /// opened, so the fresh file's own unlocks never announced themselves: they were
    /// already "known".
    /// </summary>
    public static void ReloadForNewSave()
    {
        HandleActiveSlotChanged();
    }

    // This cache is static, so it outlives the scene reload that a file switch
    // performs. Without re-seeding, quests one file had already opened would be
    // remembered as open on the file switched to, and that file's own unlocks
    // would never announce themselves.
    private static void HandleActiveSlotChanged()
    {
        SeedUnlocked();
        // The newly loaded save may already satisfy quests, exactly as at startup.
        // Anything whose gate is open but which has never been introduced is still
        // owed its scene, and will get one at the end of the next wave.
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

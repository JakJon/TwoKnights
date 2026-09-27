using System.Collections.Generic;

/// <summary>
/// Every open quest's own private copy of the counters it cares about.
///
/// A quest objective used to read the lifetime stat directly, which meant a quest
/// was already part-finished the moment it appeared. Camp Cleanup II asks for two
/// thousand kills in the forest and opens when Camp Cleanup I ends at five
/// hundred — so it arrived reading 500/2000, having been "in progress" since
/// before the cartographer mentioned it. Worse for the record objectives: a quest
/// asking for seventy-five shadow arrows in one wave was simply complete on
/// arrival for anyone who had ever had a good wave.
///
/// So a detector's number is shared but a quest's number is not. When something
/// happens, it is written to the lifetime stat AND handed to every quest that is
/// open, watching it, and unfinished — each under its own key. Two quests watching
/// one counter get two counters. That is the "own instance of the tracker": no
/// duplicated detectors, no detector that has to know which quests exist.
///
/// Only OBJECTIVES read these. Unlock conditions deliberately still read the
/// lifetime stat — "you have poisoned sixty things, the Serpent has noticed you"
/// is a fact about the player, not about a quest — and the relative ones have
/// their own baselines (see <see cref="UnlockCondition.SinceQuestId"/>).
/// </summary>
public static class QuestMeters
{
    private static Dictionary<string, List<Quest>> _watchers;

    /// <summary>
    /// One quest's private copy of one counter. Prefixed so it is obvious in a save
    /// file and in the stats panel's raw dump which numbers are per-quest.
    /// </summary>
    public static string Key(string questId, string statKey)
    {
        return "q." + questId + "." + statKey;
    }

    /// <summary>
    /// Something was counted. Adds it to each open quest watching this counter.
    ///
    /// Called BEFORE the lifetime stat is written, so that by the time that write
    /// wakes QuestProgress.Evaluate, the per-quest numbers it is about to judge are
    /// already current.
    /// </summary>
    public static void Add(string statKey, int amount)
    {
        if (amount == 0) return;
        var quests = Watching(statKey);
        if (quests == null) return;
        for (int i = 0; i < quests.Count; i++)
        {
            if (!IsOpen(quests[i], statKey)) continue;
            string key = Key(quests[i].Id, statKey);
            PlayerStats.WriteQuestMeter(key, PlayerStats.Get(key) + amount);
        }
    }

    /// <summary>
    /// A record was set — a wave's tally, a run's streak, a depth reached.
    ///
    /// Unconditional, unlike the lifetime Raise it accompanies. That is the whole
    /// point of a private copy: a player whose best wave is already 200 shadow
    /// arrows moves the lifetime record no further, but a quest opened yesterday
    /// still has to watch them do 75 with its own eyes.
    /// </summary>
    public static void Reach(string statKey, int value)
    {
        if (value <= 0) return;
        var quests = Watching(statKey);
        if (quests == null) return;
        for (int i = 0; i < quests.Count; i++)
        {
            if (!IsOpen(quests[i], statKey)) continue;
            string key = Key(quests[i].Id, statKey);
            if (PlayerStats.Get(key) < value) PlayerStats.WriteQuestMeter(key, value);
        }
    }

    /// <summary>
    /// Open for business: INTRODUCED, unfinished, and not measuring this counter
    /// over the player's whole life anyway.
    ///
    /// Introduced, not merely gate-open. A quest starts when its NPC turns up and
    /// asks, so that is when its counters start — a gate that opened at the end of
    /// a wave whose scene has not played yet is not yet a quest anybody has.
    /// </summary>
    private static bool IsOpen(Quest quest, string statKey)
    {
        if (quest == null || !QuestProgress.IsAnnounced(quest.Id)) return false;
        if (QuestProgress.IsCompleted(quest.Id)) return false;
        return !quest.MeasuresLifetime(statKey);
    }

    /// <summary>
    /// statKey to the quests that watch it, built once. The alternative is walking
    /// all forty-eight quests on every kill, and one kill touches several counters.
    /// </summary>
    private static List<Quest> Watching(string statKey)
    {
        if (_watchers == null)
        {
            _watchers = new Dictionary<string, List<Quest>>();
            foreach (var quest in QuestDatabase.All)
            {
                if (quest == null || quest.Objectives == null) continue;
                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    var objective = quest.Objectives[i];
                    if (objective == null || string.IsNullOrEmpty(objective.StatKey)) continue;
                    if (objective.Lifetime) continue;

                    if (!_watchers.TryGetValue(objective.StatKey, out var list))
                    {
                        list = new List<Quest>();
                        _watchers[objective.StatKey] = list;
                    }
                    if (!list.Contains(quest)) list.Add(quest);
                }
            }
        }
        return _watchers.TryGetValue(statKey, out var found) ? found : null;
    }

    /// <summary>Editor-only escape hatch; the index is otherwise built once per process.</summary>
    public static void Invalidate()
    {
        _watchers = null;
    }
}

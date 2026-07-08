using System;
using System.Collections.Generic;
using UnityEngine;

public static class QuestProgress
{
    public static event Action<string> OnQuestCompleted;
    public static event Action<string> OnQuestProgressChanged;

    private static bool _subscribed;

    public static void EnsureInitialized()
    {
        if (_subscribed) return;
        _subscribed = true;
        PlayerStats.OnStatChanged += HandleStatChanged;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            PlayerStats.OnStatChanged -= HandleStatChanged;
            _subscribed = false;
        };
#endif
    }

    public static bool IsCompleted(string questId)
    {
        return FindEntry(questId) != null;
    }

    public static string GetCompletionDate(string questId)
    {
        return FindEntry(questId)?.completedDate;
    }

    public static int GetProgress(Quest quest)
    {
        if (quest == null || !quest.HasObjective) return 0;
        return Mathf.Min(PlayerStats.Get(quest.ObjectiveStatKey), quest.ObjectiveTarget);
    }

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

        if (KnightRankManager.Instance != null)
        {
            KnightRankManager.Instance.AddHonor(quest.HonorReward);
        }
        else
        {
            SaveManager.Data.honorPoints += quest.HonorReward;
            SaveManager.Data.knightRank = KnightRankManager.CalculateRank(SaveManager.Data.honorPoints);
        }

        SaveManager.Save();
        OnQuestCompleted?.Invoke(quest.Id);
        return true;
    }

    public static bool CompleteQuest(string questId)
    {
        return CompleteQuest(QuestDatabase.Get(questId));
    }

    private static void HandleStatChanged(string key, int value)
    {
        foreach (var quest in QuestDatabase.All)
        {
            if (!quest.HasObjective) continue;
            if (quest.ObjectiveStatKey != key) continue;
            OnQuestProgressChanged?.Invoke(quest.Id);
            if (value >= quest.ObjectiveTarget && !IsCompleted(quest.Id))
            {
                CompleteQuest(quest);
            }
        }
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
    }
}

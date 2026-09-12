using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every quest in the game, assembled from the per-line files. Quests stay in C#
/// rather than becoming .asset files because a line is only worth reading as a
/// whole — the chain, its gates and its rewards belong in one diffable block,
/// not scattered across thirty assets.
/// </summary>
public static class QuestDatabase
{
    /// <summary>
    /// Finishing a quest publishes this stat, which is how chains gate on each
    /// other. Keeping prerequisites in the same stat vocabulary as everything
    /// else means there is one unlock mechanism, not two.
    /// </summary>
    public static string CompletionStatKey(string questId)
    {
        return "quests." + questId + ".completed";
    }

    private static List<Quest> _quests;

    public static IReadOnlyList<Quest> All
    {
        get
        {
            if (_quests == null) Build();
            return _quests;
        }
    }

    /// <summary>Map ids in log order. Empty string is the camp group, shown last.</summary>
    public static readonly string[] Groups = { QuestBuild.Forest, QuestBuild.Mine, QuestBuild.Camp };

    public static Quest Get(string id)
    {
        foreach (var q in All)
        {
            if (q.Id == id) return q;
        }
        return null;
    }

    public static IEnumerable<Quest> ForMap(string mapId)
    {
        foreach (var q in All)
        {
            if (q.MapId == mapId) yield return q;
        }
    }

    private static void Build()
    {
        _quests = new List<Quest>();
        _quests.AddRange(ForestQuests.All());
        _quests.AddRange(MineQuests.All());
        _quests.AddRange(SerpentQuests.All());
        _quests.AddRange(EmberQuests.All());
        _quests.AddRange(ShadowQuests.All());
        _quests.AddRange(FrigidQuests.All());
        _quests.AddRange(DawnQuests.All());
        _quests.AddRange(GuardianQuests.All());

        // A duplicated id would silently make one of the two uncompletable —
        // both would resolve to the same save record
        var seen = new HashSet<string>();
        foreach (var q in _quests)
        {
            if (!seen.Add(q.Id))
            {
                Debug.LogError($"[QuestDatabase] Duplicate quest id '{q.Id}'.");
            }
        }
    }
}

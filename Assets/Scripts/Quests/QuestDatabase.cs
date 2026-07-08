using System.Collections.Generic;

public static class QuestDatabase
{
    private static readonly List<Quest> _quests = new()
    {
        new Quest(
            id: "rat_slayer",
            name: "Rat Slayer",
            description: "Rats have overrun the camp stores. Slay 3 rats to earn the camp cook's gratitude.",
            honorReward: 3,
            objectiveStatKey: "kills.rat",
            objectiveTarget: 3
        ),
        new Quest(
            id: "rat_slayer_2",
            name: "Rat Slayer 2",
            description: "The rats have grown bolder. Slay 7 rats total to drive them from the cellars.",
            honorReward: 7,
            objectiveStatKey: "kills.rat",
            objectiveTarget: 7
        ),
    };

    public static IReadOnlyList<Quest> All => _quests;

    public static Quest Get(string id)
    {
        foreach (var q in _quests) if (q.Id == id) return q;
        return null;
    }
}

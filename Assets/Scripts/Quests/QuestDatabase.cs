using System.Collections.Generic;

public static class QuestDatabase
{
    private static readonly List<Quest> _quests = new()
    {
        new Quest(
            id: "rat_slayer",
            name: "Rat Slayer",
            description: "Rats have overrun the camp stores. Slay 10 rats to earn the camp cook's gratitude.",
            honorReward: 3
        ),
        new Quest(
            id: "rat_slayer_2",
            name: "Rat Slayer 2",
            description: "The rats have grown bolder. Slay the giant rat king deep in the cellars.",
            honorReward: 7
        ),
    };

    public static IReadOnlyList<Quest> All => _quests;
}

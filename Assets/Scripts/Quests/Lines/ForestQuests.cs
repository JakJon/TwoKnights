using System.Collections.Generic;
using UnityEngine;
using static QuestBuild;

/// <summary>
/// The Camp Fields. Three quests are open from a new game; beating the Rat King
/// splits the map into further lines that run in parallel.
/// </summary>
public static class ForestQuests
{
    public const string Ruckus = "ruckus_in_the_wood";
    public const string FirstWatch = "first_watch";
    public const string Cleanup1 = "camp_cleanup_1";
    public const string Cleanup2 = "camp_cleanup_2";
    public const string Cleanup3 = "camp_cleanup_3";
    public const string DeepWood = "into_the_deep_wood";
    public const string CrimsonTwins = "the_crimson_twins";
    public const string Ranger = "forest_ranger";
    public const string OneWithTrees = "one_with_the_trees";
    public const string Explorer1 = "forest_explorer_1";
    public const string Explorer2 = "forest_explorer_2";

    // Depth is one measure with one phrasing everywhere: the furthest wave
    // reached on the map, described as venturing into it.
    public const string DepthStat = "maps.camp_fields.furthest_wave";
    public const string DepthLabel = "waves into the forest";

    private static string ExploreStat => WaveExploration.DistinctStatKey(Forest);
    private const string ExploreLabel = "forest wave types met";

    private static UnlockCondition GateCleared => Stat("maps.camp_fields.gate_cleared");

    public static IEnumerable<Quest> All()
    {
        // ---- open from a new game ----

        yield return new Quest(
            id: Ruckus,
            name: "A Ruckus in the Wood",
            description:
                "The animals have been coming out of the treeline wrong — too many of them, too bold, " +
                "and all headed the same direction. The cook thinks it's the weather. The quartermaster " +
                "thinks someone upstream has been dumping something. Neither of them has been far enough " +
                "in for their opinion to be worth much. Go and find what's actually pushing them out.",
            mapId: Forest,
            // The objective never says "wave ten" — the quest is to go and look
            objectives: One("maps.camp_fields.gate_cleared", 1,
                            "Venture further into the forest", hideProgress: true),
            reward: Reward(equipmentId: "gnawed_crown", unlocksMapId: Mine));

        yield return new Quest(
            id: FirstWatch,
            name: "The First Watch",
            description:
                "Nobody expects much of a pair on their first nights. You stand where you're told, you " +
                "keep the shield up, and you learn that the wood makes a particular sound just before " +
                "something comes out of it. Three waves is the traditional measure of whether a new " +
                "pair is worth feeding.",
            mapId: Forest,
            objectives: One(DepthStat, 3, DepthLabel),
            reward: Reward(crystals: 1));

        yield return new Quest(
            id: Cleanup1,
            name: "Camp Cleanup",
            description:
                "The ground between the tents and the treeline used to be pasture. Nothing will graze " +
                "there now, and the vermin have grown comfortable enough to come in after the stores. " +
                "The quartermaster has stopped asking politely and started keeping a tally.",
            mapId: Forest,
            objectives: One("kills.map.camp_fields", 500),
            reward: Reward(crystals: 1));

        yield return new Quest(
            id: Cleanup2,
            name: "Camp Cleanup II",
            description:
                "Clearing them out taught them very little except to come at a different hour. The tally " +
                "board by the mess tent has been wiped down and started again. The quartermaster insists " +
                "this is normal and has not slept properly in a week.",
            mapId: Forest,
            objectives: One("kills.map.camp_fields", 1000),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Cleanup1)));

        yield return new Quest(
            id: Cleanup3,
            name: "Camp Cleanup III",
            description:
                "There's a point where killing things stops being a chore and starts being a border. The " +
                "wood has worked out roughly where the edge is now, and most nights it respects it. " +
                "The quartermaster has taken the board down. He kept the numbers.",
            mapId: Forest,
            objectives: One("kills.map.camp_fields", 2000),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Cleanup2)));

        // ---- the Deep Wood ----

        yield return new Quest(
            id: DeepWood,
            name: "Into the Deep Wood",
            description:
                "Past the treeline the trees stop being spaced like trees and start being spaced like a " +
                "wall. No map in camp goes further than the second creek, and the two scouts who tried " +
                "came back disagreeing about how long they had been gone. Push in and find where the " +
                "ground stops holding.",
            mapId: Forest,
            objectives: One(DepthStat, 15, DepthLabel),
            reward: Reward(crystals: 1),
            unlocks: Gate(GateCleared));

        yield return new Quest(
            id: CrimsonTwins,
            name: "The Crimson Twins",
            description:
                "Whatever the wood has been pushing out, this is what has been doing the pushing. The " +
                "scouts' accounts don't agree on much, but they agree on the colour, and they agree " +
                "there were two of them. Go to the far end of it and settle the question.",
            mapId: Forest,
            objectives: One("maps.camp_fields.true_cleared", 1,
                            "Answer what waits beyond the treeline", hideProgress: true),
            reward: Reward(extraSlot: true),
            unlocks: Gate(GateCleared));

        // ---- bare-handed challenges: no equipment carried, no special fired ----
        //      Gated on the Twins rather than the gate boss: the bare-handed line
        //      is an epilogue to the forest, not a second thing to be doing while
        //      you are still working out how far in the wood goes.

        yield return new Quest(
            id: Ranger,
            name: "Forest Ranger",
            description:
                "The rangers who worked this wood before the camp went up carried a bow, a knife, and " +
                "nothing else worth naming. They also went further in than anyone has managed since. " +
                "The quartermaster thinks the two facts are related and has said so more than once. " +
                "Strip both knights to nothing — no equipment, no special — put the Rat King down, and he will stop saying it.",
            mapId: Forest,
            objectives: One(Feats.RatKingBare, 1,
                            "Defeat the Rat King with no equipment or special equipped",
                            hideProgress: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(CrimsonTwins)));

        yield return new Quest(
            id: OneWithTrees,
            name: "One With the Trees",
            description:
                "Doing it once was a point being made. Doing it at the far end of the wood, against the " +
                "thing the whole map has been arranged around, is something else. No equipment, no " +
                "special, both knights, all the way through. The rangers left no account of trying it.",
            mapId: Forest,
            objectives: One(Feats.TwinsBare, 1,
                            "Defeat the Crimson Twins with no equipment or special equipped",
                            hideProgress: true),
            reward: Reward(crystals: 3),
            unlocks: Gate(After(Ranger)));

        // ---- the Explorer line: opens once the forest has shown you enough
        //      of itself that you would notice it repeating ----

        yield return new Quest(
            id: Explorer1,
            name: "The Long Way Round",
            description:
                "You have stood in the same field enough nights to notice it isn't the same field. What " +
                "the wood sends depends on how deep you have pushed and what you did the last time you " +
                "were out. The scouts never worked this out, which explains the maps. Keep going out, " +
                "and keep count of what comes.",
            mapId: Forest,
            objectives: One(ExploreStat, 32, ExploreLabel),
            reward: Reward(crystals: 1),
            unlocks: Gate(Stat(ExploreStat, 20)));

        yield return new Quest(
            id: Explorer2,
            name: "Every Path in the Wood",
            description:
                "There is nothing left out there that hasn't already come at you at least once. That " +
                "isn't mastery exactly — it's closer to having run out of surprises. The pack in " +
                "particular has stopped treating you as something worth testing, and the two of you " +
                "have started walking a little differently for it.",
            mapId: Forest,
            objectives: One(ExploreStat, FinaleTarget(), ExploreLabel),
            reward: Reward(equipmentId: "wolfsbane_pendant"),
            unlocks: Gate(After(Explorer1)));
    }

    /// <summary>
    /// Every playable wave in the forest. Falls back to an unreachable number if
    /// the catalog cannot be read — a target of zero would hand the finale out
    /// the moment the quest became visible.
    /// </summary>
    private static int FinaleTarget()
    {
        int total = WaveExploration.TotalWaveTypesFor(Forest);
        if (total > 0) return total;
        Debug.LogWarning("[ForestQuests] Wave catalog unreadable; explorer finale left unreachable.");
        return int.MaxValue;
    }
}

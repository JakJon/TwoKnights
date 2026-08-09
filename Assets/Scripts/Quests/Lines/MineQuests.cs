using System.Collections.Generic;
using UnityEngine;
using static QuestBuild;

/// <summary>
/// The Mine. The map opens once the Rat King is down; its explorer line then
/// opens on its own terms, off how much of the workings you have actually walked.
/// </summary>
public static class MineQuests
{
    public const string OffTheRails = "off_the_rails";
    public const string PowderAndPatience = "powder_and_patience";
    public const string Explorer1 = "mine_explorer_1";
    public const string Explorer2 = "mine_explorer_2";

    // No mine quest measures depth right now, but the phrasing is one measure
    // with one wording everywhere — a future one takes these rather than
    // re-deriving the key and drifting from the forest's.
    public const string DepthStat = "maps.mine.furthest_wave";
    public const string DepthLabel = "waves into the mine";

    private static string ExploreStat => WaveExploration.DistinctStatKey(Mine);
    private const string ExploreLabel = "mine wave types met";

    private static UnlockCondition ForestGateCleared => Stat("maps.camp_fields.gate_cleared");

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: OffTheRails,
            name: "Off the Rails",
            description:
                "The carts run all night on a loop that goes somewhere and comes back, and there is " +
                "never anyone driving them. Breaking them is loud, wasteful, and the only thing that " +
                "reliably stops the loop. Nobody has explained who keeps putting them back on the track.",
            mapId: Mine,
            objectives: One("kills.family.cart", 100),
            reward: Reward(crystals: 1),
            unlocks: Gate(ForestGateCleared));

        yield return new Quest(
            id: PowderAndPatience,
            name: "Powder and Patience",
            description:
                "Whoever worked this seam left their powder exactly where it sat, strapped to carts that " +
                "still run. You can shoot around it all night, or you can let the mine do the work it " +
                "was always going to do and make sure you're standing elsewhere when it does.",
            mapId: Mine,
            objectives: One("kills.blasted", 50),
            reward: Reward(crystals: 2),
            unlocks: Gate(ForestGateCleared));

        // ---- the Explorer line ----

        yield return new Quest(
            id: Explorer1,
            name: "Deeper Workings",
            description:
                "The upper galleries are a known quantity now — same rails, same shifts, same places " +
                "the roof drips. Further down the shafts stop following the seam and start following " +
                "something else, and what you meet down there does not work the way the upper crews do. " +
                "Go and see the rest of it.",
            mapId: Mine,
            objectives: One(ExploreStat, 24, ExploreLabel),
            reward: Reward(crystals: 1),
            unlocks: Gate(Stat(ExploreStat, 20)));

        yield return new Quest(
            id: Explorer2,
            name: "Every Shaft Walked",
            description:
                "You have been down every gallery this mine still has open. The survey maps back at camp " +
                "are wrong in nine places and you can name all nine from memory. Whatever else the dark " +
                "below is, it is no longer unknown to you.",
            mapId: Mine,
            objectives: One(ExploreStat, FinaleTarget(), ExploreLabel),
            reward: Reward(crystals: 3),
            unlocks: Gate(After(Explorer1)));
    }

    private static int FinaleTarget()
    {
        int total = WaveExploration.TotalWaveTypesFor(Mine);
        if (total > 0) return total;
        Debug.LogWarning("[MineQuests] Wave catalog unreadable; explorer finale left unreachable.");
        return int.MaxValue;
    }
}

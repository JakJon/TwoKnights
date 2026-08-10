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
    public const string WhatRunsTheCarts = "what_runs_the_carts";
    public const string Explorer1 = "mine_explorer_1";
    public const string Explorer2 = "mine_explorer_2";
    public const string TheGoldCart = "the_gold_cart";

    // No mine quest measures depth right now, but the phrasing is one measure
    // with one wording everywhere — a future one takes these rather than
    // re-deriving the key and drifting from the forest's.
    public const string DepthStat = "maps.mine.furthest_wave";
    public const string DepthLabel = "waves into the mine";

    private static string ExploreStat => WaveExploration.DistinctStatKey(Mine);
    private const string ExploreLabel = "mine wave types met";

    // The mine's own quests open the moment the mine does. Gating them on the
    // forest's gate_cleared said the same thing on a new save and nothing at all
    // on an old one — that stat is written once, on the kill, so a save that had
    // already opened the mine never received it and the line stayed invisible.
    private static UnlockCondition MineOpen => Stat(MapProgressStore.UnlockedStatKey(Mine));

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: WhatRunsTheCarts,
            name: "What Runs the Carts",
            description:
                "Every cart you break is back on the rails the next time you go down, and nobody at camp " +
                "will say who is putting them there. The survey maps stop where the upper galleries stop, " +
                "which the scouts insist is because there is nothing under them worth drawing. Follow the " +
                "track instead of the maps. Whatever is turning the wheel down there is at the end of it.",
            mapId: Mine,
            // The objective never says "wave ten" — the quest is to go and look
            objectives: One("maps.mine.gate_cleared", 1,
                            "Follow the rails to the bottom", hideProgress: true),
            reward: Reward(crystals: 1),
            unlocks: Gate(MineOpen));

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
            unlocks: Gate(MineOpen));

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
            unlocks: Gate(MineOpen));

        // The one quest the mine ends on. It opens the moment the Millstone is
        // down, because beating the wheel is what tells you there was somebody
        // turning it — the quest is the answer to the question the gate asked.
        yield return new Quest(
            id: TheGoldCart,
            name: "The Gold Cart",
            description:
                "Something has been putting the carts back on the rails all along, and the survey maps " +
                "have no word for it. The millstone was only the wheel. Whatever was turning it rides the " +
                "same loop its shifts ride, in a cart nobody who works this seam could afford, and it does " +
                "not get off.",
            mapId: Mine,
            objectives: One("maps.mine.true_cleared", 1,
                            "Put down whatever rides the gold cart", hideProgress: true),
            // A second special slot, not a second equipment slot: the Overseer
            // rode the loop firing whatever it liked, and putting it down is
            // what lets a knight hold two specials and spend one bar on both.
            reward: Reward(extraSpecialSlot: true),
            unlocks: Gate(Stat("maps.mine.gate_cleared")));

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

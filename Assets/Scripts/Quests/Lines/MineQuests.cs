using System.Collections.Generic;
using UnityEngine;
using static QuestBuild;

/// <summary>
/// The Mine. The King opens it and names what is wrong with it; the Cartographer
/// works it. The map's own business — carts, powder, shafts — needs no story
/// permission, so most of it hangs off what the player has actually done down there
/// rather than off the line above it.
/// </summary>
public static class MineQuests
{
    public const string WhatRunsTheCarts = "what_runs_the_carts";
    public const string ClearingOutCarts = "clearing_out_carts";
    public const string PowderAndPatience = "powder_and_patience";
    public const string TheGoldCart = "the_gold_cart";
    public const string Explorer1 = "start_spelunking";
    public const string Explorer2 = "certified_shaft_spelunker";
    public const string OutdoneOverseer = "outdone_overseer";

    public const string DepthStat = "maps.mine.furthest_wave";

    // The first explorer quest asks for a flat 40 wave types (owner, 2026-10-05);
    // the second is still a share of however many exist - see ExplorePercent.
    private static string ExploreStat => WaveExploration.DistinctStatKey(Mine);

    public static IEnumerable<Quest> All()
    {
        // ---- the King's line ----

        yield return new Quest(
            id: WhatRunsTheCarts,
            name: "What Runs the Carts?",
            description:
                "The kingdom's mine has been taken over by gnomes. Explore deeper into the mines, and "
                + "investigate rumors of an overseer that may be orchestrating the chaos.",
            mapId: Mine,
            objectives: One("maps.mine.gate_cleared", 1,
                            menu: "Travel deeper into the mine"),
            reward: Reward(crystals: 1, unlocksMapId: Keep),
            unlocks: Gate(After(ForestQuests.Ruckus), Stat(DepthStat, 3)),
            cast: King,
            offer:
                "Welcome to the mines!\n"
                + "We have had to pause our operations within these caves, since the gnomes have taken hold "
                + "of them. Their overseer is said to be holed up deep within.\n"
                + "Please put an end to this madness so we can once again put the mines to use.",
            completion:
                "<slow>Hmmm...<slow> I thought for certain the overseer would have been in that large "
                + "gathering.\n"
                + "I guess I should have expected he'd be even further in.\n"
                + "<fast>Don't stop looking yet<fast> he has got to be in here.\n"
                + "<exit>\n"
                + "<slow>Oh yes...<slow> There is more trouble in the kingdom...\n"
                + "One of our old keeps has been turned into an enemy foothold.\n"
                + "They are within our own walls.\n"
                + "I know you still have work to be done here, but your help is needed at the pallid keep.");

        yield return new Quest(
            id: TheGoldCart,
            name: "The Gold Cart",
            description:
                "Go deeper into the mines find the overseer, and put an end to his malice.",
            mapId: Mine,
            objectives: One("maps.mine.true_cleared", 1,
                            menu: "Defeat the mine's overseer"),
            reward: Reward(extraSpecialSlot: true),
            unlocks: Gate(Stat("maps.mine.gate_cleared")),
            cast: King,
            offer:
                "",
            completion:
                "<slow>The overseer is dead...<slow> Well done.\n"
                + "We should be able to begin to put these mines back to use slowly\n"
                + "I'm sure it will be some time before the gnomes fully clear out.\n"
                + "I've given you the ability to posses another special equipment slot.\n"
                + "If you have two specials equipped on one of you, when activated, both special effects "
                + "will engage simultaneously.\n"
                + "This should greatly aid you on your journey.");


        // ---- the Cartographer's line ----

        yield return new Quest(
            id: ClearingOutCarts,
            name: "Clearing out Carts",
            description:
                "The abandoned empty mine carts have made navigating the mines troublesome. Destroy empty "
                + "mine carts to clear the cartographers path.",
            mapId: Mine,
            objectives: One(MineStats.EmptyCartsBroken, 300,
                            menu: "{count} empty carts broken",
                            npc: "Destroy {target} empty carts"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(MineStats.EmptyCartsBroken, 10)),
            cast: Mapmaker,
            offer:
                "It seems you too have noticed the abundance of empty mine carts in these caves.\n"
                + "They are a nuisance to navigate!\n"
                + "Please continue to destroy them, and I will ensure I reward you.",
            completion:
                "Wow! It sure does feel more spacious in here with fewer of those empty mine carts "
                + "rolling around!\n"
                + "Here is some pay for your trouble.");

        yield return new Quest(
            id: PowderAndPatience,
            name: "Powder and Patience",
            description:
                "The cartographer has been rigging the carts in the mine with explosives. He wants us to "
                + "put these explosives to use and finish off some creatures with them.",
            mapId: Mine,
            objectives: One("kills.blasted", 150,
                            menu: "{count} slain by blast",
                            npc: "Slay {target} monsters with explosive mine carts"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(MineStats.PowderCartsDetonated, 20)),
            cast: Mapmaker,
            offer:
                "I see you've noticed my explosives.\n"
                + "I've been rigging some of the carts to open new routes within the mines.\n"
                + "It looks like you've found <slow>another<slow> purpose for them.\n"
                + "It has been helping research with less distractions.\n"
                + "If you continue to blow up creatures with the mine cart explosives, I can reward you.",
            completion:
                "Wow! You decimated those monsters with the explosives! I'm going to have to ask the king "
                + "for an expanded pouch... <fast>Anyways!<fast> here is your reward, as promised.");

        yield return new Quest(
            id: Explorer1,
            name: "Start spelunking",
            description:
                "The cartographer has recruited you to scout out the many shafts within the mines. "
                + "Continue to explore different mines shafts.",
            mapId: Mine,
            objectives: One(ExploreStat, 40,
                            menu: "{count} mine waves discovered",
                            npc: "Discover {target} different mine waves",
                            lifetime: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(ExploreStat, ExplorePercent(30))),
            cast: Mapmaker,
            offer:
                "I see you've explored a good amount of shafts by now. Continue to chart your path and "
                + "I'll give you a reward.",
            completion:
                "Wow! You've really charted out these caverns.\n"
                + "We've almost got a full picture of the mine shafts since the gnome take over\n"
                + "Please continue your exploration until the cave is fully understood.");

        yield return new Quest(
            id: Explorer2,
            name: "Certified Shaft Spelunker",
            description:
                "You have almost fully charted every single shaft within the mines. Continue your "
                + "exploration until it is complete.",
            mapId: Mine,
            objectives: One(ExploreStat, ExplorePercent(75),
                            menu: "{count} mine waves discovered",
                            npc: "Discover {target} different mine waves",
                            lifetime: true),
            reward: Reward(crystals: 4),
            unlocks: Gate(After(Explorer1)),
            cast: Mapmaker,
            offer:
                "",
            completion:
                "Amazing! We've gathered a full map covering each shaft within the cave!\n"
                + "I couldn't have done this without your help. Please take this.");

        yield return new Quest(
            id: OutdoneOverseer,
            name: "Outdone Overseer",
            description:
                "The cartographer wants us to defeat an overseer now without any equipment, or specials "
                + "equipped...",
            mapId: Mine,
            objectives: One(Feats.OverseerBare, 1,
                            menu: "Defeat the Overseer with no equipment or special equipped"),
            reward: Reward(equipmentId: "cart_detonator"),
            unlocks: Gate(After(ForestQuests.WreckingRatKing), Stat("kills.overseer", 1)),
            cast: Mapmaker,
            offer:
                "Legend has it the overseer has legions of fellow overseeing brethren.\n"
                + "Slay one of them with nothing equipped.\n"
                + "<fast>For research purposes<fast>",
            completion:
                "WOW! WOW! WO-\n"
                + "<slow>Oh...<slow> I should compose myself.. That was great work.\n"
                + "Please take this, and have some fun with it.");

    }

    /// <summary>See ForestQuests.ExplorePercent — same rule, this map's catalog.</summary>
    private static int ExplorePercent(int percent)
    {
        int total = WaveExploration.TotalWaveTypesFor(Mine);
        if (total > 0) return Mathf.Max(1, Mathf.CeilToInt(total * (percent / 100f)));
        Debug.LogWarning("[MineQuests] Wave catalog unreadable; explorer targets left unreachable.");
        return int.MaxValue;
    }
}

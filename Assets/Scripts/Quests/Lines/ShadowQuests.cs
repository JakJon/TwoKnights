using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Shadow. Both branches measure one wave rather than a lifetime:
/// the Order's whole argument is that volume delivered at once is worth more than
/// the same volume spread thin, so the quests ask for it that way.
/// </summary>
public static class ShadowQuests
{
    public const string Initiation = "shadow_initiation";
    public const string Symphony = "symphony_of_shadows";
    public const string ThousandCuts = "thousand_cuts";
    public const string SlicingSpirals = "slicing_spirals";
    public const string BeastlyBlades = "beastly_blades";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Silent Oath",
            description:
                "Show you know how to aim your shadow arrows in order to be accepted by the Shadow Order. "
                + "Between waves, the Order's ninja turns up at the edge of the field and vanishes again "
                + "moments later. We have to land a shot on him every time he shows himself.",
            mapId: Camp,
            objectives: One("trials.shadow", 3,
                            menu: "{count} shadow trials passed",
                            npc: "Complete the shadow trials"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(Feats.ShadowArrowHits, 100)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "You seem to have no shortage of arrows..\n"
                + "If you show us you know how to use it properly, we may be able to teach you more.",
            completion:
                "You seem to understand the art of shadowing your shot.\n"
                + "We will take you under our wing, and reward you if you prove you're fit.");

        yield return new Quest(
            id: Symphony,
            name: "Symphony of Shadows",
            description:
                "Land 75 shadow arrows in a single wave to prove yourself to the Shadow Order.",
            mapId: Camp,
            objectives: One(OrderStats.ShadowArrowWaveMax, 75,
                            menu: "Best attempt: {count} shadow arrows landed in one wave",
                            npc: "Land {target} shadow arrows in one wave"),
            reward: Reward(equipmentId: "starless_quiver"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "",
            completion:
                "<slow>Your aim is keen.<slow> You know your way with the arrow.\n"
                + "Take this trinket to permanently strengthen your shadows.");

        yield return new Quest(
            id: SlicingSpirals,
            name: "Slicing Spirals",
            description:
                "Land 75 shurikens in a single wave to prove yourself to the Shadow Order.",
            mapId: Camp,
            objectives: One(OrderStats.ShurikenWaveMax, 75,
                            menu: "Best attempt: {count} shurikens landed in one wave",
                            npc: "Land {target} shurikens in one wave"),
            reward: Reward(equipmentId: "nightglass_shard"),
            unlocks: Gate(After(Symphony)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "",
            completion:
                "Great shots.\n"
                + "You have proven yourself worthy to be equipped with so much.");

        yield return new Quest(
            id: BeastlyBlades,
            name: "Beastly Blades",
            description:
                "Show off your skills with your phantom blades by landing both shadow swings in one "
                + "swing. Then get 6 kills with phantom swings in one wave.",
            mapId: Camp,
            objectives: new[]
            {
                Obj(Feats.PhantomFullThree, 1,
                    menu: "Land a sword swing and both its phantom echoes"),
                Obj(OrderStats.PhantomKillsWaveMax, 6,
                    menu: "Best attempt: {count} felled by echoes in one wave",
                    npc: "Get {target} kills with a sword's phantom echo in one wave"),
            },
            reward: Reward(equipmentId: "echo_ribbon"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "",
            completion:
                "Your swing is strong.\n"
                + "Let's see if you're able to master our Order's secret technique.");

        yield return new Quest(
            id: ThousandCuts,
            name: "Thousand Cuts",
            description:
                "The Shadow Order assassin told us he will teach us the order's sacred technique.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.thousand_cuts", 1,
                    menu: "Take up Thousand Cuts",
                    npc: "Obtain thousand cuts upgrade"),
                Obj(OrderStats.ThousandCutsActivations, 20,
                    menu: "{count} Thousand cuts triggers",
                    npc: "Trigger Thousand Cuts {target} times"),
            },
            reward: Reward(crystals: 3, upgradeSlug: "thousand_cuts"),
            unlocks: Gate(After(BeastlyBlades)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "",
            completion:
                "You now understand the true power of the Shadow Order. <slow>You are one with the "
                + "shade.<slow>");

    }
}

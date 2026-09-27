using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Frigid. The argument Shatter puts in front of the player on every
/// frozen body: one branch holds that a stopped enemy is the point, the other that a
/// stopped enemy is a setup. The line lets you finish both, but it makes you notice
/// you chose.
/// </summary>
public static class FrigidQuests
{
    public const string Initiation = "frigid_initiation";
    public const string FrozenInTime = "frozen_in_time";
    public const string BrittleThings = "brittle_things";
    public const string FrozenFinalChances = "frozen_final_chances";
    public const string FrigidFatalities = "frigid_fatalities";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Frosted Oath",
            description:
                "A wizard from the Frost Order is offering to let us undergo their initiation trials. Now "
                + "and then between waves, ice orbs drift slowly along the top and bottom of the field. We "
                + "have to shoot every one of them before it drifts away.",
            mapId: Camp,
            objectives: One("trials.frigid", 3, "frost trials passed"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("frigid.chilled", 60)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Frigid),
            offer:
                "Oh.. It seems that you may know how to handle frost magic.. That is very exciting!\n"
                + "The Frost Order would like to see if you have what it takes to join our ranks!\n"
                + "If you're able to handle these challenges, we will tell you more.\n"
                + "The Frost Order will be sending you challenges from time to time.\n"
                + "Keep an eye out for ice orbs, and be sure to shoot them all if you see any.",
            completion:
                "<fast>Brr! <fast> You have shown us that you understand how to stop enemies in their "
                + "tracks, and are worthy of testing.\n"
                + "<fast>Next we will go into a more specialized frost trials!\n"
                + "<fast> You'll be rewarded with frost charms when you complete trials going forward.");

        yield return new Quest(
            id: FrozenInTime,
            name: "Frozen in Time",
            description:
                "After being initiated into the frosted oath we are tasked to put our freezing abilities "
                + "to use.",
            mapId: Camp,
            objectives: One(OrderStats.FrozenSecondsTotal, 300, "seconds taken from them"),
            reward: Reward(equipmentId: "winters_tooth"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Frigid),
            offer:
                "",
            completion:
                "Hoo. <fast>You stopped those creatures in their tracks!<fast> as a reward for your "
                + "dedication to the fine art of frost we present you this trinket.\n"
                + "As well as, another trial.\n"
                + "Not only can frost freeze, it can fell an enemy too.");

        yield return new Quest(
            id: BrittleThings,
            name: "Brittle Things",
            description:
                "Another trial from the Frost Order. This time we must deal massive damage with shatter "
                + "damage.",
            mapId: Camp,
            objectives: One(OrderStats.ShatterDamageWaveMax, 300, "shatter damage in one wave"),
            reward: Reward(equipmentId: "heart_of_ice"),
            unlocks: Gate(After(FrozenInTime)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Frigid),
            offer:
                "",
            completion:
                "Hoh! Well done! You've shown that not only can you use the Frost Order's abilities well, "
                + "but lethally.\n"
                + "You have earned the title of a frost knight. Great work!");

        yield return new Quest(
            id: FrozenFinalChances,
            name: "Frozen Final Chances",
            description:
                "After our Frost Order initiation we are undergoing a trial to freeze a number of enemies "
                + "that have been chilled by our glacial ward within one run.",
            mapId: Camp,
            objectives: One(OrderStats.WardFrozenRunMax, 30, "caught by the ward in one run"),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Frigid),
            offer:
                "",
            completion:
                "Brr! You just barely managed to stop those creatures in time\n"
                + "You are ready to undergo training with the Frost Order's most pain-inducing spell.");

        yield return new Quest(
            id: FrigidFatalities,
            name: "Frigid Fatalities",
            description:
                "The Frost Order has offered us the chance to learn one of their most lethal frost "
                + "spells.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.permafrost", 1, "Take up Deep Freeze III", hideProgress: true),
                Obj(OrderStats.FrozenTargetDamage, 100, "damage dealt into the ice"),
            },
            reward: Reward(equipmentId: "hoarfrost_band", upgradeSlug: "permafrost"),
            unlocks: Gate(After(FrozenFinalChances)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Frigid),
            offer:
                "",
            completion:
                "Great job. You've now witnessed all that is capable within the Frost Order.");

    }
}

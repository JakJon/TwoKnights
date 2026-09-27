using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Serpent. A light trigger reveals the initiation, the initiation
/// admits you, and admission opens both branches at once — the Fang, which is about
/// how hard one dose can bite, and the Coil, which is about how many can carry it.
/// </summary>
public static class SerpentQuests
{
    public const string Initiation = "serpent_initiation";
    public const string DeadlyAroma = "deadly_aroma";
    public const string AcidicAccumulation = "acidic_accumulation";
    public const string SlowWork = "the_slow_work";
    public const string DaggerOfDeath = "dagger_of_death";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Green Oath",
            description:
                "An assassin took note of our work. If we continue using poison, we will see what comes "
                + "of it. Between waves, the Serpent Order sends poison orbs looping across the field. "
                + "Each one has to be struck once, and only once, before it slips away for good.",
            mapId: Camp,
            objectives: One("trials.serpent", 3, "venom trials passed"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("applied.poison", 60)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Serpent),
            offer:
                "The Serpent Order has noticed your poison techniques.\n"
                + "We can teach you more...\n"
                + "The Serpent Order is challenging your efficiency. After an enemy is poisoned, you do "
                + "not need to strike it again.\n"
                + "Same with these poison orbs. Do not shoot a poison orb more than once.\n"
                + "But ensure each is shot, before it is too late.\n"
                + "We'll be watching.",
            completion:
                "You've shown yourself worthy of testing..\n"
                + "Finish these next trials, and we will reward you with specialized equipment.");

        yield return new Quest(
            id: DeadlyAroma,
            name: "Deadly Aroma",
            description:
                "We're challenged by the Serpent Order to have 11 creatures poisoned simultaneously.",
            mapId: Camp,
            objectives: One(OrderStats.PoisonSimultaneousMax, 11, "at once, carrying venom"),
            reward: Reward(equipmentId: "serpents_eye"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Serpent),
            offer:
                "",
            completion:
                "<slow>Impressive.<slow> You've shown you can poison many at once. But can you poison one "
                + "target relentlessly?");

        yield return new Quest(
            id: AcidicAccumulation,
            name: "Acidic Accumulation",
            description:
                "We're challenged by the Serpent Order to deal 100 poison damage at once.",
            mapId: Camp,
            objectives: One(OrderStats.PoisonTickMax, 100, "damage from one tick of venom"),
            reward: Reward(equipmentId: "fangbone_charm"),
            unlocks: Gate(After(DeadlyAroma)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Serpent),
            offer:
                "",
            completion:
                "Your mastery is apparent. You've earned this reward. You are now one of us.");

        yield return new Quest(
            id: SlowWork,
            name: "The slow work",
            description:
                "We're being challenged by the Serpent Order to slay 300 enemies using poison.",
            mapId: Camp,
            objectives: One("kills.poisoned", 300, "slain by venom"),
            reward: Reward(equipmentId: "hollow_fang"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Serpent),
            offer:
                "",
            completion:
                "Good.. It seems you may know what you are doing.\n"
                + "You may be able to handle a second blade in your offhand.\n"
                + "Give this new weapon a try.");

        yield return new Quest(
            id: DaggerOfDeath,
            name: "Dagger of Death",
            description:
                "Obtain one of the Serpent Order's most lethal abilities.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.acid_dagger", 1, "Take up the Acid Dagger", hideProgress: true),
                Obj(OrderStats.AcidDaggerKills, 5, "felled by the dagger"),
            },
            reward: Reward(crystals: 2, upgradeSlug: "acid_dagger"),
            unlocks: Gate(After(SlowWork)),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Serpent),
            offer:
                "",
            completion:
                "Well done. You've shown you know what it means to belong to the Serpent Order.");

    }
}

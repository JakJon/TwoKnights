using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Dawn. Every other Order measures a knight by what they killed;
/// this one measures you by what reached the person beside you, and it is the only
/// tally where sending more of it away is the better score.
///
/// Its door opens either way round — on light shared across, or on health mended by
/// Lifebloom — because a knight who went down the mending branch practised the same
/// thing by a different route. This is the only Any-of gate in the game.
/// </summary>
public static class DawnQuests
{
    public const string Initiation = "dawn_initiation";
    public const string FirstAidFrenzy = "first_aid_frenzy";
    public const string NotAScratch = "not_a_scratch";
    public const string RegainedComposure = "regained_composure";
    public const string LastLight = "the_last_light";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The protected order",
            description:
                "A paladin of the Dawn Order has shown up and challenged us to prove our abilities with "
                + "healing spells. His trial throws rocks at both of us at once, each of us the mirror of "
                + "the other, and neither of us can be hit even once.",
            mapId: Camp,
            objectives: One("trials.dawn", 1, "Pass the Paladin's trial", hideProgress: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("dawn.shared_light", 40),
                          Stat(OrderStats.LifebloomHealing, 20)),
            unlockMode: UnlockMode.Any,
            cast: Cast(NpcId.Paladin, UpgradeOrder.Dawn),
            offer:
                "You must be intrigued by the powers of the Dawn Order.\n"
                + "We can teach you healing abilities far greater than you've seen before.\n"
                + "First you must show us you're able.",
            completion:
                "We will take you under our wing.\n"
                + "Your healing spells are admirable, but have room for growth still.\n"
                + "Take on these trials, and your light will grow stronger.");

        yield return new Quest(
            id: FirstAidFrenzy,
            name: "First aid frenzy",
            description:
                "Another trial from the Dawn paladin. We must recover plenty of health within a single "
                + "wave.",
            mapId: Camp,
            objectives: One(OrderStats.HealedInWaveMax, 150, "health recovered in one wave"),
            reward: Reward(equipmentId: "warm_lantern"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Dawn),
            offer:
                "",
            completion:
                "Nicely done, you really are putting your healing spells to use.\n"
                + "Let's see if you can take them even further.");

        yield return new Quest(
            id: NotAScratch,
            name: "Not a scratch",
            description:
                "The Dawn paladin has asked us to take another trial, surviving waves of creatures at max "
                + "health. (Note you can take damage, just not end the wave below max health.)",
            mapId: Camp,
            objectives: One(OrderStats.FlawlessWaveStreakMax, 10,
                            "waves in a row ended with both knights whole"),
            reward: Reward(equipmentId: "oathbound_locket"),
            unlocks: Gate(After(FirstAidFrenzy)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Dawn),
            offer:
                "",
            completion:
                "The Dawn Order is pleased with your progress.\n"
                + "We hope you cherish this reward, and continue to be a light to those around you.");

        yield return new Quest(
            id: RegainedComposure,
            name: "Regained composure",
            description:
                "We must recover our health after having it fall low. (WRITE THE EXACT SPECIFIC NUMBERS "
                + "SOMEWHERE IN THIS QUEST)",
            mapId: Camp,
            objectives: One(Feats.DawnPairPulledBack, 1,
                            "Finish a wave with both knights at full after one was driven low",
                            hideProgress: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Dawn),
            offer:
                "",
            completion:
                "Very good.\n"
                + "Your light shines bright, but it may shine brighter yet with further training\n"
                + "We will now teach you the redeeming power of light.");

        yield return new Quest(
            id: LastLight,
            name: "The Last Light",
            description:
                "We must learn the Dawn Order's sacred healing spell and put it to use.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.last_light", 1, "Take up Last Light", hideProgress: true),
                Obj(OrderStats.LastLightActivations, 1,
                    "Let it catch the other knight once", hideProgress: true),
            },
            reward: Reward(equipmentId: "dawnbreak_crown", upgradeSlug: "last_light"),
            unlocks: Gate(After(RegainedComposure)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Dawn),
            offer:
                "",
            completion:
                "You are a light in darkness.\n"
                + "You have shown to be worthy of the Dawn Order's charms\n"
                + "Please wear this crown with dignity.");

    }
}

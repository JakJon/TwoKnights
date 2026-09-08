using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Dawn Order. Same shape as the Serpent, the Ember and the Frigid: a light
/// trigger reveals the initiation, the initiation admits you, admission opens the
/// two branches.
///
/// Dawn's branches are not a disagreement about tactics the way Frigid's are.
/// They are a disagreement about WHO the light is for. Vigil says it is for the
/// other knight. Wellspring says you cannot pour from an empty cup. The Order has
/// been having this argument for a long time and is not close to settling it.
/// </summary>
public static class DawnQuests
{
    public const string Initiation = "dawn_initiation";
    public const string Vigil1 = "dawn_vigil_1";
    public const string Vigil2 = "dawn_vigil_2";
    public const string Wellspring1 = "dawn_wellspring_1";
    public const string Wellspring2 = "dawn_wellspring_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Kept Watch",
            description:
                "The Order of the Dawn holds that there is no such thing as a knight who survived a " +
                "night, only a pair who did. They are aware this is a technicality on most evenings. " +
                "They are also aware that on the evenings it is not a technicality it is the only " +
                "thing that mattered, and they would like to see you act as though you knew which " +
                "evening you were having.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("dawn.shared_light", 100, "heals passed to the other knight"),
                Obj("dawn.lifebloom", 50, "kills that healed"),
                Obj("upgrades.order.dawn", 5, "dawn upgrades taken"),
            },
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("dawn.shared_light", 40)));

        // ---- Vigil: the light you give away ----

        yield return new Quest(
            id: Vigil1,
            name: "The Longer Half",
            description:
                "Every Order in the field measures a knight by what they killed. This one measures you " +
                "by what reached the person beside you, and it is the only tally where sending more of " +
                "it away is the better score. Some recruits take a while with this.",
            mapId: Camp,
            objectives: One("dawn.shared_light", 300, "heals passed to the other knight"),
            reward: Reward(equipmentId: "warm_lantern"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Vigil2,
            name: "Both Of You, Standing",
            description:
                "The rite is not a demonstration of healing. It is a demonstration of a night going " +
                "badly and then not going badly, which are two different skills and the Order only " +
                "cares about the second one. Let it get close. Then do not let it finish.",
            mapId: Camp,
            objectives: One(Feats.DawnPairPulledBack, 1,
                            "Finish a wave with both knights at full after one was driven low",
                            hideProgress: true),
            reward: Reward(equipmentId: "oathbound_locket"),
            unlocks: Gate(After(Vigil1)));

        // ---- Wellspring: the light you find ----

        yield return new Quest(
            id: Wellspring1,
            name: "Draw From The Well",
            description:
                "The other branch is unromantic about it. Light has to come from somewhere, it never " +
                "arrives on a timer, and a knight who gave away more than they gathered is a knight " +
                "who has arranged for two people to fall over instead of one.",
            mapId: Camp,
            objectives: One("dawn.lifebloom", 200, "kills that healed"),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Wellspring2,
            name: "The Last Light",
            description:
                "The Order's final rite is the one thing both branches agree on, which is why it is " +
                "kept for the end. Once a map, the other knight simply does not fall. It insures the " +
                "person beside you and never you, and every initiate asks about that exactly once.",
            mapId: Camp,
            objectives: One("upgrades.taken.last_light", 1,
                            "Acquire Last Light, the Dawn capstone", hideProgress: true),
            reward: Reward(equipmentId: "dawnbreak_crown"),
            unlocks: Gate(After(Wellspring1)));
    }
}

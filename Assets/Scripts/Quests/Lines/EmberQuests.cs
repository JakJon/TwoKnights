using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Ember Order. Same shape as the Serpent: a light trigger reveals the
/// initiation, the initiation admits you, admission opens Brand and Pyre.
/// </summary>
public static class EmberQuests
{
    public const string Initiation = "ember_initiation";
    public const string Brand1 = "ember_brand_1";
    public const string Brand2 = "ember_brand_2";
    public const string Pyre1 = "ember_pyre_1";
    public const string Pyre2 = "ember_pyre_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Ashen Oath",
            description:
                "Anyone can start a fire. The Order of the Ember is interested in the considerably " +
                "rarer skill of keeping one — setting something alight and then not needing to do " +
                "anything further about it. They will want to see it more than once, on different " +
                "nights, before anyone says a word to you.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("applied.ignite", 100, "enemies set alight"),
                Obj("kills.burned", 50, "slain by fire"),
                Obj("upgrades.order.ember", 5, "ember upgrades taken"),
            },
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("applied.ignite", 60)));

        // ---- Brand: the fire you carry ----

        yield return new Quest(
            id: Brand1,
            name: "Stoke the Fire",
            description:
                "A flame that only frightens is a flame you are wasting. The Order's standing complaint " +
                "about most people who carry fire is that they use it as a slightly worse arrow. Make " +
                "it do the killing on its own and stop supervising.",
            mapId: Camp,
            objectives: One("kills.burned", 200, "slain by fire"),
            reward: Reward(equipmentId: "emberbrand"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Brand2,
            name: "Scorched Ground",
            description:
                "There is nothing coming out of that treeline anymore that has not already burned once. " +
                "Camp has noticed the smell and stopped commenting on it. The Order has noticed the " +
                "count and has not stopped commenting at all.",
            mapId: Camp,
            objectives: One("kills.burned", 500, "slain by fire"),
            reward: Reward(equipmentId: "everburning_coal"),
            unlocks: Gate(After(Brand1)));

        // ---- Pyre: the fire you leave ----

        yield return new Quest(
            id: Pyre1,
            name: "Lay the Pyre",
            description:
                "The other branch cares less about what you hit and more about where it walks " +
                "afterwards. Ground that remembers the fire does half the night's work for you, and it " +
                "does that work while you are facing entirely the wrong way.",
            mapId: Camp,
            objectives: One(Feats.FireTrailsFour, 1,
                            "Have four fire trails burning at once", hideProgress: true),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Pyre2,
            name: "Salt the Earth",
            description:
                "The Order's final rite is an admission: some fires are not meant to go out. What you " +
                "light stays lit, and the field stops being ground you defend and becomes ground they " +
                "have to cross.",
            mapId: Camp,
            objectives: One("upgrades.taken.scorched_earth", 1,
                            "Acquire Scorched Earth, the Ember capstone", hideProgress: true),
            reward: Reward(equipmentId: "cinder_crown"),
            unlocks: Gate(After(Pyre1)));
    }
}

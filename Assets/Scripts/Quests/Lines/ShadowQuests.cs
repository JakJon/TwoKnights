using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Shadow Order. Same shape as the Serpent and the Ember: a light trigger
/// reveals the initiation, the initiation admits you, admission opens Blade and
/// Fan. The trigger counts landed shadow arrows, so it measures the practice
/// rather than the purchase.
/// </summary>
public static class ShadowQuests
{
    public const string Initiation = "shadow_initiation";
    public const string Blade1 = "shadow_blade_1";
    public const string Blade2 = "shadow_blade_2";
    public const string Fan1 = "shadow_fan_1";
    public const string Fan2 = "shadow_fan_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Silent Oath",
            description:
                "The Order of the Shadow has no interest in how loudly you can kill. What gets their " +
                "attention is economy — the wounded thing finished before it can turn around, the shot " +
                "that did not need a second. Show enough of it and the invitation arrives without " +
                "ceremony, usually folded into something you were already carrying.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.order.shadow", 6, "shadow upgrades taken"),
                Obj("kills.executed", 25, "finished outright"),
            },
            reward: Reward(crystals: 2),
            // Noticed for practising the Order, not for owning it: sixty arrows
            // that connected, the same counter the Blade branch then asks a
            // thousand of. Drafting Shadow Arrow was the old trigger and it fired
            // on the pick rather than on any use of it.
            unlocks: Gate(Stat(Feats.ShadowArrowHits, 60)));

        // ---- Blade: finish what is already failing ----

        yield return new Quest(
            id: Blade1,
            name: "The Quiet End",
            description:
                "Wounded things take considerably longer to die than they need to, and every second of " +
                "it is a second you are not aiming somewhere more useful. The Order regards a slow " +
                "finish as a form of rudeness, mostly toward yourself.",
            mapId: Camp,
            objectives: One(Feats.ShadowArrowHits, 1000, "shadow arrows landed"),
            reward: Reward(equipmentId: "nightglass_shard"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Blade2,
            name: "Thousand Cuts",
            description:
                "The last lesson of the blade branch is that one blade was always a compromise you " +
                "agreed to for no particular reason. Take up the rite and stop agreeing to it.",
            mapId: Camp,
            objectives: One("upgrades.taken.thousand_cuts", 1,
                            "Acquire Thousand Cuts, the Shadow capstone", hideProgress: true),
            reward: Reward(equipmentId: "starless_quiver"),
            unlocks: Gate(After(Blade1)));

        // ---- Fan: fill the air instead ----

        yield return new Quest(
            id: Fan1,
            name: "A Hundred Edges",
            description:
                "The other branch gave up on aim as a luxury some time ago. Coverage is cheaper, far " +
                "more forgiving, and considerably harder to walk through. Widen the fan and stop being " +
                "precious about where each one lands.",
            mapId: Camp,
            objectives: One(Feats.ShurikenVolleyFour, 1,
                            "Land four shurikens from a single volley", hideProgress: true),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Fan2,
            name: "Phantom Blade",
            description:
                "Swing once and let the dark swing after you. The Order has declined to explain how " +
                "this works and has politely asked that you stop asking.",
            mapId: Camp,
            objectives: One(Feats.PhantomFullThree, 1,
                            "Land a swing and both its phantom echoes", hideProgress: true),
            reward: Reward(equipmentId: "echo_ribbon"),
            unlocks: Gate(After(Fan1)));
    }
}

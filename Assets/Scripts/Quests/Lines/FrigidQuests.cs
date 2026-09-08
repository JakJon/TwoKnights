using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Frigid Order. Same shape as the Serpent and the Ember: a light trigger
/// reveals the initiation, the initiation admits you, admission opens the two
/// branches the Order actually argues about.
///
/// The argument is the one Shatter puts in front of the player on every frozen
/// body — Rime says a stopped enemy is the point, Silence says a stopped enemy is
/// a setup. The line lets you finish both, but it makes you notice you chose.
/// </summary>
public static class FrigidQuests
{
    public const string Initiation = "frigid_initiation";
    public const string Rime1 = "frigid_rime_1";
    public const string Rime2 = "frigid_rime_2";
    public const string Silence1 = "frigid_silence_1";
    public const string Silence2 = "frigid_silence_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Held Breath",
            description:
                "The Order of the Frigid does not kill anything. This is stated plainly at the door, " +
                "usually to someone who has arrived expecting otherwise. What they teach is how to " +
                "take an evening away from something that was in a hurry, and they will want to watch " +
                "you do it a great many times before they accept that you understood the distinction.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("frigid.chilled", 100, "enemies chilled"),
                Obj("frigid.frozen", 50, "enemies frozen"),
                Obj("upgrades.order.frigid", 5, "frigid upgrades taken"),
            },
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("frigid.chilled", 60)));

        // ---- Rime: the cold that holds ----

        yield return new Quest(
            id: Rime1,
            name: "Take Their Evening",
            description:
                "Anyone can make a wolf slower. The Order's interest begins at the point where the wolf " +
                "stops entirely and has to be walked around. Do that until it is unremarkable, then " +
                "come back and they will find something else to be unimpressed by.",
            mapId: Camp,
            objectives: One("frigid.frozen", 200, "enemies frozen"),
            reward: Reward(equipmentId: "hoarfrost_band"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Rime2,
            name: "The Standing Field",
            description:
                "Four at once is the number the Order uses to settle arguments. Below it you are " +
                "answering things one at a time, which is a skill but not this one. At four the field " +
                "stops being a fight and starts being a room you are tidying at your own pace.",
            mapId: Camp,
            objectives: One(Feats.FrozenFour, 1,
                            "Hold four enemies in ice at once", hideProgress: true),
            reward: Reward(equipmentId: "winters_tooth"),
            unlocks: Gate(After(Rime1)));

        // ---- Silence: the cold that breaks ----

        yield return new Quest(
            id: Silence1,
            name: "Brittle Things",
            description:
                "The other branch thinks holding is only half of it. Cold makes a body brittle, and a " +
                "brittle body answers an arrow very differently to a warm one. Their complaint about " +
                "the Rime branch is that it leaves the work undone and calls it mercy.",
            mapId: Camp,
            objectives: One("frigid.shattered", 100, "enemies shattered"),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Silence2,
            name: "Nothing Moves",
            description:
                "The Order's final rite is the admission the whole thing was always heading toward: " +
                "some ice is not meant to melt. What you stop stays stopped, and the field stops being " +
                "ground you defend and becomes a row of things waiting their turn.",
            mapId: Camp,
            objectives: One("upgrades.taken.permafrost", 1,
                            "Acquire Permafrost, the Frigid capstone", hideProgress: true),
            reward: Reward(equipmentId: "heart_of_ice"),
            unlocks: Gate(After(Silence1)));
    }
}

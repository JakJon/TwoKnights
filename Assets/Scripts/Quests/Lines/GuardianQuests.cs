using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Guardian Order. Same shape as the others: a light trigger reveals the
/// initiation, the initiation admits you, admission opens two branches at once.
///
/// The argument is the Order's own pillar turned against itself. Guardian does not
/// buy a bigger number, it buys the LANDING — and the two halves disagree about
/// which landing is the one that matters. The Wall says it is what arrives and
/// gets stopped. The Line says it is what you send and where it ends up. Both are
/// the same sentence read from opposite ends, which is why the line lets you finish
/// both and why the last quest in the Line branch cannot be done without the Wall.
/// </summary>
public static class GuardianQuests
{
    public const string Initiation = "guardian_initiation";
    public const string Wall1 = "guardian_wall_1";
    public const string Wall2 = "guardian_wall_2";
    public const string Line1 = "guardian_line_1";
    public const string Line2 = "guardian_line_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Standing Order",
            description:
                "The Order keeps no barracks and recruits nobody. Their position is that a thing " +
                "arriving and a thing landing are one problem looked at from either end, and that " +
                "anyone who has worked that out has already joined whether or not they were asked. " +
                "The quartermaster finds them insufferable and has never once been able to say why.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("guardian.reflected", 60, "rocks sent back"),
                Obj("guardian.guided", 100, "shots guided"),
                Obj("upgrades.order.guardian", 5, "guardian upgrades taken"),
            },
            reward: Reward(crystals: 2),
            // The one reveal in the game that asks about a RUN rather than a
            // lifetime: two Guardian picks on one knight, and that knight then doing
            // something with them. See GuardianAwakening.
            unlocks: Gate(Stat(Feats.GuardianAwoken, 1)));

        // ---- The Wall: what arrives ----

        yield return new Quest(
            id: Wall1,
            name: "Nothing Gets Through",
            description:
                "The shafts have been throwing the same stone at this camp for as long as anyone " +
                "has been counting, and the Order's whole answer is that it is a perfectly good " +
                "stone. Send enough of them back and the argument stops being philosophical. The " +
                "gnomes have not adjusted their aim, which the Order takes as a compliment.",
            mapId: Camp,
            objectives: One("guardian.reflected", 250, "rocks sent back"),
            reward: Reward(equipmentId: "returned_stone"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Wall2,
            name: "The Immovable Watch",
            description:
                "Every knight before you bought a body's removal with their own health and called " +
                "it a fair trade. The Order's last teaching is that it was never a trade at all, " +
                "and that anything which reaches the guard can simply be told to go back the way " +
                "it came.",
            mapId: Camp,
            objectives: One("upgrades.taken.bulwark", 1,
                            "Acquire Bulwark, the Guardian capstone", hideProgress: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Wall1)));

        // ---- The Line: what you send ----

        yield return new Quest(
            id: Line1,
            name: "It Finds Them",
            description:
                "The other half of the Order is less interested in the wall and more interested in " +
                "the beam, and considers the Wall branch a very elaborate way of standing still. " +
                "Their position is that an arrow which needed aiming was a badly made arrow. They " +
                "are difficult to argue with and worse to drink with.",
            mapId: Camp,
            objectives: One("guardian.guided", 300, "shots guided"),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Line2,
            name: "Their Own Powder",
            description:
                "This is the rite the two branches finally agree on, largely because neither can " +
                "perform it alone. A powder rock caught on the guard, turned, steered into company " +
                "and allowed to finish what the mine started. The Order regards it as the plainest " +
                "possible statement of what they have been saying the whole time.",
            mapId: Camp,
            objectives: One(Feats.ReflectThree, 1,
                            "Catch three at once with one returned powder rock", hideProgress: true),
            reward: Reward(equipmentId: "worn_baldric"),
            unlocks: Gate(After(Line1)));
    }
}

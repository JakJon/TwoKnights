using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Serpent Order. Poisoning a handful of enemies is enough to be noticed —
/// it reveals the initiation and nothing else. The initiation is what admits
/// you, and admission opens both branches at once.
/// </summary>
public static class SerpentQuests
{
    public const string Initiation = "serpent_initiation";
    public const string Fang1 = "serpent_fang_1";
    public const string Fang2 = "serpent_fang_2";
    public const string Coil1 = "serpent_coil_1";
    public const string Coil2 = "serpent_coil_2";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Green Oath",
            description:
                "The Order of the Serpent does not recruit and does not advertise. What it does is " +
                "notice, eventually, when someone has been using poison properly — not as a finisher " +
                "but as the whole plan, letting the work happen while you stand somewhere safe. Do " +
                "enough of it and someone will find you. There is no ceremony. There is a small green " +
                "mark on your kit that you did not put there.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("applied.poison", 100, "enemies poisoned"),
                Obj("kills.poisoned", 50, "slain by venom"),
                Obj("upgrades.order.serpent", 5, "serpent upgrades taken"),
            },
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("applied.poison", 60)));

        // ---- Fang: make the venom bite harder ----

        yield return new Quest(
            id: Fang1,
            name: "The Slow Work",
            description:
                "The first thing they teach is that you are not in a hurry. A killed thing and a dying " +
                "thing are worth the same by morning, and the dying one cost you one arrow instead of " +
                "four. Go and be unhurried about it, at length, until it stops feeling like waiting.",
            mapId: Camp,
            objectives: One("kills.poisoned", 200, "slain by venom"),
            reward: Reward(equipmentId: "fangbone_charm"),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Fang2,
            name: "The Long Coil",
            description:
                "By now the wood knows how you taste and comes anyway, which the Order regards as the " +
                "actual result. Nothing out there has learned to avoid you. They have simply worked out " +
                "that there is no avoiding it, and adjusted their expectations accordingly.",
            mapId: Camp,
            objectives: One("kills.poisoned", 500, "slain by venom"),
            reward: Reward(equipmentId: "serpents_eye"),
            unlocks: Gate(After(Fang1)));

        // ---- Coil: make the venom spread ----

        yield return new Quest(
            id: Coil1,
            name: "A Cloud That Lingers",
            description:
                "The second branch is less about the arrow and more about the air. What one of them " +
                "carries, the next one breathes, and you were never required to be present for the " +
                "second part. Learn to leave something behind you.",
            mapId: Camp,
            objectives: One(Feats.PoisonCloudFour, 1,
                            "Poison four enemies with a single cloud", hideProgress: true),
            reward: Reward(crystals: 1),
            unlocks: Gate(After(Initiation)));

        yield return new Quest(
            id: Coil2,
            name: "What the Dead Carry",
            description:
                "The Order's last lesson is the one they leave out of the written rites: a corpse is " +
                "not the end of a job, it is a delivery. Take up the rite and the dead start working " +
                "the shift after yours.",
            mapId: Camp,
            objectives: One("upgrades.taken.plaguebringer", 1,
                            "Acquire Plaguebringer, the Serpent capstone", hideProgress: true),
            reward: Reward(equipmentId: "hollow_fang"),
            unlocks: Gate(After(Coil1)));
    }
}

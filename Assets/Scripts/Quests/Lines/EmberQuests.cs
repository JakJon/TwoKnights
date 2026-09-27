using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Ember. One branch is about spread — how much ground you can have
/// alight at once — and the other about persistence, keeping one thing burning
/// without ever letting it go out. Anyone can start a fire; the Order is interested
/// in the rarer skill of not having to start it again.
/// </summary>
public static class EmberQuests
{
    public const string Initiation = "ember_initiation";
    public const string UpInSmoke = "up_in_smoke";
    public const string SustainedSizzle = "sustained_sizzle";
    public const string SuperbSpellcasting = "superb_spellcasting";
    public const string Permanence = "permanence_of_pyromancy";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Ashen Oath",
            description:
                "A wizard of the Fire Order revealed himself, and offered to enroll us if we can pass his "
                + "initiation trials. Every so often between waves, the Order sends fire orbs racing along "
                + "the edges of the field. Every one of them has to be shot down before it gets away.",
            mapId: Camp,
            objectives: One("trials.ember", 3, "fire trials passed"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat("applied.ignite", 60)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Ember),
            offer:
                "Oh oh. I must say, you sure seem to know how to heat things up!\n"
                + "<fast>Your fire abilities show true potential!<fast>\n"
                + "The Ember Order could benefit from another member.\n"
                + "If you show you are serious in your pyromancy, we can train you knightly skills to not "
                + "only bash, but also <slow>burn.<slow>\n"
                + "The Fire Order will be sending fire orbs your way to test you.\n"
                + "Shoot all the fire orbs in order to prove your skill!",
            completion:
                "Yes! That was great!\n"
                + "I knew that you were capable of not only setting creatures ablaze, but reducing them to "
                + "ashes.\n"
                + "<fast>You have shown yourself worthy of more challenging tasks! <fast>\n"
                + "If you complete more of our trials you will be rewarded with fire enhancing equipment "
                + "and techniques. . Burn Brightly.");

        yield return new Quest(
            id: UpInSmoke,
            name: "Up in smoke",
            description:
                "One of our first Ember initiation trials is to cover the field in fire.",
            mapId: Camp,
            objectives: One(OrderStats.FireCoveragePercentMax, 25, "percent of the field alight at once"),
            reward: Reward(equipmentId: "everburning_coal"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Ember),
            offer:
                "",
            completion:
                "<fast>Ho ho! Wow!<fast> It sure got hot in here.\n"
                + "That was an exquisitely large fire you created just now!\n"
                + "Take this as a token of recognition.\n"
                + "Let's see if you can handle an even more challenging feat of fire!\n"
                + "Burn Brightly.");

        yield return new Quest(
            id: SustainedSizzle,
            name: "Sustained Sizzle",
            description:
                "We have been challenged with one of the Ember Order's most challenging trials. We must "
                + "sustain our flame on a boss mob while dealing massive damage.",
            mapId: Camp,
            objectives: One(OrderStats.BossBurnUninterruptedMax, 1200,
                            "damage burned into one boss without the fire lapsing"),
            reward: Reward(equipmentId: "emberbrand"),
            unlocks: Gate(After(UpInSmoke)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Ember),
            offer:
                "",
            completion:
                "<fast>Oooh, hoh wow! That was thrilling<fast>!\n"
                + "You've shown without a doubt that you can sustain your flame.\n"
                + "<slow> You are one with the order of flame. <slow> Burn brightly.");

        yield return new Quest(
            id: SuperbSpellcasting,
            name: "Superb Spellcasting",
            description:
                "One of our first Ember initiation trials is to master our spell casting. We must show "
                + "off our accuracy with fireball casts.",
            mapId: Camp,
            objectives: One(OrderStats.FireballRunMax, 150, "fireballs landed in one run"),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Ember),
            offer:
                "",
            completion:
                "<fast>Hoo!<fast> You've proven to understand the fundamentals of spellcasting\n"
                + "We believe that you will be capable to handle this stronger fire spell. Burn brightly.");

        yield return new Quest(
            id: Permanence,
            name: "The Permanence of Pyromancy",
            description:
                "The Ember Order has agreed to teach us one of its sacred fire spells, Scorched Earth. "
                + "To prove we have learned it, we must take it up in battle and keep a single fire "
                + "burning without ever letting it go out.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.scorched_earth", 1, "Take up Scorched Earth", hideProgress: true),
                Obj(OrderStats.FireFieldSecondsMax, 60, "seconds of one unbroken fire"),
            },
            reward: Reward(equipmentId: "cinder_crown", upgradeSlug: "scorched_earth"),
            unlocks: Gate(After(SuperbSpellcasting)),
            cast: Cast(NpcId.Wizard, UpgradeOrder.Ember),
            offer:
                "",
            completion:
                "You now will witness the true power of fire.\n"
                + "Try not to burn down the entire kingdom! Burn brightly!");

    }
}

using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The Order of the Guardian. A thing arriving and a thing landing are one problem
/// looked at from either end — the Wall branch answers the first, the Line branch the
/// second, and the Order maintains this was always obvious.
/// </summary>
public static class GuardianQuests
{
    public const string Initiation = "guardian_initiation";
    public const string WhatsYoursIsMine = "whats_yours_is_mine";
    public const string ShieldSlammer = "shield_slammer";
    public const string AceShooter = "ace_shooter";
    public const string SafeDistance = "a_safe_distance";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: Initiation,
            name: "Initiation: The Standing Order",
            description:
                "A paladin of the Guardian Order has noticed our skills. We must utilize their abilities "
                + "to learn more. When he decides we are ready, he will test each of us in turn with a "
                + "storm of rocks, and neither of us can be hit even once.",
            mapId: Camp,
            objectives: One("trials.guardian", 1,
                            menu: "Pass the Paladin's trial"),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(Feats.GuardianAwoken, 1)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "Greetings. It seems that you may be interested in the Guardian Order's abilities.\n"
                + "If you prove your worth then we will be more than willing to teach you the way of the "
                + "guardian!\n"
                + "Here, try your hand at these trials before we speak more.",
            completion:
                "It seems you may know how to put our abilities to use!\n"
                + "Let's test you some more.");

        yield return new Quest(
            id: WhatsYoursIsMine,
            name: "What's yours is mine",
            description:
                "The paladin has offered us a trial, deal damage by reflecting projectiles.",
            mapId: Camp,
            objectives: One(OrderStats.ReflectDamageWaveMax, 100,
                            menu: "{count} damage dealt with reflected projectiles",
                            npc: "Deal {target} damage with reflected projectiles"),
            reward: Reward(equipmentId: "returned_stone"),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "",
            completion:
                "Nice work. You've shown you understand how to use the enemies projectiles against them!\n"
                + "You are worthy to learn the true power of the shield you hold.");

        yield return new Quest(
            id: ShieldSlammer,
            name: "Shield Slammer",
            description:
                "The paladin told us that our shield has more powers than we know. We must learn what "
                + "they mean by this.",
            mapId: Camp,
            objectives: new[]
            {
                Obj("upgrades.taken.bulwark", 1,
                    menu: "Obtain bulwark upgrade",
                    npc: "Obtain the bulwark upgrade"),
                Obj(OrderStats.BulwarkShoves, 15,
                    menu: "{count} enemies bounced back with bulwark",
                    npc: "and bounce back {target} enemies with your shield"),
            },
            reward: Reward(crystals: 2, upgradeSlug: "bulwark"),
            unlocks: Gate(After(WhatsYoursIsMine)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "",
            completion:
                "Amazing work! You have shown you understand what it means to be a guardian.\n"
                + "We are honored to have you amongst us.");

        yield return new Quest(
            id: AceShooter,
            name: "Ace Shooter",
            description:
                "The paladin has offered us a trial, do not miss a shot with our guided bow's arrows.",
            mapId: Camp,
            objectives: One(OrderStats.GuidedNoMissStreakMax, 100,
                            menu: "{count} guided arrows shot without missing",
                            npc: "Shoot {target} guided arrows without missing"),
            reward: Reward(crystals: 3),
            unlocks: Gate(After(Initiation)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "",
            completion:
                "Brilliant shots! You didn't miss once.\n"
                + "Let's see if you have that same finesse with your sword.");

        yield return new Quest(
            id: SafeDistance,
            name: "A safe distance",
            description:
                "The guardian has given us a trial for our blade. Get long distance kills with our long "
                + "sword in a single wave.",
            mapId: Camp,
            objectives: One(OrderStats.LongRangeSwordKillsWaveMax, 10,
                            menu: "{count} enemies killed with the tip of your long sword in one wave",
                            npc: "Kill {target} enemies with the tip of your long sword in one wave"),
            reward: Reward(equipmentId: "worn_baldric"),
            unlocks: Gate(After(AceShooter)),
            cast: Cast(NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "",
            completion:
                "You have mastered the guardian equipment and shown yourself honorable.\n"
                + "You are hereby an exalted guardian!");

    }
}

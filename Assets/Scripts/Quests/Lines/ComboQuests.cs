using System.Collections.Generic;
using static QuestBuild;

/// <summary>
/// The combination upgrades — the two places where a knight who practises one Order
/// and a knight who practises another are offered the thing that only exists between
/// them. Both NPCs attend, unless both Orders happen to be the same person's.
///
/// These are the only quests whose gate asks something about a knight IN A RUN rather
/// than about the file's lifetime totals: the condition is that one knight qualifies
/// on both Orders at once, which a pair of separate runs can never satisfy. See
/// ComboReadyWatcher for the detector.
///
/// The upgrade itself is hidden from the draft until the quest opens, so the first
/// sight of the card is the quest paying off — see QuestUpgradeReveals.
///
/// PROSE NOT WRITTEN YET. Both carry no offer or completion text, which means no
/// scene plays for them; they still unlock, complete and pay out. Fill them in from
/// Docs/Design/quest-script.md when the combination pass happens.
/// </summary>
public static class ComboQuests
{
    public const string SleepingDart = "combo_sleeping_dart";
    public const string FireSight = "combo_fire_sight";

    public const string SleepingDartUpgrade = "sleeping_dart_1";
    public const string FireSightUpgrade = "fire_sight";

    public static IEnumerable<Quest> All()
    {
        yield return new Quest(
            id: SleepingDart,
            name: "The Sleeping Dart",
            description:
                "",
            mapId: Camp,
            objectives: One("upgrades.taken.sleeping_dart_1", 1,
                            "Take up the Sleeping Dart", hideProgress: true),
            reward: Reward(crystals: 2, upgradeSlug: SleepingDartUpgrade),
            unlocks: Gate(After(SerpentQuests.Initiation), After(ShadowQuests.Initiation),
                          Stat(OrderStats.ComboReady(SleepingDartUpgrade))),
            cast: Cast(NpcId.Ninja, UpgradeOrder.Shadow),
            offer:
                "",
            completion:
                "");

        yield return new Quest(
            id: FireSight,
            name: "Fire Sight",
            description:
                "",
            mapId: Camp,
            objectives: One("upgrades.taken.fire_sight", 1,
                            "Take up Fire Sight", hideProgress: true),
            reward: Reward(crystals: 2, upgradeSlug: FireSightUpgrade),
            unlocks: Gate(After(EmberQuests.Initiation), After(GuardianQuests.Initiation),
                          Stat(OrderStats.ComboReady(FireSightUpgrade))),
            cast: Pair(NpcId.Wizard, UpgradeOrder.Ember, NpcId.Paladin, UpgradeOrder.Guardian),
            offer:
                "",
            completion:
                "");

    }
}

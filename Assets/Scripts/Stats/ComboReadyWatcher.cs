using UnityEngine;

/// <summary>
/// Notices the moment ONE knight first qualifies for a combination upgrade on both
/// of its Orders at once, and publishes the stat the combination quest is gated on.
///
/// This is the only quest condition in the game that asks something about a knight
/// inside a run rather than about the file's lifetime totals, and it has to be:
/// "a knight holds an upgrade from each Order" is exactly the thing two separate
/// runs on two separate knights must NOT satisfy.
///
/// The test is deliberately not a hardcoded threshold per combo. It asks the
/// upgrade's own asset whether it would now be draftable for this knight — every
/// gate except the quest reveal that is hiding it. So Sleeping Dart's two-of-each
/// and Fire Sight's five-Ember-plus-Bowsight-II are both honoured without this
/// file knowing either number, and a combination upgrade added later needs no
/// change here at all.
///
/// Called from UpgradeManager.ApplyUpgrade, which is the one moment that knows both
/// the knight and what they just took.
/// </summary>
public static class ComboReadyWatcher
{
    /// <summary>
    /// Re-checks every quest-revealed combination upgrade against the knight who
    /// just drafted something, and publishes readiness for any that now fits.
    ///
    /// The stat is a latch, not a count: it is written once and never cleared, so
    /// a knight who qualifies mid-run and then dies has still opened the quest.
    /// That is deliberate — the condition is "you have shown you can", and making
    /// the player hold the state until a wave happened to end would be a different,
    /// worse quest.
    /// </summary>
    public static void Evaluate(UpgradeManager manager, KnightTarget knight)
    {
        if (manager == null) return;

        foreach (var quest in QuestDatabase.All)
        {
            var reward = quest.Reward;
            if (reward == null || !reward.RevealsUpgrade) continue;

            string key = OrderStats.ComboReady(reward.UpgradeSlug);
            if (PlayerStats.Get(key) >= 1) continue;          // already latched

            // Only the ones whose gate actually mentions readiness. Capstones are
            // revealed by their quest too, and theirs opens off the line above it
            // rather than off a knight's build.
            if (!Watches(quest, key)) continue;

            if (manager.WouldBeDraftable(reward.UpgradeSlug, knight))
            {
                PlayerStats.Set(key, 1);
                Debug.Log($"[ComboReady] {knight} qualifies for '{reward.UpgradeSlug}' " +
                          $"— opening {quest.Id}.");
            }
        }
    }

    private static bool Watches(Quest quest, string key)
    {
        if (quest.Unlocks == null) return false;
        for (int i = 0; i < quest.Unlocks.Length; i++)
        {
            if (quest.Unlocks[i] != null && quest.Unlocks[i].StatKey == key) return true;
        }
        return false;
    }
}

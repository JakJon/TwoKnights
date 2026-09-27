using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place that answers "may this upgrade appear in a draft yet?" for the
/// upgrades that are hidden behind a quest — the Order capstones and the
/// combination upgrades.
///
/// The link lives on the quest (<see cref="QuestReward.UpgradeSlug"/>) rather than
/// on the upgrade asset, because the quest already has to name the upgrade in order
/// to list it as a reward. Storing it twice would mean an upgrade that appears in
/// the draft while the quest that supposedly grants it says otherwise.
///
/// Built once and cached: the database is static for the life of the process, and
/// this is consulted for every upgrade on every draft.
/// </summary>
public static class QuestUpgradeReveals
{
    private static Dictionary<string, string> _questBySlug;

    private static Dictionary<string, string> Map
    {
        get
        {
            if (_questBySlug != null) return _questBySlug;

            _questBySlug = new Dictionary<string, string>();
            foreach (var quest in QuestDatabase.All)
            {
                var reward = quest.Reward;
                if (reward == null || !reward.RevealsUpgrade) continue;

                if (_questBySlug.TryGetValue(reward.UpgradeSlug, out string owner))
                {
                    // Two quests claiming one upgrade means one of them can never
                    // be the thing that reveals it, and which one wins would be
                    // database order. Say so rather than picking.
                    Debug.LogError(
                        $"[QuestUpgradeReveals] '{reward.UpgradeSlug}' is claimed by both " +
                        $"'{owner}' and '{quest.Id}'. An upgrade may be revealed by one quest only.");
                    continue;
                }
                _questBySlug[reward.UpgradeSlug] = quest.Id;
            }
            return _questBySlug;
        }
    }

    /// <summary>
    /// False only for an upgrade that some quest reveals and whose quest is still
    /// hidden. Everything else — the whole ordinary pool — is available, so the
    /// common answer costs one dictionary miss.
    /// </summary>
    public static bool IsRevealed(string upgradeSlug)
    {
        if (string.IsNullOrEmpty(upgradeSlug)) return true;
        if (!Map.TryGetValue(upgradeSlug, out string questId)) return true;

        // Introduced, not merely gate-open: the scene in which somebody offers you
        // the thing is what puts it in the draft.
        return QuestProgress.IsAnnounced(questId);
    }

    /// <summary>The quest that reveals this upgrade, or null if nothing gates it.</summary>
    public static Quest RevealedBy(string upgradeSlug)
    {
        if (string.IsNullOrEmpty(upgradeSlug)) return null;
        return Map.TryGetValue(upgradeSlug, out string questId) ? QuestDatabase.Get(questId) : null;
    }

    /// <summary>
    /// The upgrade's proper name for a reward line, resolved through the
    /// UpgradeManager's catalog. Falls back to the slug spelled out — a reward that
    /// reads "Acid Dagger" when the asset is found and "acid dagger" when it is not
    /// is still a readable reward, where an empty string would be a silent hole.
    /// </summary>
    public static string DisplayName(string upgradeSlug)
    {
        if (string.IsNullOrEmpty(upgradeSlug)) return "";

        // Resources, like every other reader of this asset — there is no singleton,
        // and this only runs when a reward line is actually drawn.
        var manager = Resources.Load<UpgradeManager>("UpgradeManager");
        if (manager != null)
        {
            foreach (var upgrade in manager.AllUpgrades)
            {
                if (upgrade == null) continue;
                if (UpgradeManager.StatSlug(upgrade.name) == upgradeSlug)
                {
                    return string.IsNullOrEmpty(upgrade.UpgradeName) ? upgrade.name : upgrade.UpgradeName;
                }
            }
        }
        return upgradeSlug.Replace('_', ' ');
    }

    /// <summary>Editor-only escape hatch; the map is otherwise built once per process.</summary>
    public static void Invalidate()
    {
        _questBySlug = null;
    }
}

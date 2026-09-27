using System.Collections.Generic;

/// <summary>
/// How many guided shots a knight has landed in a row without one going wide.
///
/// Per knight, and only the better of the two is ever recorded, because the quest
/// says so out loud: the other knight missing is explicitly allowed. A single
/// shared streak would mean one knight could not practise this while the other was
/// playing normally.
///
/// The live streak is in memory and dies with the run; only its record reaches
/// PlayerStats, through QuestTally so Test Mode is excluded like everything else.
///
/// A shot counts as resolved when its projectile is destroyed, not when it locks:
/// the lock is the shot committing to a target, and whether it arrived is the
/// question being asked. See GuidedShot.OnDestroy.
/// </summary>
public static class GuidedAim
{
    private static readonly Dictionary<string, int> _streak = new Dictionary<string, int>();

    /// <summary>Cleared per run — static, so it outlives the scene load otherwise.</summary>
    public static void BeginRun()
    {
        _streak.Clear();
    }

    /// <summary>
    /// A guided shot finished its flight. Landed extends the streak and may set a
    /// new record; missed puts that knight back to zero and leaves the record alone.
    /// </summary>
    public static void Resolve(string knightTag, bool landed)
    {
        if (string.IsNullOrEmpty(knightTag)) return;

        if (!landed)
        {
            _streak[knightTag] = 0;
            return;
        }

        _streak.TryGetValue(knightTag, out int run);
        run++;
        _streak[knightTag] = run;
        QuestTally.Peak(OrderStats.GuidedNoMissStreakMax, run);
    }
}

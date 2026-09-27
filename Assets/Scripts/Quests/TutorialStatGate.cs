using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What may be counted during the player's very first run.
///
/// The rule is blunt on purpose: THE TUTORIAL RUN DOES NOT COUNT. Nothing it does
/// feeds any quest except the one quest that is awake for it. A new player walking
/// out of the tutorial should arrive in camp with the game's content still ahead of
/// them, not with four Order initiations already open and unannounced behind their
/// back.
///
/// Freezing the quests' READING of the stats was not enough, and this is the
/// correction. A frozen quest still watched its counters climb, so the instant the
/// run ended and the freeze lifted, everything the run had earned landed at once —
/// in the camp, where no NPC can walk on to say so. The counters themselves have to
/// stand still.
///
/// Which keys survive is DERIVED, never listed: whatever the live quests actually
/// read. A quest marked live gets exactly the counters it asks for and the roster
/// cannot drift from the gate.
///
/// Test Mode is the precedent — a run that is not the real thing writes nothing
/// into the record, the same way QuestTally and furthest_wave already treat it.
/// </summary>
public static class TutorialStatGate
{
    /// <summary>
    /// Families that are bookkeeping rather than achievement, and are let through
    /// whatever the run is.
    ///
    /// <c>quests.</c> and <c>baseline.</c> are the quest system talking to itself —
    /// a completion published as a stat, and the mark a relative unlock counts
    /// from. Blocking those would break the one quest that IS live.
    ///
    /// <c>maps.</c> is the run's own progress: which map is open, how deep the
    /// player has been, which gate has fallen. The level select and the map unlock
    /// both read it, and the tutorial run genuinely does end ten waves into the
    /// forest — pretending otherwise would lose the Mine along with the tally.
    ///
    /// <c>q.</c> is a quest's own private copy of a counter. Those are only ever
    /// written for a quest that is already open, and during the tutorial run only a
    /// live quest can be open — so the rule is enforced upstream and blocking them
    /// here would break the one quest that is supposed to work.
    /// </summary>
    private static readonly string[] AlwaysAllowedPrefixes = { "quests.", "baseline.", "maps.", "q." };

    private static HashSet<string> _allowed;

    /// <summary>
    /// True when <paramref name="key"/> may be written right now. False only during
    /// the tutorial run, and only for a counter no live quest cares about.
    /// </summary>
    public static bool Allows(string key)
    {
        if (!TutorialRun.IsTutorialRun) return true;
        if (string.IsNullOrEmpty(key)) return true;

        for (int i = 0; i < AlwaysAllowedPrefixes.Length; i++)
        {
            if (key.StartsWith(AlwaysAllowedPrefixes[i], System.StringComparison.Ordinal)) return true;
        }
        return Allowed.Contains(key);
    }

    private static HashSet<string> Allowed
    {
        get
        {
            if (_allowed != null) return _allowed;

            // Assigned BEFORE it is filled. Building it walks the quest database,
            // and anything down there that wrote a stat would re-enter this getter;
            // a half-built set is survivable, an unbounded recursion is not.
            _allowed = new HashSet<string>();
            foreach (var quest in QuestDatabase.All)
            {
                if (quest == null || !quest.LiveDuringTutorialRun) continue;

                if (quest.Objectives != null)
                {
                    for (int i = 0; i < quest.Objectives.Length; i++)
                    {
                        var objective = quest.Objectives[i];
                        if (objective != null) _allowed.Add(objective.StatKey);
                    }
                }
                if (quest.Unlocks != null)
                {
                    for (int i = 0; i < quest.Unlocks.Length; i++)
                    {
                        var condition = quest.Unlocks[i];
                        if (condition != null) _allowed.Add(condition.StatKey);
                    }
                }
            }
            return _allowed;
        }
    }

    /// <summary>Editor-only escape hatch; the set is otherwise built once per process.</summary>
    public static void Invalidate()
    {
        _allowed = null;
    }
}

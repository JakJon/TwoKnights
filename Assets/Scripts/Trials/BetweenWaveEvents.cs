using System.Collections;
using UnityEngine;

/// <summary>
/// Decides what, if anything, happens in the gap after a survived wave, once the
/// "Wave Survived" panel has gone. At most ONE event gets a gap, and they are ranked
/// (owner, 2026-09-26):
///
///   HIGH    Quest scenes — an NPC offering or completing a quest. Always win.
///           They are played later in the same gap by Spawner.PlayQuestScenes;
///           this only has to see that one is owed and keep everything else out.
///   MEDIUM  Order trials — every initiation's trial. See OrderTrials for when
///           each is due and how two due at once are settled between themselves.
///   LOW     The target range. See TargetRange.
///
/// A lower tier only gets the gap when nothing above it is going to play. A tier
/// rolls its dice only once every tier above it has passed, so a low event can
/// never take a gap a higher one would have had.
///
/// Never after a boss wave, whatever is due.
///
/// Scenes a trial or the range causes — an initiation finishing, the Cartographer
/// offering Target Practice after the first clear — still play in the same gap,
/// after the event: Spawner runs the quest scenes once this returns.
/// </summary>
public static class BetweenWaveEvents
{
    /// <summary>
    /// Picks and plays the gap's event, and returns once it is over (at once when
    /// there is none). Every gap writes a line to the log saying what happened,
    /// because "why did nothing come up" is otherwise unanswerable after the fact.
    /// </summary>
    public static IEnumerator PlayOne(Spawner spawner, int survivedWave, bool afterBoss, string mapId)
    {
        if (afterBoss)
        {
            Debug.Log($"[Events] after wave {survivedWave}: skipped (boss wave)");
            yield break;
        }

        // HIGH. Nothing is played here — the scenes run after this returns — but
        // the gap is theirs, so nothing lower may roll for it.
        if (QuestSceneQueue.HasPending)
        {
            Debug.Log($"[Events] after wave {survivedWave}: an NPC scene has the gap (high)");
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test Mode forcing a target pattern. Checked below the quest scenes on
        // purpose, the same as a forced Order trial: a forced event still gives way
        // to an NPC, so a test run shows the gaps the way a real run would.
        if (TargetRange.TryForced(out var forced))
        {
            yield return TrialRunner.PlayTargets(spawner, forced, practice: true);
            yield break;
        }
#endif

        // MEDIUM.
        if (OrderTrials.TryPick(survivedWave, out var trial, out int phase, out bool practiceTrial))
        {
            yield return TrialRunner.Play(spawner, trial, phase, practiceTrial);
            yield break;
        }

        // LOW.
        if (TargetRange.TryPick(survivedWave, mapId, out var pattern))
        {
            yield return TrialRunner.PlayTargets(spawner, pattern, practice: false);
            yield break;
        }

        Debug.Log($"[Events] after wave {survivedWave}: no event");
    }
}

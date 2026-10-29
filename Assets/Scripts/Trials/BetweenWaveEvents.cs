using System.Collections;
using System.Collections.Generic;
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
///   LOW     The target range (forest) and cart sorting (mine). See TargetRange
///           and CartSorting.
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

        // And a forced cart sorting phase, on the same terms.
        if (CartSorting.TryForced(out var forcedCarts))
        {
            yield return TrialRunner.PlayCarts(spawner, forcedCarts, practice: true);
            yield break;
        }

        // Test Mode with a person at the controls: everything due in this gap is
        // listed and they say which one plays, or none. See TestChoices.
        var choices = TestChoices(spawner, survivedWave, mapId);
        var picker = choices != null ? TestWavePicker.GetOrCreate() : null;
        if (picker != null)
        {
            const int Undecided = -2;
            int picked = Undecided;
            var labels = new List<string>();
            foreach (var choice in choices) labels.Add(choice.Label);

            Time.timeScale = 0f;
            picker.ShowEvents(survivedWave, labels, index => picked = index);
            while (picked == Undecided) yield return null;
            Time.timeScale = 1f;

            if (picked < 0)
            {
                Debug.Log($"[Events] after wave {survivedWave}: no event (picked in Test Mode)");
                yield break;
            }

            yield return choices[picked].Play();
            yield break;
        }
#endif

        // MEDIUM.
        if (OrderTrials.TryPick(survivedWave, out var trial, out int phase, out bool practiceTrial))
        {
            yield return TrialRunner.Play(spawner, trial, phase, practiceTrial);
            yield break;
        }

        // LOW. Two events, and they never meet: the range is the forest's and the
        // carts are the mine's, so at most one of them is ever due in a gap.
        if (TargetRange.TryPick(survivedWave, mapId, out var pattern))
        {
            yield return TrialRunner.PlayTargets(spawner, pattern, practice: false);
            yield break;
        }

        if (CartSorting.TryPick(survivedWave, mapId, out var carts))
        {
            yield return TrialRunner.PlayCarts(spawner, carts, practice: false);
            yield break;
        }

        Debug.Log($"[Events] after wave {survivedWave}: no event");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>One row of Test Mode's event list: what it says, and how to play it.</summary>
    private struct TestChoice
    {
        public string Label;
        public System.Func<IEnumerator> Play;
    }

    /// <summary>
    /// What a tester can choose from in the gap after <paramref name="survivedWave"/>:
    /// every event that is due there — each open Order trial, the target range, the
    /// carts. The dice and the ladder are both set aside, so the range and the carts
    /// need no roll and either can be had in a gap a trial was also due in. Whatever is picked
    /// is the real event, recorded as it would be in any run, not practice.
    ///
    /// Null when there is nothing to ask: nothing is due, or nobody is there to
    /// answer. A forced event (TestRunConfig.ForcedTrial) is already the tester's
    /// choice for every gap, and AutoPickWave means the run is being driven without
    /// a person, so a list would hang it. PlayOne then goes down the ladder as usual.
    /// </summary>
    private static List<TestChoice> TestChoices(Spawner spawner, int survivedWave, string mapId)
    {
        if (!TestRunConfig.ActiveRun) return null;
        if (!string.IsNullOrEmpty(TestRunConfig.ForcedTrial)) return null;
        if (!string.IsNullOrEmpty(TestRunConfig.AutoPickWave)) return null;

        var choices = new List<TestChoice>();

        foreach (var due in OrderTrials.PeekDue(survivedWave))
        {
            var trial = due;
            choices.Add(new TestChoice
            {
                Label = trial.Phases > 1
                    ? $"{trial.Order} trial — phase {OrderTrials.NextPhase(trial) + 1} of {trial.Phases}"
                    : $"{trial.Order} trial",
                Play = () => TrialRunner.Play(spawner, trial, OrderTrials.PickByHand(trial, survivedWave),
                                              practice: false),
            });
        }

        var range = TargetRange.PeekDue(survivedWave, mapId);
        if (range != null)
        {
            choices.Add(new TestChoice
            {
                Label = $"Target range — pattern {range.Number}",
                Play = () =>
                {
                    TargetRange.PickByHand(range, survivedWave);
                    return TrialRunner.PlayTargets(spawner, range, practice: false);
                },
            });
        }

        var carts = CartSorting.PeekDue(survivedWave, mapId);
        if (carts != null)
        {
            choices.Add(new TestChoice
            {
                Label = $"Cart sorting — phase {carts.Number}",
                Play = () =>
                {
                    CartSorting.PickByHand(carts, survivedWave);
                    return TrialRunner.PlayCarts(spawner, carts, practice: false);
                },
            });
        }

        return choices.Count > 0 ? choices : null;
    }
#endif
}

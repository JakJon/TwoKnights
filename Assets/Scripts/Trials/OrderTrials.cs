using UnityEngine;

/// <summary>
/// The Orders' initiation trials: short tests that can turn up in the gap between
/// two waves while an Order's initiation quest is open. Passing them is how the
/// initiation completes — they replaced the old "set 300 things alight" counters,
/// which the owner found too plain to be anybody's welcome into an Order.
///
/// A trial's content — every orb, rock and blink, and when it comes — is fixed, so a
/// player who fails one can learn it. That keeps the no-randomness pillar the waves
/// live under.
///
/// Rules (owner's calls):
/// - Only after a SURVIVED wave, and only in a gap with no NPC scenes queued.
/// - Never after a boss.
/// - One trial per gap, ever.
/// - Each phase of a three-phase trial is its own event: passing phase one means the
///   NEXT time the trial comes up it is phase two. Losing a phase replays that
///   phase next time, never an earlier one.
/// - (2026-09-25) When more than one trial is due, the one that has gone the
///   LONGEST without playing gets the gap. This replaced "the rarest wins".
/// </summary>
public static class OrderTrials
{
    public enum Kind { FireOrbs, IceOrbs, VenomOrbs, NinjaChase, GuardianBout, DawnBout }

    public sealed class Trial
    {
        public readonly UpgradeOrder Order;
        public readonly Kind Kind;
        /// <summary>The initiation quest the trial belongs to. It only plays while that quest is open.</summary>
        public readonly string QuestId;
        /// <summary>Counts phases passed. The initiation's one objective reads it.</summary>
        public readonly string StatKey;
        /// <summary>The first wave the trial is due after.</summary>
        public readonly int FirstWave;
        /// <summary>...and every this-many waves from there.</summary>
        public readonly int EveryNthWave;
        public readonly float Chance;
        public readonly int Phases;
        /// <summary>Test Mode's name for it, and the value <see cref="TestRunConfig.ForcedTrial"/> takes.</summary>
        public readonly string Key;

        public Trial(UpgradeOrder order, Kind kind, string questId, string key,
                     int firstWave, int everyNthWave, float chance, int phases)
        {
            Order = order;
            Kind = kind;
            QuestId = questId;
            Key = key;
            StatKey = "trials." + key;
            FirstWave = firstWave;
            EveryNthWave = everyNthWave;
            Chance = chance;
            Phases = phases;
        }

        public bool IsDueAfter(int wave)
        {
            return wave >= FirstWave && (wave - FirstWave) % EveryNthWave == 0;
        }

        /// <summary>
        /// The trial clock's reading the last time this trial played. Bookkeeping,
        /// not an achievement: written with PlayerStats.Set so no quest meter sees it,
        /// and left out of StatsDatabase so the Stats panel does not list it.
        /// </summary>
        public string LastPlayedKey => StatKey + ".last";
    }

    /// <summary>
    /// Every trial and when it is due (owner, 2026-09-25): all of them at 100%. Fire
    /// and ice after every ODD wave, the Serpent and the Ninja after every EVEN wave,
    /// the Paladin's two on the timings they always had — Dawn every sixth wave, the
    /// Guardian every seventh. The order of this list no longer matters.
    /// </summary>
    public static readonly Trial[] All =
    {
        //                                                                                      first  every  chance  phases
        new Trial(UpgradeOrder.Ember,    Kind.FireOrbs,     EmberQuests.Initiation,    "ember",    1,     2,     1.00f,  3),
        new Trial(UpgradeOrder.Frigid,   Kind.IceOrbs,      FrigidQuests.Initiation,   "frigid",   1,     2,     1.00f,  3),
        new Trial(UpgradeOrder.Serpent,  Kind.VenomOrbs,    SerpentQuests.Initiation,  "serpent",  2,     2,     1.00f,  3),
        new Trial(UpgradeOrder.Shadow,   Kind.NinjaChase,   ShadowQuests.Initiation,   "shadow",   2,     2,     1.00f,  3),
        new Trial(UpgradeOrder.Dawn,     Kind.DawnBout,     DawnQuests.Initiation,     "dawn",     6,     6,     1.00f,  1),
        new Trial(UpgradeOrder.Guardian, Kind.GuardianBout, GuardianQuests.Initiation, "guardian", 7,     7,     1.00f,  1),
    };

    /// <summary>
    /// Counts the gaps trials have been decided in, across every run on the file.
    /// "How long has it been" is measured on this clock, so it carries over from
    /// one run to the next rather than everyone starting level each run.
    /// </summary>
    private const string ClockKey = "trials.clock";

    public static Trial Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Key == key) return All[i];
        }
        return null;
    }

    /// <summary>
    /// Decides whether the gap after <paramref name="survivedWave"/> gets a trial.
    /// The caller has already ruled out boss waves and gaps with NPC scenes queued.
    ///
    /// Of the trials that are due, open and roll their chance, the one that has gone
    /// the longest without playing wins. A trial that has never played beats any
    /// that has; an exact tie is broken at random, evenly.
    ///
    /// <paramref name="practice"/> comes back true for a trial Test Mode forced:
    /// it plays exactly as the real one does and records nothing.
    /// </summary>
    public static bool TryPick(int survivedWave, out Trial trial, out int phase, out bool practice)
    {
        trial = null;
        phase = 0;
        practice = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Only inside a Test Mode run: the setting is static and outlives the run
        // that asked for it, and an ordinary run must never play practice trials.
        var forced = TestRunConfig.ActiveRun ? Find(TestRunConfig.ForcedTrial) : null;
        if (forced != null)
        {
            trial = forced;
            phase = Mathf.Clamp(TestRunConfig.ForcedTrialPhase - 1, 0, forced.Phases - 1);
            practice = true;
            return true;
        }
#endif

        if (survivedWave <= 0) return false;

        // Written between waves, so the stat journal is off and it lands at once.
        int clock = PlayerStats.Get(ClockKey) + 1;
        PlayerStats.Set(ClockKey, clock);

        // What was due and how each went, for the log line at the end. Without it a
        // gap with no trial looks the same as a trial that could never come up.
        var report = new System.Text.StringBuilder();

        Trial best = null;
        int bestWait = -1;
        int tied = 0;

        for (int i = 0; i < All.Length; i++)
        {
            var candidate = All[i];
            if (!candidate.IsDueAfter(survivedWave)) continue;
            if (!IsOpen(candidate))
            {
                report.Append(candidate.Key).Append(" closed, ");
                continue;
            }
            if (Random.value >= candidate.Chance)
            {
                report.Append(candidate.Key).Append(" missed its roll, ");
                continue;
            }

            int wait = GapsSinceLastPlayed(candidate, clock);
            report.Append(candidate.Key)
                  .Append(wait == int.MaxValue ? " never played, " : " waited " + wait + ", ");

            if (wait > bestWait)
            {
                best = candidate;
                bestWait = wait;
                tied = 1;
            }
            else if (wait == bestWait)
            {
                // Each of the tied trials ends up picked with equal odds, however
                // many there are, without having to collect them first.
                tied++;
                if (Random.Range(0, tied) == 0) best = candidate;
            }
        }

        if (best == null)
        {
            Debug.Log(report.Length > 0
                ? $"[Trials] after wave {survivedWave}: {report.ToString().TrimEnd(',', ' ')} — no trial"
                : $"[Trials] after wave {survivedWave}: nothing due — no trial");
            return false;
        }

        // Played, win or lose: this is what "gone the longest" is measured from.
        PlayerStats.Set(best.LastPlayedKey, clock);

        trial = best;
        phase = NextPhase(best);
        Debug.Log($"[Trials] after wave {survivedWave}: {report.ToString().TrimEnd(',', ' ')} — {best.Key} plays");
        return true;
    }

    /// <summary>Gaps since the trial last played, or int.MaxValue if it never has.</summary>
    private static int GapsSinceLastPlayed(Trial trial, int clock)
    {
        int last = PlayerStats.Get(trial.LastPlayedKey);
        return last <= 0 ? int.MaxValue : clock - last;
    }

    /// <summary>
    /// The initiation has been offered, is not finished, and still has a phase to
    /// play. An offer the NPC has not made yet does not count: the trial is what the
    /// offer explains, so it cannot turn up before the explanation.
    /// </summary>
    public static bool IsOpen(Trial trial)
    {
        if (trial == null) return false;
        var quest = QuestDatabase.Get(trial.QuestId);
        if (quest == null) return false;
        // Covers the tutorial run too — every Order quest is frozen through it.
        if (!quest.IsUnlocked) return false;
        if (!QuestProgress.IsAnnounced(trial.QuestId)) return false;
        if (QuestProgress.IsCompleted(trial.QuestId)) return false;
        return NextPhase(trial) < trial.Phases;
    }

    /// <summary>
    /// Zero-based: how many phases this initiation has already seen passed. Read off
    /// the quest's own counter, so it starts at zero when the quest was offered and
    /// matches what the quest log shows.
    /// </summary>
    public static int NextPhase(Trial trial)
    {
        var objective = Objective(trial);
        return objective != null ? objective.Current : 0;
    }

    /// <summary>A phase was passed. Written between waves, so it lands at once.</summary>
    public static void RecordPass(Trial trial)
    {
        if (trial == null) return;
        PlayerStats.Increment(trial.StatKey);
        PlayerStats.Flush();
    }

    private static QuestObjective Objective(Trial trial)
    {
        var quest = QuestDatabase.Get(trial.QuestId);
        if (quest == null || quest.Objectives == null) return null;
        for (int i = 0; i < quest.Objectives.Length; i++)
        {
            if (quest.Objectives[i] != null && quest.Objectives[i].StatKey == trial.StatKey)
                return quest.Objectives[i];
        }
        return null;
    }
}

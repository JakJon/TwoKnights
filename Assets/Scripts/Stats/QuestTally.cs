using System.Collections.Generic;

/// <summary>
/// The accumulators behind the quest objectives that ask "how many at once", "how
/// many in one wave", or "how far without failing". Those are records, not running
/// totals, so each needs somewhere to count that resets — and somewhere permanent
/// for the best result to land.
///
/// The live counters are in memory and die with the run; only the record reaches
/// <see cref="PlayerStats"/>, always through Raise, so a bad wave can never undo a
/// good one.
///
/// Records are raised AS THEY GROW rather than reported at the end of the wave.
/// A wave that ends in death, a quit to camp, or a boss victory takes a different
/// path out of Spawner.RunWave, and an end-of-wave report would be silently lost
/// down at least one of them. Raising continuously costs a dictionary lookup per
/// event and cannot lose anything.
///
/// Everything here is a no-op during a Test Mode run, for the same reason
/// furthest_wave is: a run that begins at wave 20 with a finished build would set
/// every record in the game on its first encounter.
/// </summary>
public static class QuestTally
{
    private static readonly Dictionary<string, int> _wave = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> _run = new Dictionary<string, int>();

    private static bool Excluded => TestRunConfig.ActiveRun;

    /// <summary>Called from Spawner.Start. Clears everything — statics outlive the scene load.</summary>
    public static void BeginRun()
    {
        _wave.Clear();
        _run.Clear();
    }

    /// <summary>Called from Spawner.BeginWave, beside DawnVigil.BeginWave.</summary>
    public static void BeginWave()
    {
        _wave.Clear();
    }

    /// <summary>Add to this wave's tally and record it if it is the best yet.</summary>
    public static void Wave(string maxKey, int amount = 1)
    {
        if (Excluded || amount <= 0) return;
        _wave.TryGetValue(maxKey, out int running);
        running += amount;
        _wave[maxKey] = running;
        PlayerStats.Raise(maxKey, running);
    }

    /// <summary>Add to this run's tally and record it if it is the best yet.</summary>
    public static void Run(string maxKey, int amount = 1)
    {
        if (Excluded || amount <= 0) return;
        _run.TryGetValue(maxKey, out int running);
        running += amount;
        _run[maxKey] = running;
        PlayerStats.Raise(maxKey, running);
    }

    /// <summary>
    /// Record a value the caller is already tracking — a count of things alive right
    /// now, or the length of a streak. Nothing is accumulated here; this is only the
    /// high-water mark.
    /// </summary>
    public static void Peak(string maxKey, int currentValue)
    {
        if (Excluded || currentValue <= 0) return;
        PlayerStats.Raise(maxKey, currentValue);
    }

    /// <summary>A plain lifetime total.</summary>
    public static void Total(string key, int amount = 1)
    {
        if (Excluded || amount <= 0) return;
        PlayerStats.Increment(key, amount);
    }

    /// <summary>This wave's running value, for a caller that needs to read it back.</summary>
    public static int InWave(string maxKey)
    {
        _wave.TryGetValue(maxKey, out int running);
        return running;
    }

    /// <summary>
    /// Drops a run tally back to zero without touching the record. For the counters
    /// that measure an UNBROKEN run of something — a burn that lapsed, a guided shot
    /// that missed — where the record stands but the attempt starts again.
    /// </summary>
    public static void BreakRun(string maxKey)
    {
        _run.Remove(maxKey);
    }

    /// <summary>Same, for a wave-scoped streak.</summary>
    public static void BreakWave(string maxKey)
    {
        _wave.Remove(maxKey);
    }
}

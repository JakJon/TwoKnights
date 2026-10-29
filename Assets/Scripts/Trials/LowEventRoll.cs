using UnityEngine;

/// <summary>
/// The roll the two low-priority events share: the forest's target range and the
/// mine's cart sorting (owner, 2026-10-13).
///
/// In a gap the event is due in, it has one chance in four. Once four waves or more
/// have gone by without it, that becomes one in two until it plays. The wait is
/// counted inside this run only: from the wave it last played after, or, while it
/// has not played yet, from the wave before the first one it can follow. So with a
/// first wave of 5, the gaps after waves 5, 6 and 7 are one in four and the gap
/// after wave 8 is one in two.
///
/// Before this the range played every time and the carts one time in three. The
/// better odds after a wait are there because a run of misses reads as the event
/// having gone for good — the reason the range's first roll was taken out on
/// 2026-09-26.
///
/// One count does for both events. The range is the forest's and the carts are
/// the mine's, so a run only ever has one of them. A low event that shares a map
/// with another will need a count of its own.
///
/// The roll decides only WHETHER the event plays. What is in it is never rolled.
/// </summary>
public static class LowEventRoll
{
    /// <summary>One gap in four, once nothing higher wants the gap.</summary>
    public const float Chance = 0.25f;

    /// <summary>One gap in two, once it has been <see cref="LongWaitWaves"/> without it.</summary>
    public const float LongWaitChance = 0.5f;

    /// <summary>How many waves without the event count as a long wait.</summary>
    public const int LongWaitWaves = 4;

    /// <summary>The wave the event last played after in this run. 0 while it has not played.</summary>
    private static int _lastPlayedAfter;

    /// <summary>Static, so it survives a scene reload. Called by the Spawner as a run starts.</summary>
    public static void BeginRun()
    {
        _lastPlayedAfter = 0;
    }

    /// <summary>
    /// The chance in the gap after <paramref name="survivedWave"/>, for an event
    /// that can first follow wave <paramref name="firstWave"/>.
    /// </summary>
    public static float ChanceAfter(int survivedWave, int firstWave)
    {
        int waited = survivedWave - Mathf.Max(_lastPlayedAfter, firstWave - 1);
        return waited >= LongWaitWaves ? LongWaitChance : Chance;
    }

    /// <summary>
    /// Rolls for the gap. <paramref name="percent"/> is the chance it had, for the log.
    /// </summary>
    public static bool Roll(int survivedWave, int firstWave, out int percent)
    {
        float chance = ChanceAfter(survivedWave, firstWave);
        percent = Mathf.RoundToInt(chance * 100f);
        return Random.value < chance;
    }

    /// <summary>The event played in the gap after <paramref name="survivedWave"/>, won or lost.</summary>
    public static void NotePlayed(int survivedWave)
    {
        _lastPlayedAfter = survivedWave;
    }
}

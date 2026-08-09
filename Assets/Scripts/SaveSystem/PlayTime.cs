using UnityEngine;

/// <summary>
/// Total time spent on the live file, counted in unscaled seconds so a paused
/// menu, an upgrade screen and the camp all count — the meter measures time with
/// the game, not time with the simulation running.
///
/// Counting happens in memory every frame; disk writes are rate limited, for the
/// same reason PlayerStats batches its own (a full JSON serialize plus an atomic
/// file replace is far too heavy to do per frame). Worst case a crash costs the
/// last <see cref="FlushInterval"/> seconds.
/// </summary>
public static class PlayTime
{
    private const double FlushInterval = 60;

    public static double Seconds => SaveManager.Data.playTimeSeconds;

    /// <summary>
    /// "3h 12m" / "12m 04s" / "48s". Hours never roll over into days: a play time
    /// is easier to compare as one number than as two units the reader has to
    /// multiply, and this is read at a glance off a file bar.
    /// </summary>
    public static string Format(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;

        int total = (int)seconds;
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int secs = total % 60;

        if (hours > 0) return $"{hours}h {minutes:00}m";
        if (minutes > 0) return $"{minutes}m {secs:00}s";
        return $"{secs}s";
    }

    /// <summary>
    /// Persists pending time. Called at the same safe points as PlayerStats.Flush
    /// plus on a timer, so callers rarely need this directly.
    /// </summary>
    public static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        SaveManager.Save();
    }

    private static bool _dirty;
    private static double _sinceFlush;

    private static void Tick(float unscaledDelta)
    {
        if (unscaledDelta <= 0f) return;

        // Straight through to the live save data: a file switch discards that
        // object wholesale, so time accrued before the player picked a file can
        // never be misattributed to the file they picked.
        SaveManager.Data.playTimeSeconds += unscaledDelta;
        _dirty = true;

        _sinceFlush += unscaledDelta;
        if (_sinceFlush >= FlushInterval)
        {
            _sinceFlush = 0;
            Flush();
        }
    }

    // A hidden, scene-independent ticker. Spawned from code rather than placed in
    // a scene so play time is counted identically however the game was entered —
    // including an editor session started straight in Main.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("~PlayTime");
        host.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(host);
        host.AddComponent<PlayTimeTicker>();
    }

    private class PlayTimeTicker : MonoBehaviour
    {
        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        // Quitting, or dropping to the background on a platform that may never
        // return, would otherwise lose everything since the last timed flush.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit()
        {
            Flush();
        }
    }
}

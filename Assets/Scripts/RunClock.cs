using UnityEngine;

/// <summary>
/// How long the knights have spent actually playing this run, for the death
/// screen. Counts game time, so anything that freezes the arena (the pause menu,
/// the upgrade menu, every NPC conversation, the Wave Survived beat, the black
/// between waves) is left out by itself. The checks in Tick say the owner's rule
/// out loud (2026-10-17: no pause, no upgrade menu, no NPC dialogue), so it still
/// holds if one of those ever stops freezing time.
/// Static, so it survives the scene load and must be zeroed at the start of a run.
/// </summary>
public static class RunClock
{
    public static float Seconds { get; private set; }

    public static void BeginRun()
    {
        Seconds = 0f;
    }

    /// <summary>Called every frame by the Spawner, which owns the run.</summary>
    public static void Tick(bool upgradeMenuOpen)
    {
        if (upgradeMenuOpen) return;
        if (PauseMenu.IsPaused) return;
        if (QuestScene.IsPlaying || TrialRunner.IsSpeaking) return;
        if (DeathScreen.IsVisible) return;

        Seconds += Time.deltaTime;
    }

    /// <summary>"9:05" under an hour, "1:02:05" past it.</summary>
    public static string Format(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = total / 3600;
        int minutes = (total / 60) % 60;
        int secs = total % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{secs:00}"
            : $"{minutes}:{secs:00}";
    }
}

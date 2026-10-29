using UnityEngine;

// How strongly enemy rocks glow, as a setting the player owns: 0 = no rim at
// all, ring or glow, 1 = as built, 2 = double.
// RockOutline multiplies its glow strength by this every frame, so a change
// shows on rocks already in the air.
//
// Kept in PlayerPrefs, like the volume and the bar layout: it belongs to the
// screen the game is played on, not to the save file.
public static class RockGlowSetting
{
    private const string Key = "fx.rockGlow";

    public const float Max = 2f;
    private const float StepSize = 0.1f;

    private static bool _loaded;
    private static float _intensity = 1f;

    public static float Intensity
    {
        get
        {
            if (!_loaded)
            {
                _intensity = Mathf.Clamp(PlayerPrefs.GetFloat(Key, 1f), 0f, Max);
                _loaded = true;
            }
            return _intensity;
        }
    }

    /// <summary>One notch up or down. False when already at the end.</summary>
    public static bool Step(int direction)
    {
        float current = Intensity;
        float next = Mathf.Clamp(Mathf.Round((current + direction * StepSize) / StepSize) * StepSize, 0f, Max);
        if (Mathf.Approximately(next, current)) return false;
        _intensity = next;
        PlayerPrefs.SetFloat(Key, _intensity);
        return true;
    }

    public static void Save() => PlayerPrefs.Save();
}

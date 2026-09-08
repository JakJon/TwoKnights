using UnityEngine;

/// <summary>
/// Which rock a shaft just spat out. Plain is the ordinary grey stone every
/// wave has always thrown; the other two only appear deep into a run.
/// </summary>
public enum RockVariant
{
    Plain = 0,
    Poison = 1,
    Explosive = 2
}

/// <summary>
/// The late-run escalation on enemy rocks, by map.
///
///   * The Camp Fields — from wave eleven, every tenth rock is powder.
///   * The Mine — powder from the FIRST wave, every tenth rock.
///   * The Mine, from wave ten — poison as well, every fifteenth rock, along
///     with the quarter-faster flight. Poison is the mine's DEEP signature and
///     nothing before wave ten is green (owner's call, 2026-09-07): the green
///     rock is the one that carries a cost past the hit itself, and meeting it
///     on the first wave of the first map gave the player nothing to escalate
///     into. Powder is the rock that teaches the guard; poison is the reward
///     for having learned it.
///
/// A COUNT, NEVER A ROLL. The cadence is the promise: a player who has been
/// counting knows the tenth one is the loud one before it leaves the shaft, and
/// can spend the guard on it. The count resets at the top of every wave (see
/// Spawner) so what a wave throws never depends on the run that led to it —
/// the same rule RailNetwork's cart variants follow.
///
/// The two cadences overlap on the thirtieth rock. Poison wins there: it is the
/// rarer of the two promises and the one worth having counted for.
/// </summary>
public static class RockEscalation
{
    public const string ForestMapId = "camp_fields";
    public const string MineMapId = "mine";

    // "After wave 10 in the forest" — so wave eleven onward.
    public const int ForestFromWaveNumber = 11;

    // The mine throws powder from the off.
    public const int MineExplosiveFromWaveNumber = 1;

    // Poison and the hurried flight are the DEEP mine, and arrive together.
    public const int MineEscalationFromWaveNumber = 10;

    public const int ExplosiveEveryNth = 10;
    public const int PoisonEveryNth = 15;

    /// <summary>How much faster the mine's rocks fly once the escalation is on.</summary>
    public const float MineSpeedScale = 1.25f;

    private static int _thrown;

    /// <summary>Called by the Spawner at the top of every wave.</summary>
    public static void ResetForWave()
    {
        _thrown = 0;
    }

    /// <summary>
    /// Advances the count and says what the rock leaving now is. Call once per
    /// rock, at the moment the wave asks for it rather than when the delayed
    /// spawn actually fires, so the cadence follows the order the wave authored.
    /// </summary>
    public static RockVariant NextVariant()
    {
        _thrown++;

        if (!TryGetMapId(out string mapId)) return RockVariant.Plain;
        int wave = CurrentWaveNumber();

        if (mapId == MineMapId)
        {
            // Poison outranks powder where the two cadences meet, on the
            // thirtieth rock: it is the rarer promise and the one worth counting for.
            if (wave >= MineEscalationFromWaveNumber && _thrown % PoisonEveryNth == 0)
            {
                return RockVariant.Poison;
            }
            if (wave >= MineExplosiveFromWaveNumber && _thrown % ExplosiveEveryNth == 0)
            {
                return RockVariant.Explosive;
            }
            return RockVariant.Plain;
        }

        if (mapId == ForestMapId && wave >= ForestFromWaveNumber)
        {
            if (_thrown % ExplosiveEveryNth == 0) return RockVariant.Explosive;
        }

        return RockVariant.Plain;
    }

    /// <summary>
    /// Multiplier on whatever speed the wave asked for. Only the mine hurries,
    /// and only once it is deep enough for the escalation.
    /// </summary>
    public static float SpeedScale
    {
        get
        {
            if (!TryGetMapId(out string mapId)) return 1f;
            return (mapId == MineMapId && CurrentWaveNumber() >= MineEscalationFromWaveNumber)
                ? MineSpeedScale
                : 1f;
        }
    }

    private static bool TryGetMapId(out string mapId)
    {
        mapId = null;
        var manager = WaveManager.ActiveInstance;
        if (manager == null) return false;
        var map = manager.CurrentMap;
        if (map == null) return false;
        mapId = map.MapId;
        return !string.IsNullOrEmpty(mapId);
    }

    private static int CurrentWaveNumber()
    {
        var manager = WaveManager.ActiveInstance;
        return manager != null ? manager.CurrentWaveNumber : 0;
    }
}

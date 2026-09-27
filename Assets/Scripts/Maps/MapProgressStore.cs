using System.Collections.Generic;
using UnityEngine;

// Save-backed per-map progression: unlocked / gate cleared / true boss cleared.
// Also publishes the map stat keys that quests hook
// (maps.<id>.unlocked, maps.<id>.gate_cleared, maps.<id>.true_cleared).
public static class MapProgressStore
{
    /// <summary>Set the moment a map becomes selectable. Quests for a map gate on this.</summary>
    public static string UnlockedStatKey(string mapId) => $"maps.{mapId}.unlocked";

    public static MapRecord Get(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        var list = SaveManager.Data.maps ?? (SaveManager.Data.maps = new List<MapRecord>());
        foreach (var record in list)
        {
            if (record != null && record.mapId == mapId) return record;
        }
        var fresh = new MapRecord { mapId = mapId };
        list.Add(fresh);
        return fresh;
    }

    public static bool IsUnlocked(MapDefinition map)
    {
        if (map == null) return false;
        if (map.UnlockedByDefault) return true;
        var record = Get(map.MapId);
        if (record != null && record.unlocked) return true;

        // Back-fill. Unlock() only ever runs on the FIRST gate kill, so a save
        // that cleared a gate before that gate was wired to unlock anything —
        // or while the next map was still unlockedByDefault — never received
        // the write, and locking the map now would strand it outside forever.
        // The gate-cleared record is the durable fact, so derive from it.
        var unlocker = UnlockerOf(map.MapId);
        if (unlocker != null && IsGateCleared(unlocker.MapId))
        {
            Unlock(map.MapId);
            return true;
        }
        return false;
    }

    /// <summary>The map whose gate boss opens <paramref name="mapId"/>, or null when nothing does.</summary>
    public static MapDefinition UnlockerOf(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        var catalog = MapCatalog.Instance;
        if (catalog == null) return null;
        foreach (var candidate in catalog.Maps)
        {
            if (candidate != null && candidate.UnlocksMapId == mapId) return candidate;
        }
        return null;
    }

    public static bool IsGateCleared(string mapId)
    {
        var record = Get(mapId);
        return record != null && record.gateCleared;
    }

    public static bool IsTrueCleared(string mapId)
    {
        var record = Get(mapId);
        return record != null && record.trueCleared;
    }

    /// <summary>How deep this map has ever been run, in waves CLEARED.</summary>
    public static int FurthestWave(string mapId)
    {
        var record = Get(mapId);
        return record != null ? record.furthestWave : 0;
    }

    /// <summary>
    /// Waves a run just cleared on this map. Only ever raises the record, so the
    /// order runs happen in cannot lose a deeper one.
    /// </summary>
    public static void RecordWavesCleared(string mapId, int wavesCleared)
    {
        if (string.IsNullOrEmpty(mapId) || wavesCleared <= 0) return;
        var record = Get(mapId);
        if (record == null || record.furthestWave >= wavesCleared) return;
        record.furthestWave = wavesCleared;
        SaveManager.Save();
    }

    /// <summary>How many stars one map can wear on the level select.</summary>
    public const int StarCount = 3;

    /// <summary>
    /// First star: the run reached the bottom of the map — its authored
    /// finalWaveNumber. Not the true boss's wave: the Camp Fields keeps playing
    /// past the Twins, and the star is for getting all the way down.
    /// </summary>
    public static bool HasDepthStar(MapDefinition map)
    {
        if (map == null) return false;
        return FurthestWave(map.MapId) >= map.FinalWaveNumber;
    }

    /// <summary>
    /// Second star: every quest filed under this map is finished. A map with no
    /// quests authored yet never earns it — otherwise an empty line would hand
    /// out a star for nothing.
    /// </summary>
    public static bool HasQuestStar(string mapId)
    {
        QuestTally(mapId, out int done, out int total);
        return total > 0 && done >= total;
    }

    /// <summary>
    /// Third star: every wave type on the map has been met. Deliberately a true
    /// sweep, where the explorer QUEST finale only asks for 75% — so finishing a
    /// map's quests and having seen all of it stay two different achievements.
    /// </summary>
    public static bool HasWaveTypeStar(string mapId)
    {
        WaveTypeTally(mapId, out int found, out int total);
        return total > 0 && found >= total;
    }

    /// <summary>
    /// How many of this map's wave types the player has met, and how many it
    /// has. Both numbers are real: unlike how deep a map goes, there is nothing
    /// worth withholding in saying how much there is left to find. The found
    /// count is clamped, because the saved tally is recounted from the setlist
    /// and a wave dropped from the map would otherwise read as 33 of 32.
    /// </summary>
    public static void WaveTypeTally(string mapId, out int found, out int total)
    {
        found = 0;
        total = 0;
        if (string.IsNullOrEmpty(mapId)) return;

        total = WaveExploration.TotalWaveTypesFor(mapId);
        if (total <= 0) return;
        found = Mathf.Min(PlayerStats.Get(WaveExploration.DistinctStatKey(mapId)), total);
    }

    /// <summary>How many of the three stars this map has earned, 0 to 3.</summary>
    public static int StarsFor(MapDefinition map)
    {
        if (map == null) return 0;
        int stars = 0;
        if (HasDepthStar(map)) stars++;
        if (HasQuestStar(map.MapId)) stars++;
        if (HasWaveTypeStar(map.MapId)) stars++;
        return stars;
    }

    /// <summary>
    /// How many quests this map has, and how many are done — the level select
    /// prints both. The total is always the REAL total, never hidden behind a
    /// question mark, so a card says up front how much work a map holds.
    /// </summary>
    public static void QuestTally(string mapId, out int completed, out int total)
    {
        completed = 0;
        total = 0;
        if (string.IsNullOrEmpty(mapId)) return;
        foreach (var quest in QuestDatabase.ForMap(mapId))
        {
            total++;
            if (QuestProgress.IsCompleted(quest.Id)) completed++;
        }
    }

    public static void Unlock(string mapId)
    {
        var record = Get(mapId);
        if (record == null) return;
        if (!record.unlocked)
        {
            record.unlocked = true;
            SaveManager.Save();
        }

        // Published on every call, not only on the write. A save that unlocked a
        // map before this stat existed would otherwise never announce it, and
        // the quests waiting on "the Mine is open" would stay shut forever.
        PlayerStats.Raise(UnlockedStatKey(mapId), 1);
    }

    /// <summary>
    /// Republishes what the save already knows, so quests gated on a map being
    /// open settle at load rather than at the next gate kill. Both halves need
    /// it: `unlocked` is a save flag that predates its stat, and gate_cleared is
    /// only incremented on the FIRST kill, which an older save already spent.
    /// </summary>
    public static void PublishProgressStats()
    {
        var catalog = MapCatalog.Instance;
        if (catalog == null) return;
        foreach (var map in catalog.Maps)
        {
            if (map == null) continue;
            if (IsUnlocked(map)) PlayerStats.Republish(UnlockedStatKey(map.MapId), 1);
            if (IsGateCleared(map.MapId)) PlayerStats.Republish($"maps.{map.MapId}.gate_cleared", 1);
            if (IsTrueCleared(map.MapId)) PlayerStats.Republish($"maps.{map.MapId}.true_cleared", 1);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PublishOnLoad()
    {
        PublishProgressStats();
        SaveManager.OnActiveSlotChanged -= PublishProgressStats;
        SaveManager.OnActiveSlotChanged += PublishProgressStats;
    }

    // First gate kill: marks the map, unlocks the next one, feeds quest stats.
    // Safe to call on every kill — repeats are no-ops.
    public static void MarkGateCleared(MapDefinition map)
    {
        if (map == null) return;
        var record = Get(map.MapId);
        if (record == null || record.gateCleared) return;

        record.gateCleared = true;
        SaveManager.Save();

        if (!string.IsNullOrEmpty(map.UnlocksMapId))
        {
            Unlock(map.UnlocksMapId);
        }

        PlayerStats.Increment($"maps.{map.MapId}.gate_cleared");
    }

    public static void MarkTrueCleared(MapDefinition map)
    {
        if (map == null) return;
        var record = Get(map.MapId);
        if (record == null || record.trueCleared) return;

        record.trueCleared = true;
        SaveManager.Save();

        PlayerStats.Increment($"maps.{map.MapId}.true_cleared");
    }
}

using System.Collections.Generic;

// Save-backed per-map progression: unlocked / gate cleared / true boss cleared.
// Also increments the map stat keys that quests can hook
// (maps.<id>.gate_cleared, maps.<id>.true_cleared).
public static class MapProgressStore
{
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
        return record != null && record.unlocked;
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

    public static void Unlock(string mapId)
    {
        var record = Get(mapId);
        if (record == null || record.unlocked) return;
        record.unlocked = true;
        SaveManager.Save();
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

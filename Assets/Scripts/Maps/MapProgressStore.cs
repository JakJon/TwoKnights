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

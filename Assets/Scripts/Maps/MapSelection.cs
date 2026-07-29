using UnityEngine;

// Carries the level-select choice from the camp scene into the game scene.
// Static (not a scene object) because the choice has to survive the scene load
// that acts on it; the pick is also written to the save so the level select
// reopens on the map you last played.
public static class MapSelection
{
    private static MapDefinition _selected;

    public static MapDefinition Selected => _selected;

    public static void Select(MapDefinition map)
    {
        if (map == null) return;
        _selected = map;
        if (SaveManager.Data.lastPlayedMapId != map.MapId)
        {
            SaveManager.Data.lastPlayedMapId = map.MapId;
            SaveManager.Save();
        }
    }

    // What the run should actually play. Falls back down the chain so a run
    // started without ever touching the level select (Test Mode, a direct scene
    // play from the editor) still lands on a real map.
    public static MapDefinition Resolve()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // A pending Test Mode setup names its own map — and may name a locked
        // one, which is the point of Test Mode
        if (TestRunConfig.Pending && TestRunConfig.Map != null) return TestRunConfig.Map;
#endif
        if (_selected != null) return _selected;

        var catalog = MapCatalog.Instance;
        if (catalog == null) return null;

        var remembered = catalog.Find(SaveManager.Data.lastPlayedMapId);
        if (remembered != null && MapProgressStore.IsUnlocked(remembered)) return remembered;

        return catalog.FirstUnlocked();
    }

    // Cleared when returning to camp so a stale pick can't outlive its menu
    public static void Clear()
    {
        _selected = null;
    }
}

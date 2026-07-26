using System.Collections.Generic;
using UnityEngine;

// The ordered roster of playable maps, in campaign order. Lives in Resources
// because two scenes need it without a shared wiring point: the camp's level
// select builds its panes from it, and the game scene's WaveManager resolves
// the chosen map id back to a MapDefinition on run start.
[CreateAssetMenu(fileName = "MapCatalog", menuName = "Maps/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    public const string ResourcePath = "MapCatalog";

    [Tooltip("Campaign order — this is the left-to-right order of the level select panes")]
    [SerializeField] private List<MapDefinition> maps = new List<MapDefinition>();

    private static MapCatalog _instance;

    public IReadOnlyList<MapDefinition> Maps => maps;

    public static MapCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<MapCatalog>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogWarning($"[MapCatalog] No catalog at Resources/{ResourcePath}. Level select will be empty.");
                }
            }
            return _instance;
        }
    }

    public MapDefinition Find(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i] != null && maps[i].MapId == mapId) return maps[i];
        }
        return null;
    }

    // First map the player is allowed into — the fallback when nothing is saved
    public MapDefinition FirstUnlocked()
    {
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i] != null && MapProgressStore.IsUnlocked(maps[i])) return maps[i];
        }
        return maps.Count > 0 ? maps[0] : null;
    }
}

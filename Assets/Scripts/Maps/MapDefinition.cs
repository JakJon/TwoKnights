using UnityEngine;
using System.Collections.Generic;

// A Map is the unit of content: a handcrafted wave setlist plus its bosses.
// The gate boss appears at gateBossWaveNumber; the FIRST kill ends the run in
// victory and unlocks unlocksMapId. On later runs the gate boss still appears,
// but beating it lets the run continue into the extended waves toward the true
// boss, which always ends the run in victory.
[CreateAssetMenu(fileName = "MapDefinition", menuName = "Maps/Map Definition")]
public class MapDefinition : ScriptableObject
{
    // A stretch of the map with its own backdrop, entered at fromWaveNumber.
    // Crossing into a stage that carries a ventureLine earns the full curtain
    // ceremony between waves (VentureCurtain); the backdrop itself is applied
    // by BackgroundController.
    [System.Serializable]
    public class MapStage
    {
        public string label;
        [Min(1)] public int fromWaveNumber = 1;
        public Sprite backdrop;
        [Tooltip("Shown over black between waves when the run first enters this stage. Empty = no ceremony, just the short fade.")]
        [TextArea] public string ventureLine;
    }

    [SerializeField] private string mapId = "camp_fields";
    [SerializeField] private string displayName = "The Camp Fields";
    [SerializeField] private bool unlockedByDefault = false;

    [Header("Level select")]
    [Tooltip("Pane artwork. Currently the map's own backdrop sprite; swap for dedicated key art when it exists.")]
    [SerializeField] private Sprite previewImage;
    [Tooltip("One-line flavour under the map name on the level select pane")]
    [SerializeField] private string tagline = "";
    [Tooltip("Shown in place of the tagline while this map is locked, e.g. 'Defeat the Rat King'. " +
             "Empty = derived from whichever map's gate boss opens this one.")]
    [SerializeField] private string lockedHint = "";

    [Header("Setlist (weighted pool, each plays once per run)")]
    [Tooltip("THIS is the map's pool — the only list the run draws from. A wave that is not here never plays on this map.")]
    [SerializeField] private List<BaseWave> waves = new List<BaseWave>();

    [Tooltip("Folders the 'Find Waves In Folders' context menu searches, e.g. Assets/Scripts/Waves/Cave/ChooChoo. " +
             "Authored per map so a search can never reach into another map's waves. Searching is recursive.")]
    [SerializeField] private List<string> waveFolders = new List<string>();

    [Header("Gate boss — the map's nominal end")]
    [SerializeField] private BaseWave gateBoss;
    [SerializeField] private int gateBossWaveNumber = 10;

    [Header("True boss — deeper waves after the gate has fallen")]
    [SerializeField] private BaseWave trueBoss;
    [SerializeField] private int trueBossWaveNumber = 20;

    [Header("Roster")]
    [Tooltip("First wave the stronger enemies may spawn on — dark bats, black wolves, brown and " +
             "black rats. -1 derives it from the gate boss, which is the Camp Fields rule: they are " +
             "staggered in behind the rat king. A map where they are ordinary residents sets 1.")]
    [SerializeField] private int strongerEnemiesFromWave = -1;

    [Header("Stages — deeper into the map")]
    [Tooltip("Backdrop phases keyed by the 1-based wave number they start on")]
    [SerializeField] private List<MapStage> stages = new List<MapStage>();

    [Header("Progression")]
    [Tooltip("mapId unlocked when this map's gate boss first falls (empty = none)")]
    [SerializeField] private string unlocksMapId = "";

    public string MapId => mapId;
    public string DisplayName => displayName;
    public bool UnlockedByDefault => unlockedByDefault;
    public Sprite PreviewImage => previewImage;
    public string Tagline => tagline;
    public string LockedHint => lockedHint;
    public IReadOnlyList<BaseWave> Waves => waves;
    public BaseWave GateBoss => gateBoss;
    public int GateBossWaveNumber => gateBossWaveNumber;
    public BaseWave TrueBoss => trueBoss;
    public int TrueBossWaveNumber => trueBossWaveNumber;

    /// <summary>
    /// First wave number the stronger enemies are allowed to spawn on. Kept as
    /// its own number rather than read off the gate boss: tying the two together
    /// made "hold the stronger enemies back" a rule of the GAME, when it is only
    /// how the Camp Fields staggers its roster in — and it silently pushed the
    /// Mine's stronger enemies out to wave 16 because the Mine's gate sits later.
    /// </summary>
    public int StrongerEnemiesFromWave =>
        strongerEnemiesFromWave >= 0 ? strongerEnemiesFromWave : gateBossWaveNumber + 1;
    public string UnlocksMapId => unlocksMapId;
    public IReadOnlyList<MapStage> Stages => stages;

    // The stage the run is in at the given 1-based wave number: the highest
    // fromWaveNumber at or below it. Null when the map declares no stages, or
    // when every stage starts later (a map whose first stage isn't wave 1).
    public MapStage StageForWave(int waveNumber)
    {
        MapStage best = null;
        for (int i = 0; i < stages.Count; i++)
        {
            MapStage stage = stages[i];
            if (stage == null || stage.fromWaveNumber > waveNumber) continue;
            if (best == null || stage.fromWaveNumber > best.fromWaveNumber)
                best = stage;
        }
        return best;
    }

#if UNITY_EDITOR
    // The per-map replacement for WaveManager's old "Auto-Find All Waves", which
    // searched the whole project and so pulled every map's waves into one list.
    //
    // It ADDS what it finds rather than replacing the list, and that is deliberate
    // twice over: a wave that lives outside the named folders (Stalkers.asset sits
    // loose at the Waves root) would otherwise be deleted by a search that simply
    // never looked where it lives, and an entry that failed to load reads as null
    // — a wholesale rebuild would drop it silently, which is the same disappearing
    // act this whole mechanism is meant to stop. Nulls are reported and left alone.
    [ContextMenu("Find Waves In Folders")]
    private void FindWavesInFolders()
    {
        if (waveFolders == null || waveFolders.Count == 0)
        {
            Debug.LogWarning($"[{name}] No wave folders listed, so there is nowhere to search. " +
                             "Add e.g. Assets/Scripts/Waves/Cave/ChooChoo to Wave Folders first.");
            return;
        }

        var folders = new List<string>();
        for (int i = 0; i < waveFolders.Count; i++)
        {
            string folder = waveFolders[i];
            if (string.IsNullOrWhiteSpace(folder)) continue;

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[{name}] '{folder}' is not a folder in this project — skipped.");
                continue;
            }
            folders.Add(folder);
        }
        if (folders.Count == 0) return;

        int nulls = 0;
        for (int i = 0; i < waves.Count; i++)
        {
            if (waves[i] == null) nulls++;
        }

        int added = 0;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BaseWave", folders.ToArray());
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var wave = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseWave>(path);
            if (wave == null || waves.Contains(wave)) continue;

            waves.Add(wave);
            added++;
            Debug.Log($"[{name}] Added {wave.name} to the setlist.");
        }

        if (added > 0) UnityEditor.EditorUtility.SetDirty(this);

        string nullNote = nulls > 0
            ? $" {nulls} existing entr{(nulls == 1 ? "y" : "ies")} could not be loaded and " +
              "were left in place — reimport this asset to resolve them."
            : "";
        Debug.Log($"[{name}] Searched {folders.Count} folder(s): added {added}, setlist now {waves.Count}.{nullNote}");
    }
#endif
}

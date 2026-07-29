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

    [Header("Setlist (weighted pool, each plays once per run)")]
    [SerializeField] private List<BaseWave> waves = new List<BaseWave>();

    [Header("Gate boss — the map's nominal end")]
    [SerializeField] private BaseWave gateBoss;
    [SerializeField] private int gateBossWaveNumber = 10;

    [Header("True boss — deeper waves after the gate has fallen")]
    [SerializeField] private BaseWave trueBoss;
    [SerializeField] private int trueBossWaveNumber = 20;

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
    public IReadOnlyList<BaseWave> Waves => waves;
    public BaseWave GateBoss => gateBoss;
    public int GateBossWaveNumber => gateBossWaveNumber;
    public BaseWave TrueBoss => trueBoss;
    public int TrueBossWaveNumber => trueBossWaveNumber;
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
}

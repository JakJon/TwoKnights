using UnityEngine;
using System.Collections.Generic;

// A Map is the unit of content: a handcrafted wave setlist plus its bosses.
// The gate boss appears at gateBossWaveNumber and the FIRST kill unlocks
// unlocksMapId. What that kill does to the RUN is the map's own business, and
// the two shipped maps answer it differently:
//
//   * The Camp Fields — the gate is a finish line. The first kill ends the run
//     in victory (gateBossEndsRun), and the rat king stands at his wave number
//     on every later run (gateBossRepeats), where beating him again is how a run
//     gets into the deep waves toward the true boss.
//   * The Mine — the gate is a door. Beating the Millstone records the clear and
//     the run walks straight through it; afterwards it is not forced again at all
//     and simply plays as an ordinary wave out of the setlist.
//
// The true boss always ends the run in victory.
[CreateAssetMenu(fileName = "MapDefinition", menuName = "Maps/Map Definition")]
public class MapDefinition : ScriptableObject
{
    // A stretch of the map with its own backdrop, entered at fromWaveNumber.
    // Crossing into a stage that carries a ventureLine earns the full curtain
    // ceremony between waves (VentureCurtain); the backdrop — and the optional
    // foreground that draws over the arena — are applied by BackgroundController,
    // which hands the canopy to ArenaLighting in the same breath.
    [System.Serializable]
    public class MapStage
    {
        public string label;
        [Min(1)] public int fromWaveNumber = 1;
        public Sprite backdrop;
        [Tooltip("Optional overlay drawn IN FRONT of every sprite in the arena — knights, enemies, projectiles. " +
                 "The near lip of a cave mouth, a rock overhang: the knights walk BEHIND it. Usually the second " +
                 "layer of the backdrop's .aseprite, imported with Layer Import Mode = Individual Layers. " +
                 "Empty = a flat backdrop with nothing in front.")]
        public Sprite foreground;
        [Tooltip("Optional light and shade laid over the arena — the canopy the Camp Fields fights under, " +
                 "thinning to a glade over the knights, or the slow breathing dark of the Mine. Drawn live " +
                 "by ArenaLighting rather than painted into the backdrop, so it falls on enemies and " +
                 "projectiles too and moves. Empty = the stage is lit by its backdrop art alone.")]
        public CanopyProfile canopy;
        [Tooltip("Shown over black between waves when the run first enters this stage. Empty = no ceremony, just the short fade.")]
        [TextArea] public string ventureLine;
    }

    // Which rats this map sends out, and in what order, from fromWaveNumber on.
    // Deliberately shaped like MapStage above and read the same way
    // (RatCadenceForWave mirrors StageForWave): the highest fromWaveNumber at or
    // below the current wave wins.
    //
    // This exists because every wave script used to pick its own rat prefab by
    // hand, and the old strength gate then quietly downgraded most of those picks
    // to grey — so black rats were reachable from exactly two assets in the
    // whole forest, and a player who had not beaten the rat king had never seen
    // one. Which rat walks on is a fact about how deep the run is. That is the
    // map's business, so the map says it, once, here.
    [System.Serializable]
    public class RatPhase
    {
        public string label;
        [Min(1)] public int fromWaveNumber = 1;
        [Tooltip("Rat types in order. Every SpawnRat that does not name a type outright takes " +
                 "the next one from here, cycling. The counter resets each wave, so a given wave " +
                 "always produces the same rats in the same order.")]
        public List<RatType> cadence = new List<RatType>();
    }

    [SerializeField] private string mapId = "camp_fields";
    [SerializeField] private string displayName = "The Camp Fields";
    [SerializeField] private bool unlockedByDefault = false;

    [Header("Level select")]
    [Tooltip("Pane artwork. Currently the map's own backdrop sprite; swap for dedicated key art when it exists.")]
    [SerializeField] private Sprite previewImage;
    [Tooltip("The blurb on the map's card in the level select — a sentence or two about the place.")]
    [TextArea] [SerializeField] private string description = "";
    [Tooltip("How many waves deep this map goes. The level select counts the deepest wave reached " +
             "against this, and earns the map's first star when the player gets there. It is NOT the " +
             "same as the true boss's wave number: the Camp Fields keeps playing past its Twins, and " +
             "this is the number that says how far past.")]
    [Min(1)] [SerializeField] private int finalWaveNumber = 30;
    [Tooltip("Colour of this map's mark on the file select — the small circle that appears on a file " +
             "once this map's TRUE boss has fallen. The file bar says nothing else about progress, so " +
             "the colour is the only thing identifying which map was finished; keep them far apart.")]
    [SerializeField] private Color completionMarkColor = new Color(0.72f, 0.68f, 0.58f, 1f);

    [Header("Setlist (weighted pool, each plays once per run)")]
    [Tooltip("THIS is the map's pool — the only list the run draws from. A wave that is not here never plays on this map.")]
    [SerializeField] private List<BaseWave> waves = new List<BaseWave>();

    [Tooltip("Folders the 'Find Waves In Folders' context menu searches, e.g. Assets/Scripts/Waves/Cave/ChooChoo. " +
             "Authored per map so a search can never reach into another map's waves. Searching is recursive.")]
    [SerializeField] private List<string> waveFolders = new List<string>();

    [Header("Gate boss — the map's nominal end")]
    [SerializeField] private BaseWave gateBoss;
    [SerializeField] private int gateBossWaveNumber = 10;

    [Tooltip("Does the gate keep standing across the wave number on every later run? The Camp Fields rule " +
             "is yes: the rat king is there at wave ten forever, and beating him again is how a run gets " +
             "into the deep waves. Turn it off for a gate that is a one-time door — it is forced until the " +
             "first time it falls and after that it is an ordinary wave, which only works if the gate wave " +
             "is ALSO in the map's setlist with a real weight and an unlock window.")]
    [SerializeField] private bool gateBossRepeats = true;

    [Tooltip("Does the first kill END the run in victory? The Camp Fields rule is yes — the gate is the " +
             "map's nominal finish line, and a first run stops there. Turn it off for a gate that is a " +
             "door rather than a finish line: it still marks the map's gate cleared, still pays out " +
             "whatever that unlocks, and the run walks straight through it and carries on.")]
    [SerializeField] private bool gateBossEndsRun = true;

    [Header("True boss — deeper waves after the gate has fallen")]
    [SerializeField] private BaseWave trueBoss;
    [SerializeField] private int trueBossWaveNumber = 20;

    [Tooltip("Does beating the true boss END the run in victory? Yes is the ordinary rule and " +
             "the default. The Camp Fields turns it off: the Twins are the deepest thing the " +
             "forest has NAMED, not the deepest thing it has, and the run carries on past them " +
             "into the moonlit waves until the knights fall. The kill still records the true " +
             "clear and still pays out whatever that unlocks either way.")]
    [SerializeField] private bool trueBossEndsRun = true;

    [Header("Roster")]
    [Tooltip("First wave the stronger enemies may spawn on — dark bats and black wolves. " +
             "-1 derives it from the gate boss, which is the Camp Fields rule: they are " +
             "staggered in behind the rat king. A map where they are ordinary residents sets 1. " +
             "Rats are NOT gated here any more — they follow ratPhases instead.")]
    [SerializeField] private int strongerEnemiesFromWave = -1;

    [Tooltip("Rat cadences by depth. Empty = grey rats only. The highest fromWaveNumber at " +
             "or below the current wave wins, exactly like the stage list.")]
    [SerializeField] private List<RatPhase> ratPhases = new List<RatPhase>();

    [Tooltip("First wave this map's DEEP roster replaces the ordinary one: the Camp Fields' " +
             "Moonlit bats and slimes, and grey wolves coming out black. -1 = this map has no " +
             "deep roster. Rats are NOT part of this — they have their own schedule in ratPhases.")]
    [SerializeField] private int deepRosterFromWave = -1;

    [Header("Stages — deeper into the map")]
    [Tooltip("Backdrop phases keyed by the 1-based wave number they start on")]
    [SerializeField] private List<MapStage> stages = new List<MapStage>();

    [Header("Music")]
    [Tooltip("Plays from wave 1 until the map goes deep. Empty = silence, which is the honest default for a map whose score has not been chosen yet.")]
    [SerializeField] private AudioClip explorationMusic;
    [Tooltip("Takes over once the map goes deep. Point this at the same clip as the exploration track for a map that should not change gear.")]
    [SerializeField] private AudioClip intenseMusic;
    [Tooltip("First wave the intense track plays on. Match this to the deep STAGE'S fromWaveNumber — the backdrop darkening and the music changing gear are one moment, and splitting them reads as a bug in whichever one lands second.")]
    [Min(1)] [SerializeField] private int intenseMusicFromWave = 11;
    [Tooltip("Plays on the gate boss and the true boss. Empty = the boss fights under whatever the depth track is.")]
    [SerializeField] private AudioClip bossMusic;

    [Header("Progression")]
    [Tooltip("mapId unlocked when this map's gate boss first falls (empty = none)")]
    [SerializeField] private string unlocksMapId = "";

    public string MapId => mapId;
    public string DisplayName => displayName;
    public bool UnlockedByDefault => unlockedByDefault;
    public Sprite PreviewImage => previewImage;

    /// <summary>The map card's blurb on the level select.</summary>
    public string Description => description;

    /// <summary>
    /// The deepest wave this map has, which the level select shows the player's
    /// furthest run against. See the field's tooltip for why this is authored
    /// rather than read off the true boss.
    /// </summary>
    public int FinalWaveNumber => finalWaveNumber;

    /// <summary>Colour of this map's completion circle on the file select. See the field's tooltip.</summary>
    public Color CompletionMarkColor => completionMarkColor;
    public AudioClip ExplorationMusic => explorationMusic;
    public AudioClip IntenseMusic => intenseMusic;
    public int IntenseMusicFromWave => intenseMusicFromWave;
    public AudioClip BossMusic => bossMusic;

    /// <summary>
    /// Is this one of the map's two named fights? Asked with the wave the run is
    /// about to play rather than with a wave NUMBER, because the scheduler — not
    /// the number — decides when a boss is on: a gate that has already fallen this
    /// run does not come back on its wave number, and Test Mode can put a boss
    /// anywhere at all.
    /// </summary>
    public bool IsBossWave(BaseWave wave)
    {
        if (wave == null) return false;
        return wave == gateBoss || wave == trueBoss;
    }

    /// <summary>
    /// What should be playing for this wave. Bosses win over depth, and depth wins
    /// over the opening track. Falls back rather than returning null when a slot is
    /// empty, so a map with only an exploration track keeps playing it through a
    /// boss instead of dropping to silence at the loudest moment of the run.
    /// </summary>
    public AudioClip MusicForWave(int waveNumber, bool isBossWave)
    {
        if (isBossWave && bossMusic != null) return bossMusic;
        if (waveNumber >= intenseMusicFromWave && intenseMusic != null) return intenseMusic;
        return explorationMusic != null ? explorationMusic : intenseMusic;
    }

    public IReadOnlyList<BaseWave> Waves => waves;
    public BaseWave GateBoss => gateBoss;
    public int GateBossWaveNumber => gateBossWaveNumber;

    /// <summary>
    /// Is the gate put back for every run, or is it a door that stays open once
    /// it has been walked through? See the field's tooltip — a one-time gate has
    /// to be in the setlist as well, or it is never seen again after its first
    /// clear.
    /// </summary>
    public bool GateBossRepeats => gateBossRepeats;

    /// <summary>
    /// Does beating the gate for the first time end the run in victory? False
    /// makes it a doorway: the clear is recorded and paid for exactly as before,
    /// but the run does not stop at it.
    /// </summary>
    public bool GateBossEndsRun => gateBossEndsRun;
    public BaseWave TrueBoss => trueBoss;
    public int TrueBossWaveNumber => trueBossWaveNumber;

    /// <summary>
    /// Does the true boss falling end the run? See the field's tooltip — false makes it
    /// the deepest NAMED fight rather than the last one, and the run plays on past it.
    /// </summary>
    public bool TrueBossEndsRun => trueBossEndsRun;

    /// <summary>
    /// First wave number the stronger enemies are allowed to spawn on. Kept as
    /// its own number rather than read off the gate boss: tying the two together
    /// made "hold the stronger enemies back" a rule of the GAME, when it is only
    /// how the Camp Fields staggers its roster in — and it silently pushed the
    /// Mine's stronger enemies out to wave 16 because the Mine's gate sits later.
    /// </summary>
    public int StrongerEnemiesFromWave =>
        strongerEnemiesFromWave >= 0 ? strongerEnemiesFromWave : gateBossWaveNumber + 1;

    /// <summary>
    /// First wave this map's deep roster is live on: Moonlit bats and slimes, and
    /// grey wolves coming out black. -1 (the default) means the map has none, and
    /// nothing is ever substituted. Rats are deliberately NOT here — they run on
    /// ratPhases, which turns over on its own schedule and on both maps.
    /// </summary>
    public int DeepRosterFromWave => deepRosterFromWave;

    /// <summary>
    /// The rat cadence in force at the given 1-based wave number: the phase with
    /// the highest fromWaveNumber at or below it. Same shape as StageForWave.
    /// Returns null when the map declares no phases, or when every phase starts
    /// later — callers fall back to a plain grey rat.
    /// </summary>
    public IReadOnlyList<RatType> RatCadenceForWave(int waveNumber)
    {
        RatPhase best = null;
        for (int i = 0; i < ratPhases.Count; i++)
        {
            RatPhase phase = ratPhases[i];
            if (phase == null || phase.fromWaveNumber > waveNumber) continue;
            if (phase.cadence == null || phase.cadence.Count == 0) continue;
            if (best == null || phase.fromWaveNumber > best.fromWaveNumber)
                best = phase;
        }
        return best != null ? best.cadence : null;
    }
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

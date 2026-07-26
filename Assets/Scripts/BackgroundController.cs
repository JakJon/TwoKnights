using System.Collections.Generic;
using UnityEngine;

// Swaps the arena backdrop as the run pushes deeper into the map (forest ->
// deep forest once the rat king falls on wave 10), and across maps (the mine
// runs on the cave backdrop). Stages are keyed by the 1-based wave number they
// start on and optionally by map id; the highest matching stage at or below the
// current wave wins. Polls the WaveManager instead of hooking wave events so
// Test Mode jumps and run restarts are picked up automatically.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundController : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        public string label;
        [Tooltip("Map this stage belongs to. Empty = any map.")]
        public string mapId;
        [Min(1)] public int fromWaveNumber = 1;
        public Sprite sprite;
    }

    [SerializeField] private List<Stage> stages = new List<Stage>();

    private SpriteRenderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        var waveManager = WaveManager.ActiveInstance;
        if (waveManager == null) return;

        var map = waveManager.CurrentMap;
        string mapId = map != null ? map.MapId : null;

        Stage best = null;
        for (int i = 0; i < stages.Count; i++)
        {
            Stage stage = stages[i];
            if (stage.sprite == null || stage.fromWaveNumber > waveManager.CurrentWaveNumber)
                continue;
            if (!string.IsNullOrEmpty(stage.mapId) && stage.mapId != mapId)
                continue;
            // A stage naming this map beats a wildcard one, whatever the wave
            bool betterMatch = best == null
                || (!string.IsNullOrEmpty(stage.mapId) && string.IsNullOrEmpty(best.mapId))
                || (string.IsNullOrEmpty(stage.mapId) == string.IsNullOrEmpty(best.mapId)
                    && stage.fromWaveNumber > best.fromWaveNumber);
            if (betterMatch) best = stage;
        }

        if (best != null && _renderer.sprite != best.sprite)
        {
            _renderer.sprite = best.sprite;
        }
    }
}

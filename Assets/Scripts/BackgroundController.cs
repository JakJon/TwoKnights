using System.Collections.Generic;
using UnityEngine;

// Swaps the arena backdrop as the run pushes deeper into the map (forest ->
// deep forest once the rat king falls on wave 10). Stages are keyed by the
// 1-based wave number they start on; the highest stage at or below the
// current wave wins. Polls the WaveManager instead of hooking wave events so
// Test Mode jumps and run restarts are picked up automatically.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundController : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        public string label;
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

        Stage best = null;
        for (int i = 0; i < stages.Count; i++)
        {
            Stage stage = stages[i];
            if (stage.sprite == null || stage.fromWaveNumber > waveManager.CurrentWaveNumber)
                continue;
            if (best == null || stage.fromWaveNumber > best.fromWaveNumber)
                best = stage;
        }

        if (best != null && _renderer.sprite != best.sprite)
        {
            _renderer.sprite = best.sprite;
        }
    }
}

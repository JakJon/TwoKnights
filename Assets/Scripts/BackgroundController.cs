using UnityEngine;

// Applies the arena backdrop for the stage the run is currently in (forest ->
// deep forest once the rat king falls on wave 10, the cave on the mine). The
// stages themselves live on the MapDefinition asset, so authoring a new one
// never means touching this scene.
//
// Polls the WaveManager instead of hooking wave events so Test Mode jumps and
// run restarts are picked up automatically. Between waves the Spawner calls
// Hold() so the swap waits behind the black curtain instead of popping on the
// cleared arena — CurrentWaveNumber advances the instant WaveCompleted() runs.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundController : MonoBehaviour
{
    public static BackgroundController Instance { get; private set; }

    private SpriteRenderer _renderer;
    private bool _held;

    void Awake()
    {
        Instance = this;
        _renderer = GetComponent<SpriteRenderer>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_held) return;
        ApplyCurrentStage();
    }

    // Freeze the backdrop on whatever it is showing now. Safe to call twice.
    public void Hold()
    {
        _held = true;
    }

    // Resume polling and swap immediately, so the caller can be sure the new
    // backdrop is up before it lifts the curtain
    public void ReleaseAndApply()
    {
        _held = false;
        ApplyCurrentStage();
    }

    private void ApplyCurrentStage()
    {
        var waveManager = WaveManager.ActiveInstance;
        var map = waveManager != null ? waveManager.CurrentMap : null;
        if (map == null) return;

        var stage = map.StageForWave(waveManager.CurrentWaveNumber);
        if (stage == null || stage.backdrop == null) return;

        if (_renderer.sprite != stage.backdrop)
            _renderer.sprite = stage.backdrop;
    }
}

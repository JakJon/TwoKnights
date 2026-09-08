using UnityEngine;

// Applies the arena backdrop for the stage the run is currently in (forest ->
// deep forest once the rat king falls on wave 10, the mine -> the deep mine once
// the millstone has been beaten). The stages themselves live on the
// MapDefinition asset, so authoring a new one never means touching this scene.
//
// A stage may also carry a FOREGROUND: a second sprite drawn over every other
// sprite in the arena, so the knights fight behind the near lip of the cave
// mouth rather than on top of it. It rides a child renderer this component
// builds itself — nothing in the scene needs wiring, and a stage without one
// simply leaves that renderer switched off.
//
// Polls the WaveManager instead of hooking wave events so Test Mode jumps and
// run restarts are picked up automatically. Between waves the Spawner calls
// Hold() so the swap waits behind the black curtain instead of popping on the
// cleared arena — CurrentWaveNumber advances the instant WaveCompleted() runs.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundController : MonoBehaviour
{
    public static BackgroundController Instance { get; private set; }

    // Clear of every sorting order the game hands out in the world: the highest
    // is the bomb blast at 25 (BombFx), and prefabs top out at 12. On the
    // Default layer rather than Background, because Background as a whole
    // draws under Default and no order within it could ever reach the arena.
    private const string ForegroundSortingLayer = "Default";
    private const int ForegroundSortingOrder = 100;

    private SpriteRenderer _renderer;
    private SpriteRenderer _foreground;
    private bool _held;

    void Awake()
    {
        Instance = this;
        _renderer = GetComponent<SpriteRenderer>();
        _foreground = BuildForegroundRenderer();
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
        if (stage == null) return;

        // A stage that names no backdrop keeps the one already up — the map is
        // saying nothing about the backdrop, not asking for a blank arena
        if (stage.backdrop != null && _renderer.sprite != stage.backdrop)
            _renderer.sprite = stage.backdrop;

        ApplyForeground(stage.foreground);
        ApplyLighting(stage.canopy);
    }

    // The stage's light and shade, swapped in lockstep with its backdrop — both
    // are the same decision about how this stretch of the map looks, and Hold()
    // has to keep them behind the same curtain.
    //
    // The rig builds itself on first use, so a map that never asks for lighting
    // (the Mine) never gets one made.
    private void ApplyLighting(CanopyProfile canopy)
    {
        if (canopy == null && ArenaLighting.Instance == null) return;
        ArenaLighting.Ensure().Apply(canopy);
    }

    private void ApplyForeground(Sprite sprite)
    {
        if (_foreground == null) return;

        if (_foreground.sprite != sprite) _foreground.sprite = sprite;
        _foreground.enabled = sprite != null;
    }

    // Built here rather than authored in the scene: the backdrop object is an
    // instance of an .aseprite's generated model prefab, so a hand-added child
    // would be a prefab override that the next reimport could argue with.
    //
    // Shares the backdrop's material on purpose. That material is lit, and the
    // one global light is what VentureCurtain rides down to black — a foreground
    // on an unlit material would hang there glowing while the arena goes dark.
    private SpriteRenderer BuildForegroundRenderer()
    {
        var holder = new GameObject("BackdropForeground");
        // False, not true: the child wants the backdrop's own transform, since
        // both sprites are the same canvas at the same pivot and only line up
        // when they sit exactly on top of each other
        holder.transform.SetParent(transform, false);

        var foreground = holder.AddComponent<SpriteRenderer>();
        foreground.sharedMaterial = _renderer.sharedMaterial;
        foreground.sortingLayerName = ForegroundSortingLayer;
        foreground.sortingOrder = ForegroundSortingOrder;
        foreground.enabled = false;
        return foreground;
    }
}

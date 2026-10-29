using UnityEngine;

// Applies the arena backdrop for the stage the run is currently in (forest ->
// deep forest once the rat king falls on wave 10, the mine -> the deep mine once
// the millstone has been beaten). The stages themselves live on the
// MapDefinition asset, so authoring a new one never means touching this scene.
//
// A stage may also carry a FOREGROUND: a second sprite laid over the backdrop.
// The stage says which way it faces the fight. The Deep Mine's rim and rocks
// sit UNDER every game sprite — the track, the carts, the knights and the
// enemies all draw on top of them. The forest's tree canopy sits OVER the
// knights and enemies, so they walk underneath it. It rides a child renderer
// this component builds itself — nothing in the scene needs wiring, and a
// stage without one simply leaves that renderer switched off.
//
// A stage may also make the gems and ore in its art glow (GemGlow, another
// child built here). It is painted from whichever backdrop and foreground are
// up, so it changes stage in the same breath they do.
//
// Polls the WaveManager instead of hooking wave events so Test Mode jumps and
// run restarts are picked up automatically. Between waves the Spawner calls
// Hold() so the swap waits behind the black curtain instead of popping on the
// cleared arena — CurrentWaveNumber advances the instant WaveCompleted() runs.
//
// THE STAGE MAY ONLY TURN OVER OUT OF SIGHT. Hold()/ReleaseAndApply() is the
// Spawner saying when that is, but the poll below enforces it on its own account
// as well: once a stage is up, the backdrop will not change again unless the
// venture curtain is black. Crossing into the deep forest is a reveal the game
// spends a whole ceremony on — a line of prose over black, then the light coming
// back up on somewhere new — and every way it has ever been spoiled has been some
// path that let CurrentWaveNumber move while the player was still looking at the
// arena. The first application is exempt: a run always begins by putting its own
// map's stage up, and nothing has been revealed yet.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundController : MonoBehaviour
{
    public static BackgroundController Instance { get; private set; }

    // A foreground UNDER the arena (the Deep Mine's rim): just above the
    // backdrop (Background/0) and below the lowest game sprite on that layer,
    // the track at Background/10. Everything on Default draws over the whole
    // Background layer, so the arena is above it too.
    private const string UnderSortingLayer = "Background";
    private const int UnderSortingOrder = 5;

    // A foreground OVER the arena (the forest canopy): clear of the highest
    // order the fight hands out — the bomb blast's ring at 26 (BombFx) — and
    // under the canopy light's actor pass at Default/200, so the trees take the
    // same shade as everything walking beneath them. Quest NPCs and their text
    // (Default/500 and up) still draw over it.
    private const string OverSortingLayer = "Default";
    private const int OverSortingOrder = 100;

    private SpriteRenderer _renderer;
    private SpriteRenderer _foreground;
    private GemGlow _gemGlow;
    private bool _held;

    /// <summary>
    /// Whether a stage has been put up at least once. Until it has, the poll applies
    /// freely — that first pass is what dresses the arena for the map the run is on,
    /// and it is the same pass a Test Mode jump or a restart rides in on, because
    /// both of those go through a scene load and so through a fresh component.
    /// </summary>
    private bool _applied;

    void Awake()
    {
        Instance = this;
        _renderer = GetComponent<SpriteRenderer>();
        _foreground = BuildForegroundRenderer();
        _gemGlow = GemGlow.Build(transform);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_held) return;
        // Dressing the arena for the first time is always allowed; turning it over
        // afterwards is allowed only behind black. See the header.
        if (_applied && !VentureCurtain.IsBlack) return;
        ApplyCurrentStage();
    }

    // Freeze the backdrop on whatever it is showing now. Safe to call twice.
    public void Hold()
    {
        _held = true;
    }

    // Resume polling and swap immediately, so the caller can be sure the new
    // backdrop is up before it lifts the curtain. Only ever called from behind
    // black — everywhere else wants Release().
    public void ReleaseAndApply()
    {
        _held = false;
        ApplyCurrentStage();
    }

    /// <summary>
    /// Drops the hold WITHOUT swapping. For the paths that only need to stop
    /// freezing the backdrop — a run ending, a quit to camp — where the arena is
    /// still lit and in front of the player, and applying would be exactly the pop
    /// the hold existed to prevent. The clearest case is a first gate clear: the
    /// boss kill has already moved CurrentWaveNumber into the next stage, so a swap
    /// here puts the deep forest up behind the victory banner and spoils the reveal
    /// on the very run that earned it.
    /// </summary>
    public void Release()
    {
        _held = false;
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

        ApplyForeground(stage.foreground, stage.foregroundOverArena);
        ApplyLighting(stage.canopy);
        _gemGlow.Apply(stage.gemGlow, _renderer.sprite, _foreground.enabled ? _foreground.sprite : null);
        _applied = true;
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

    private void ApplyForeground(Sprite sprite, bool overArena)
    {
        if (_foreground == null) return;

        if (_foreground.sprite != sprite) _foreground.sprite = sprite;
        _foreground.enabled = sprite != null;

        string layer = overArena ? OverSortingLayer : UnderSortingLayer;
        if (_foreground.sortingLayerName != layer) _foreground.sortingLayerName = layer;
        _foreground.sortingOrder = overArena ? OverSortingOrder : UnderSortingOrder;
    }

    // Built here rather than authored in the scene, so a stage can gain a
    // foreground without anyone touching Main.unity.
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
        foreground.sortingLayerName = UnderSortingLayer;
        foreground.sortingOrder = UnderSortingOrder;
        foreground.enabled = false;
        return foreground;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

// The between-wave curtain. The arena's light dies while a grid of black tiles
// eats the frame on an ordered-dither pattern; the upgrade menu then plays out
// over that black, a line of prose marks a stage crossing, and the light
// returns on the new backdrop.
//
// Lives in the PlayerHUD document (sortingOrder -10), so it covers the arena
// and the HUD while the upgrade menu (0) and the death/quest panels (10) still
// draw on top of it. Like BossHealthBar it resolves itself lazily and needs no
// scene wiring beyond the UXML/USS.
//
// Every duration here is unscaled and the tile stagger rides the panel
// scheduler: the Spawner freezes timeScale for the whole transition, so
// WaitForSeconds and Time.deltaTime would both stall.
public static class VentureCurtain
{
    // Grid matches the 640x360 reference frame at 20px tiles
    private const int Cols = 32;
    private const int Rows = 18;

    // Ordered-dither thresholds. Deterministic by design — wave content and its
    // presentation never roll dice.
    private static readonly int[,] Bayer4 =
    {
        {  0,  8,  2, 10 },
        { 12,  4, 14,  6 },
        {  3, 11,  1,  9 },
        { 15,  7, 13,  5 }
    };

    // These four must stay in step with PlayerHUD.uss
    private const float TileFade = 0.22f;   // .venture-tile transition-duration
    private const float QuickFade = 0.30f;  // .venture-fill transition-duration
    private const float SealFade = 0.06f;   // .venture-fill--instant
    private const float LineFade = 0.80f;   // .venture-line transition-duration

    private const float CeremonialSpread = 0.62f; // first tile to last tile
    private const float LineHold = 1.8f;

    private const string FillOnClass = "venture-fill--on";
    private const string FillInstantClass = "venture-fill--instant";
    private const string TileOnClass = "venture-tile--on";
    private const string LineVisibleClass = "venture-line--visible";

    private static UIDocument _document;
    private static VisualElement _curtain;
    private static VisualElement _fill;
    private static VisualElement _tileGrid;
    private static Label _line;
    private static VisualElement[] _tiles;

    private static Light2D _globalLight;
    private static float _baseIntensity = 1f;

    // Bumped by every Close/Open/ForceClear so scheduled tile flips and waits
    // left over from an abandoned transition (a death, a quit to camp) can tell
    // that they no longer own the screen
    private static int _epoch;

    public static bool IsBlack { get; private set; }

    /// <summary>
    /// Takes the screen to black. Ceremonial closes get the dither dissolve and
    /// a slower light death; ordinary wave gaps get a short straight fade.
    /// </summary>
    public static IEnumerator Close(bool ceremonial)
    {
        if (!TryResolve()) yield break;
        int epoch = ++_epoch;

        _curtain.style.display = DisplayStyle.Flex;
        _line.style.display = DisplayStyle.None;
        _line.RemoveFromClassList(LineVisibleClass);
        _fill.RemoveFromClassList(FillOnClass);
        _fill.RemoveFromClassList(FillInstantClass);

        if (!ceremonial)
        {
            _tileGrid.style.display = DisplayStyle.None;
            // A frame of layout so the transition has a from-value to animate
            yield return null;
            if (_epoch != epoch) yield break;

            _fill.AddToClassList(FillOnClass);
            yield return DriveLight(_baseIntensity, 0f, QuickFade, QuickFade);
            IsBlack = true;
            yield break;
        }

        _tileGrid.style.display = DisplayStyle.Flex;
        for (int i = 0; i < _tiles.Length; i++)
            _tiles[i].RemoveFromClassList(TileOnClass);

        yield return null;
        if (_epoch != epoch) yield break;

        ScheduleTiles(epoch, on: true);

        // The light dies over the dissolve's spread, so the world is already
        // black by the time the last tiles land
        yield return DriveLight(_baseIntensity, 0f, CeremonialSpread, CeremonialSpread + TileFade);
        if (_epoch != epoch) yield break;

        // Seal the hairlines between tiles. Everything is black at this instant,
        // so swapping the grid out for a solid fill is invisible.
        _fill.AddToClassList(FillInstantClass);
        _fill.AddToClassList(FillOnClass);
        yield return new WaitForSecondsRealtime(SealFade + 0.02f);
        if (_epoch != epoch) yield break;

        _tileGrid.style.display = DisplayStyle.None;
        IsBlack = true;
    }

    /// <summary>
    /// Fades a line of prose in over the black, holds it, and fades it out.
    /// Assumes Close already ran.
    /// </summary>
    public static IEnumerator ShowLine(string text)
    {
        if (!TryResolve() || string.IsNullOrEmpty(text)) yield break;
        int epoch = _epoch;

        _line.text = text;
        _line.style.display = DisplayStyle.Flex;
        _line.RemoveFromClassList(LineVisibleClass);

        yield return null;
        if (_epoch != epoch) yield break;

        _line.AddToClassList(LineVisibleClass);
        yield return new WaitForSecondsRealtime(LineFade + LineHold);
        if (_epoch != epoch) yield break;

        _line.RemoveFromClassList(LineVisibleClass);
        yield return new WaitForSecondsRealtime(LineFade);
        if (_epoch != epoch) yield break;

        _line.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Gives the screen back: the dissolve runs in reverse and the light
    /// returns to the intensity the scene shipped with.
    /// </summary>
    public static IEnumerator Open(bool ceremonial)
    {
        if (!TryResolve()) yield break;
        int epoch = ++_epoch;

        _curtain.style.display = DisplayStyle.Flex;
        _line.style.display = DisplayStyle.None;

        if (!ceremonial)
        {
            _tileGrid.style.display = DisplayStyle.None;
            _fill.RemoveFromClassList(FillInstantClass);
            _fill.RemoveFromClassList(FillOnClass);
            yield return DriveLight(0f, _baseIntensity, QuickFade, QuickFade);
            if (_epoch != epoch) yield break;

            _curtain.style.display = DisplayStyle.None;
            IsBlack = false;
            yield break;
        }

        // Hand the black over from the solid fill to the tiles before dissolving
        _tileGrid.style.display = DisplayStyle.Flex;
        for (int i = 0; i < _tiles.Length; i++)
            _tiles[i].AddToClassList(TileOnClass);

        yield return null;
        if (_epoch != epoch) yield break;

        _fill.RemoveFromClassList(FillOnClass);
        yield return new WaitForSecondsRealtime(SealFade + 0.02f);
        if (_epoch != epoch) yield break;

        _fill.RemoveFromClassList(FillInstantClass);
        ScheduleTiles(epoch, on: false);

        yield return DriveLight(0f, _baseIntensity, CeremonialSpread, CeremonialSpread + TileFade);
        if (_epoch != epoch) yield break;

        _tileGrid.style.display = DisplayStyle.None;
        _curtain.style.display = DisplayStyle.None;
        IsBlack = false;
    }

    /// <summary>
    /// Instantly gives the screen back and abandons any transition in flight.
    /// Death and quit-to-camp both bail out mid-curtain, and neither may leave
    /// the player staring at black.
    /// </summary>
    public static void ForceClear()
    {
        _epoch++;
        IsBlack = false;

        if (_globalLight != null) _globalLight.intensity = _baseIntensity;
        if (_curtain == null) return;

        _curtain.style.display = DisplayStyle.None;
        _tileGrid.style.display = DisplayStyle.None;
        _fill.RemoveFromClassList(FillOnClass);
        _fill.RemoveFromClassList(FillInstantClass);
        _line.RemoveFromClassList(LineVisibleClass);
        _line.style.display = DisplayStyle.None;
    }

    // Staggers every tile by its dither threshold. Turning off walks the same
    // matrix backwards, so the frame reopens in the order it closed.
    private static void ScheduleTiles(int epoch, bool on)
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Cols; x++)
            {
                int threshold = Bayer4[y & 3, x & 3];
                if (!on) threshold = 15 - threshold;

                VisualElement tile = _tiles[y * Cols + x];
                long delayMs = (long)(threshold / 16f * CeremonialSpread * 1000f);
                tile.schedule.Execute(() =>
                {
                    if (_epoch != epoch) return;
                    if (on) tile.AddToClassList(TileOnClass);
                    else tile.RemoveFromClassList(TileOnClass);
                }).StartingIn(delayMs);
            }
        }
    }

    // Rides the global light from one intensity to another over lightDuration,
    // then idles out the rest of totalDuration so the caller can wait on the
    // slower of the two (light vs dissolve) in one statement.
    private static IEnumerator DriveLight(float from, float to, float lightDuration, float totalDuration)
    {
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_globalLight != null && lightDuration > 0f)
                _globalLight.intensity = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / lightDuration));
            yield return null;
        }

        if (_globalLight != null) _globalLight.intensity = to;
    }

    // The HUD document is found lazily and re-resolved after scene loads;
    // rootVisualElement can also be null briefly after a UXML reimport
    private static bool TryResolve()
    {
        if (_document != null && _curtain != null && _globalLight != null) return true;

        if (_document == null)
        {
            _curtain = null;
            var hudObject = GameObject.Find("PlayerHUD");
            _document = hudObject != null ? hudObject.GetComponent<UIDocument>() : null;
        }

        var root = _document != null ? _document.rootVisualElement : null;
        if (root == null) return false;

        if (_curtain == null)
        {
            _curtain = root.Q<VisualElement>("venture-curtain");
            _fill = root.Q<VisualElement>("venture-fill");
            _tileGrid = root.Q<VisualElement>("venture-tiles");
            _line = root.Q<Label>("venture-line");
            if (_curtain == null || _fill == null || _tileGrid == null || _line == null)
            {
                _curtain = null;
                return false;
            }
            BuildTiles();
        }

        if (_globalLight == null)
        {
            // The one global light in the scene: a Multiply blend covering the
            // Default and Background sorting layers, so intensity 0 takes the
            // arena and the backdrop to true black while UI stays lit
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                _globalLight = light;
                _baseIntensity = light.intensity;
                break;
            }
        }

        // A scene with no global light still gets the dissolve — the world just
        // doesn't darken underneath it
        return _curtain != null;
    }

    // Rows of flex-grow children rather than percentage widths: the layout
    // divides exactly, with no rounding gap at the right edge of the grid
    private static void BuildTiles()
    {
        _tileGrid.Clear();
        _tiles = new VisualElement[Cols * Rows];

        for (int y = 0; y < Rows; y++)
        {
            var row = new VisualElement();
            row.AddToClassList("venture-tile-row");
            row.pickingMode = PickingMode.Ignore;

            for (int x = 0; x < Cols; x++)
            {
                var tile = new VisualElement();
                tile.AddToClassList("venture-tile");
                tile.pickingMode = PickingMode.Ignore;
                row.Add(tile);
                _tiles[y * Cols + x] = tile;
            }

            _tileGrid.Add(row);
        }
    }
}

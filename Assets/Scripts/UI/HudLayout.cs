using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// How big the knights' bars are, where they sit and how see-through they are,
// as five settings the player owns (owner, 2026-10-14 and -16). Size, inward,
// upward and transparency act on each knight's whole block together: the
// streak line, both bars and the numbers beside them. Width acts on the bars
// alone.
//
//   Size    how big the block is drawn, grown out of its own corner of the screen
//   Width   how long the two bars are, 1 = as built, 2 = twice as long. Nothing
//           else changes: not the text, not the bars' height (see PixelBar)
//   Inward  how far it moves in from its side, 0 = at the edge as built, 1 = the
//           two blocks nearly meeting in the middle
//   Upward  how far it moves up, 0 = at the bottom as built, 1 = at the top
//   Transparency  0 = solid as built, 1 = gone. The boss bar is left alone.
//
// Inward and upward are fractions of the room there is, not distances, so no
// setting can push a block off the screen whatever its size, and the two
// knights' blocks always stay mirror images of each other.
//
// Kept in PlayerPrefs, for the same reason the volume is (see AudioManager):
// it belongs to the screen the game is played on, not to the save file.
//
// HealthBar hands the HUD in once it has found it (Attach). The settings panel
// changes the values, and the live HUD, if a run is on, moves at once.
public static class HudLayout
{
    private const string SizeKey = "hud.barSize";
    private const string InwardKey = "hud.barInward";
    private const string UpwardKey = "hud.barUpward";
    private const string WidthKey = "hud.barWidth";
    private const string TransparencyKey = "hud.barTransparency";

    // Half size to double
    private const float MinSize = 0.5f;
    private const float MaxSize = 2f;

    // One notch of the stick on the two position rows
    public const float PositionStep = 0.05f;

    // The bars as built to twice that, a tenth at a time
    public const float MaxBarWidth = 2f;
    private const float BarWidthStep = 0.1f;

    /// <summary>The bars' length changed. Every PixelBar listens and repaints itself at the new length.</summary>
    public static event System.Action BarWidthChanged;

    // A knight's block at size 1, in HUD pixels: the bar and the widest number
    // beside it ("1000 / 1000", 3px letter spacing), then the streak line over
    // both bars. Fixed rather than measured, so a block does not shift every
    // time a number or the streak changes size.
    private const float BlockWidth = 270f;
    private const float BlockHeight = 70f;

    // Kept clear between the two blocks when both are pushed all the way in
    private const float MiddleGap = 12f;

    // The HUD's own margins (.player-hud's padding): 3% at the sides, 2.2% at
    // the foot, both of the HUD's width. The top gets the foot's.
    private const float SideMargin = 0.03f;
    private const float FootMargin = 0.022f;

    private static bool _loaded;
    private static bool _dirty;
    private static float _size = 1f;
    private static float _inward;
    private static float _upward;
    private static float _barWidth = 1f;
    private static float _transparency;

    private static VisualElement _hud;
    private static VisualElement _left;
    private static VisualElement _right;

    /// <summary>The size the bars are drawn at, snapped to one this screen draws cleanly (see <see cref="SizeSteps"/>).</summary>
    public static float Size
    {
        get
        {
            Load();
            var steps = SizeSteps();
            return steps[NearestStep(steps, _size)];
        }
    }

    public static float Inward
    {
        get { Load(); return _inward; }
    }

    public static float Upward
    {
        get { Load(); return _upward; }
    }

    /// <summary>How see-through the knights' blocks are, 0 (solid) to 1 (gone).</summary>
    public static float Transparency
    {
        get { Load(); return _transparency; }
    }

    public static bool SetTransparency(float value)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, _transparency)) return false;
        _transparency = value;
        PlayerPrefs.SetFloat(TransparencyKey, _transparency);
        _dirty = true;
        Apply();
        return true;
    }

    /// <summary>How long the bars are, 1 (as built) to <see cref="MaxBarWidth"/>.</summary>
    public static float BarWidth
    {
        get { Load(); return _barWidth; }
    }

    /// <summary>The bars' length in art pixels. Whole pixels, so the bar's pixels stay square.</summary>
    public static int BarPixels => Mathf.RoundToInt(PixelBar.Width * BarWidth);

    /// <summary>A tenth longer or shorter. False when already at that end.</summary>
    public static bool StepBarWidth(int direction)
    {
        Load();
        float next = Mathf.Clamp(Mathf.Round((_barWidth + direction * BarWidthStep) / BarWidthStep) * BarWidthStep,
                                 1f, MaxBarWidth);
        if (Mathf.Approximately(next, _barWidth)) return false;

        _barWidth = next;
        PlayerPrefs.SetFloat(WidthKey, _barWidth);
        _dirty = true;
        BarWidthChanged?.Invoke();
        Apply();
        return true;
    }

    /// <summary>A run's HUD is on screen to be moved. The settings panel only turns see-through when it is.</summary>
    public static bool HasLiveHud => _hud != null && _hud.panel != null;

    /// <summary>
    /// Every size the bars can be drawn at on this screen, smallest first. Each
    /// one puts an art pixel on a whole number of screen pixels, so the bar's
    /// pixels stay square and even: at a size in between, some columns of the
    /// bar would come out a screen pixel wider than the rest. At 1080p that is
    /// 67%, 100%, 133%, 167% and 200%. 100%, the size it was built at, is always
    /// on the list, even on a screen where it is not a whole number.
    /// </summary>
    public static List<float> SizeSteps()
    {
        // Screen pixels per art pixel at size 1. The HUD panel is 1200 wide and
        // follows the screen's width, and an art pixel is 1.875 of its pixels,
        // so that is the screen's width over 640: 3 at 1080p.
        float basePixels = Mathf.Max(1f, Screen.width / 640f);

        var steps = new List<float>();
        int smallest = Mathf.Max(1, Mathf.CeilToInt(basePixels * MinSize - 0.001f));
        int largest = Mathf.FloorToInt(basePixels * MaxSize + 0.001f);
        bool hasOne = false;
        for (int pixels = smallest; pixels <= largest; pixels++)
        {
            float size = pixels / basePixels;
            if (Mathf.Abs(size - 1f) < 0.01f)
            {
                size = 1f;
                hasOne = true;
            }
            steps.Add(size);
        }
        if (!hasOne)
        {
            steps.Add(1f);
            steps.Sort();
        }
        return steps;
    }

    public static int SizeIndex
    {
        get
        {
            Load();
            return NearestStep(SizeSteps(), _size);
        }
    }

    /// <summary>One size up or down the list. False when already at that end.</summary>
    public static bool StepSize(int direction)
    {
        var steps = SizeSteps();
        int next = Mathf.Clamp(SizeIndex + direction, 0, steps.Count - 1);
        if (next == SizeIndex) return false;

        _size = steps[next];
        PlayerPrefs.SetFloat(SizeKey, _size);
        _dirty = true;
        Apply();
        return true;
    }

    public static bool SetInward(float value)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, _inward)) return false;
        _inward = value;
        PlayerPrefs.SetFloat(InwardKey, _inward);
        _dirty = true;
        Apply();
        return true;
    }

    public static bool SetUpward(float value)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, _upward)) return false;
        _upward = value;
        PlayerPrefs.SetFloat(UpwardKey, _upward);
        _dirty = true;
        Apply();
        return true;
    }

    /// <summary>
    /// Put the settings on disk. The setters only write PlayerPrefs in memory,
    /// as the volume's do; the settings panel calls this when it closes.
    /// </summary>
    public static void Save()
    {
        if (!_dirty) return;
        _dirty = false;
        PlayerPrefs.Save();
    }

    /// <summary>
    /// The HUD a run has just built. Called by both knights' HealthBars once
    /// they have found it, so it is called twice; the second does nothing new.
    /// </summary>
    public static void Attach(VisualElement root)
    {
        if (root == null) return;
        var hud = root.Q<VisualElement>("player-hud");
        if (hud == null) return;

        if (hud != _hud)
        {
            _hud = hud;
            _left = root.Q<VisualElement>("left-hud");
            _right = root.Q<VisualElement>("right-hud");

            // The room there is to move in changes with the screen's shape, and
            // the sizes that draw cleanly change with its width
            _hud.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        }

        Apply();
    }

    private static void Apply()
    {
        if (!HasLiveHud) return;

        float size = Size;

        // The HUD's own size, read off the panel. Before the first layout it is
        // not known yet, and the panel's width with the screen's shape stands in.
        float width = _hud.layout.width;
        float height = _hud.layout.height;
        if (float.IsNaN(width) || width <= 0f) width = 1200f;
        if (float.IsNaN(height) || height <= 0f) height = width * Screen.height / Mathf.Max(1f, Screen.width);

        float side = width * SideMargin;
        float foot = width * FootMargin;
        // A longer bar is a wider block, so it has less room to move in
        float blockWidth = BlockWidth + (BarPixels - PixelBar.Width) * PixelBar.ArtPixel;
        float roomIn = Mathf.Max(0f, width * 0.5f - side - blockWidth * size - MiddleGap * 0.5f);
        float roomUp = Mathf.Max(0f, height - foot * 2f - BlockHeight * size);

        float inward = Inward * roomIn;
        float upward = Upward * roomUp;

        Place(_left, size, inward, upward, fromRight: false);
        Place(_right, size, -inward, upward, fromRight: true);
        Fade(_left);
        Fade(_right);
    }

    // On the block's parts, not on the block: TutorialStage fades the blocks
    // themselves in and out, and the two would overwrite each other. Opacity
    // multiplies down the tree, so the tutorial's fade still works on top.
    private static void Fade(VisualElement block)
    {
        if (block == null) return;
        float opacity = 1f - Transparency;
        foreach (var part in block.Children()) part.style.opacity = opacity;
    }

    private static void Place(VisualElement block, float size, float across, float upward, bool fromRight)
    {
        if (block == null) return;

        // Grown out of its own bottom corner, so it gets bigger into the screen
        // rather than off the edge of it
        block.style.transformOrigin = new TransformOrigin(
            Length.Percent(fromRight ? 100f : 0f), Length.Percent(100f), 0f);
        block.style.scale = new Scale(new Vector3(size, size, 1f));
        block.style.translate = new Translate(new Length(across), new Length(-upward), 0f);
    }

    private static int NearestStep(List<float> steps, float size)
    {
        int best = 0;
        for (int i = 1; i < steps.Count; i++)
        {
            if (Mathf.Abs(steps[i] - size) < Mathf.Abs(steps[best] - size)) best = i;
        }
        return best;
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _size = PlayerPrefs.GetFloat(SizeKey, 1f);
        _inward = Mathf.Clamp01(PlayerPrefs.GetFloat(InwardKey, 0f));
        _upward = Mathf.Clamp01(PlayerPrefs.GetFloat(UpwardKey, 0f));
        _barWidth = Mathf.Clamp(PlayerPrefs.GetFloat(WidthKey, 1f), 1f, MaxBarWidth);
        _transparency = Mathf.Clamp01(PlayerPrefs.GetFloat(TransparencyKey, 0f));
    }
}

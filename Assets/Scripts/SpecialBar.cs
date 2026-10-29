using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit adapter: PlayerSpecial pushes values in and this paints the
// special bar + streak line in PlayerHUD.uxml for one knight. The bar itself
// is a PixelBar: deep purple at the first pixel through to deep blue at the
// last. A full bar reads READY, and a halo in the bar's colour pulses behind
// it for as long as it stays full.
// The streak line splits in two: the "×N" grows, recolours and catches fire
// with the multiplier, while "· STREAK n" beside it stays put.
public class SpecialBar : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private string sidePrefix = "left";

    private VisualElement _row;
    private PixelBar _bar;
    private Label _label;
    private TextRim _labelRim;
    private VisualElement _glow;
    private VisualElement _streakLine;
    private Label _streakMult;
    private Label _streakCount;
    private StreakFlames _streakFlames;
    private TextRim _streakMultRim;
    private TextRim _streakCountRim;
    private int _shownTier;
    private bool _visible = true;
    private int _max = 1000;
    private int _current;
    private int _multiplier = 1;
    private int _streak;
    private bool _painted;
    private float _glowStrength;

    // The halo behind a full bar (owner, 2026-10-13): one swell and fade every
    // this many seconds, never fully dark in between, the way the low-health
    // halo never is. No sound goes with it.
    private const float ReadyPulseSeconds = 1.2f;
    private const float ReadyGlowLow = 0.3f;

    public void Initialize(int maxSpecial)
    {
        _max = Mathf.Max(1, maxSpecial);
        Apply();
    }

    /// <summary>
    /// Hides the whole bar row for a knight carrying no special. The bar still
    /// fills underneath — there is simply nothing to spend it on, so showing a
    /// meter the player can never cash in would only read as a bug.
    /// </summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        ApplyVisibility();
    }

    public void SetValue(int newValue)
    {
        _current = Mathf.Clamp(newValue, 0, _max);
        Apply();
    }

    public void SetStreak(int multiplier, int streak)
    {
        _multiplier = multiplier;
        _streak = streak;
        ApplyStreak();
    }

    // rootVisualElement can be null while the panel is still initializing —
    // Update retries the bind with the latest cached values. After that it only
    // ticks while the bar is full, to move the halo; Apply wakes it for that.
    private void Update()
    {
        if (!EnsureUI()) return;

        if (!_painted)
        {
            _painted = true;
            ApplyVisibility();
            Apply();
            ApplyStreak();
        }

        if (!TickReadyPulse()) enabled = false;
    }

    /// <summary>
    /// Moves the halo behind a full bar. False when there is nothing to move:
    /// the bar is not full, or it is hidden because the knight carries no
    /// special. Scaled time, so the pulse holds still behind the pause and
    /// upgrade menus like everything else. Both knights read the same clock, so
    /// two full bars pulse together.
    /// </summary>
    private bool TickReadyPulse()
    {
        if (_glow == null) return false;

        if (!_visible || _current < _max)
        {
            SetGlow(0f);
            return false;
        }

        float pulse = 0.5f + 0.5f * Mathf.Cos(Time.time / ReadyPulseSeconds * 2f * Mathf.PI);
        SetGlow(Mathf.Lerp(ReadyGlowLow, 1f, pulse));
        return true;
    }

    private void SetGlow(float strength)
    {
        if (Mathf.Approximately(strength, _glowStrength)) return;
        _glowStrength = strength;
        _glow.style.opacity = strength;
    }

    private bool EnsureUI()
    {
        if (_row != null && _bar != null && _labelRim != null && _streakFlames != null) return true;
        if (hudDocument == null) return false;
        var root = hudDocument.rootVisualElement;
        if (root == null) return false;
        _row = root.Q<VisualElement>($"{sidePrefix}-special-row");
        var holder = root.Q<VisualElement>($"{sidePrefix}-special-bar");
        _label = root.Q<Label>($"{sidePrefix}-special-label");
        // Optional: the bar still works in a HUD built without the halo
        _glow = root.Q<VisualElement>($"{sidePrefix}-special-glow");
        _streakLine = root.Q<VisualElement>($"{sidePrefix}-streak");
        _streakMult = root.Q<Label>($"{sidePrefix}-streak-mult");
        _streakCount = root.Q<Label>($"{sidePrefix}-streak-count");
        var multBox = root.Q<VisualElement>($"{sidePrefix}-streak-mult-box");
        if (_row == null || holder == null || _label == null || _streakLine == null
            || _streakMult == null || _streakCount == null || multBox == null) return false;

        _bar = new PixelBar(holder, PixelBar.SpecialRamp, sidePrefix == "right");

        // First child, so the number draws over its own flames
        _streakFlames = new StreakFlames();
        multBox.Insert(0, _streakFlames);

        // Thick black outlines behind both halves of the line. From here on their
        // text and classes go through the rims, which keep the copies in step.
        _streakMultRim = new TextRim(_streakMult, MultRimPixels[1]);
        _streakCountRim = new TextRim(_streakCount, CountRimPixels);
        _labelRim = new TextRim(_label, CountRimPixels);
        return true;
    }

    private void Apply()
    {
        if (!EnsureUI()) return;
        bool full = _current >= _max;
        _bar.SetFill((float)_current / _max);
        _labelRim.SetText(full ? "READY" : $"{_current} / {_max}");
        _labelRim.EnableInClassList("bar-label--ready", full);

        // Update is what moves the halo. It takes the halo off and goes back to
        // sleep by itself on the first frame the bar is no longer full.
        if (full && _visible) enabled = true;
    }

    private void ApplyVisibility()
    {
        if (!EnsureUI()) return;
        _row.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_visible && _current >= _max) enabled = true;
    }

    private static readonly string[] TierClasses = { null, "streak-mult--1", "streak-mult--2", "streak-mult--3", "streak-mult--4" };
    private const string PopClass = "streak-mult--pop";

    // Outline thickness in HUD pixels (owner, 2026-09-26: "thick black outline").
    // The multiplier's grows with its tier, so a big x4 is outlined as heavily,
    // for its size, as the tiny x1.
    private static readonly float[] MultRimPixels = { 0f, 1.5f, 2f, 2.5f, 3f };
    // "· STREAK n". The numbers beside both bars wear the same one (owner,
    // 2026-10-14), so HealthBar reads it from here.
    public const float CountRimPixels = 1.5f;

    private void ApplyStreak()
    {
        if (!EnsureUI()) return;
        int tier = Mathf.Clamp(_multiplier, 1, TierClasses.Length - 1);
        _streakMultRim.SetText($"×{_multiplier}");
        _streakCountRim.SetText($"· STREAK {_streak}");
        _streakLine.EnableInClassList("streak--hot", _multiplier > 1);
        for (int t = 1; t < TierClasses.Length; t++)
            _streakMultRim.EnableInClassList(TierClasses[t], t == tier);
        _streakMultRim.SetThickness(MultRimPixels[tier]);
        _streakFlames.SetTier(tier);

        // Climbing a tier punches the number; dropping back to x1 just shrinks
        if (_shownTier > 0 && tier > _shownTier)
        {
            _streakMultRim.AddToClassList(PopClass);
            _streakMult.schedule.Execute(() => _streakMultRim.RemoveFromClassList(PopClass)).StartingIn(50);
        }
        _shownTier = tier;
    }
}

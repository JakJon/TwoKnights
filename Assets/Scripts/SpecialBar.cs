using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit adapter: PlayerSpecial pushes values in and this paints the
// Gilded Vigil special bar + streak line in PlayerHUD.uxml for one knight.
// Gold owns the charged state: a full bar brightens and reads READY.
// The streak line splits in two: the "×N" grows, recolours and catches fire
// with the multiplier, while "· STREAK n" beside it stays put.
public class SpecialBar : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private string sidePrefix = "left";

    private VisualElement _row;
    private VisualElement _fill;
    private Label _label;
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
    // Update retries the bind with the latest cached values, then stops ticking
    private void Update()
    {
        if (EnsureUI())
        {
            ApplyVisibility();
            Apply();
            ApplyStreak();
            enabled = false;
        }
    }

    private bool EnsureUI()
    {
        if (_row != null && _fill != null && _label != null && _streakFlames != null) return true;
        if (hudDocument == null) return false;
        var root = hudDocument.rootVisualElement;
        if (root == null) return false;
        _row = root.Q<VisualElement>($"{sidePrefix}-special-row");
        _fill = root.Q<VisualElement>($"{sidePrefix}-special-fill");
        _label = root.Q<Label>($"{sidePrefix}-special-label");
        _streakLine = root.Q<VisualElement>($"{sidePrefix}-streak");
        _streakMult = root.Q<Label>($"{sidePrefix}-streak-mult");
        _streakCount = root.Q<Label>($"{sidePrefix}-streak-count");
        var multBox = root.Q<VisualElement>($"{sidePrefix}-streak-mult-box");
        if (_row == null || _fill == null || _label == null || _streakLine == null
            || _streakMult == null || _streakCount == null || multBox == null) return false;

        // First child, so the number draws over its own flames
        _streakFlames = new StreakFlames();
        multBox.Insert(0, _streakFlames);

        // Thick black outlines behind both halves of the line. From here on their
        // text and classes go through the rims, which keep the copies in step.
        _streakMultRim = new TextRim(_streakMult, MultRimPixels[1]);
        _streakCountRim = new TextRim(_streakCount, CountRimPixels);
        return true;
    }

    private void Apply()
    {
        if (!EnsureUI()) return;
        float fraction = (float)_current / _max;
        bool full = _current >= _max;
        _fill.style.width = Length.Percent(fraction * 100f);
        _fill.EnableInClassList("special--full", full);
        _label.text = full ? "READY" : $"{_current} / {_max}";
        _label.EnableInClassList("bar-label--ready", full);
    }

    private void ApplyVisibility()
    {
        if (!EnsureUI()) return;
        _row.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static readonly string[] TierClasses = { null, "streak-mult--1", "streak-mult--2", "streak-mult--3", "streak-mult--4" };
    private const string PopClass = "streak-mult--pop";

    // Outline thickness in HUD pixels (owner, 2026-09-26: "thick black outline").
    // The multiplier's grows with its tier, so a big x4 is outlined as heavily,
    // for its size, as the tiny x1.
    private static readonly float[] MultRimPixels = { 0f, 1.5f, 2f, 2.5f, 3f };
    private const float CountRimPixels = 1.5f;

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

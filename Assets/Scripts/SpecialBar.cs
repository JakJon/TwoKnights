using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit adapter: PlayerSpecial pushes values in and this paints the
// Gilded Vigil special bar + streak line in PlayerHUD.uxml for one knight.
// Gold owns the charged state: a full bar brightens and reads READY.
public class SpecialBar : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private string sidePrefix = "left";

    private VisualElement _row;
    private VisualElement _fill;
    private Label _label;
    private Label _streakLabel;
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
        if (_row != null && _fill != null && _label != null && _streakLabel != null) return true;
        if (hudDocument == null) return false;
        var root = hudDocument.rootVisualElement;
        if (root == null) return false;
        _row = root.Q<VisualElement>($"{sidePrefix}-special-row");
        _fill = root.Q<VisualElement>($"{sidePrefix}-special-fill");
        _label = root.Q<Label>($"{sidePrefix}-special-label");
        _streakLabel = root.Q<Label>($"{sidePrefix}-streak");
        return _row != null && _fill != null && _label != null && _streakLabel != null;
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

    private void ApplyStreak()
    {
        if (!EnsureUI()) return;
        _streakLabel.text = $"×{_multiplier} · STREAK {_streak}";
        _streakLabel.EnableInClassList("streak--hot", _multiplier > 1);
    }
}

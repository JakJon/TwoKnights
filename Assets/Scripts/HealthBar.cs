using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit adapter: PlayerHealth pushes values in (same Initialize/SetValue
// API as the old uGUI bar) and this paints the Gilded Vigil health bar defined
// in PlayerHUD.uxml for one knight ("left"/"right").
public class HealthBar : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private string sidePrefix = "left";

    private VisualElement _fill;
    private Label _label;
    private int _max = 1;
    private int _current = 1;

    private const float LowHealthFraction = 0.25f;

    public void Initialize(int maxHealth)
    {
        _max = Mathf.Max(1, maxHealth);
        Apply();
    }

    public void SetValue(int newValue)
    {
        _current = Mathf.Clamp(newValue, 0, _max);
        Apply();
    }

    // rootVisualElement can be null while the panel is still initializing —
    // Update retries the bind with the latest cached values, then stops ticking
    private void Update()
    {
        if (EnsureUI())
        {
            Apply();
            enabled = false;
        }
    }

    private bool EnsureUI()
    {
        if (_fill != null && _label != null) return true;
        if (hudDocument == null) return false;
        var root = hudDocument.rootVisualElement;
        if (root == null) return false;
        _fill = root.Q<VisualElement>($"{sidePrefix}-health-fill");
        _label = root.Q<Label>($"{sidePrefix}-health-label");
        return _fill != null && _label != null;
    }

    private void Apply()
    {
        if (!EnsureUI()) return;
        float fraction = (float)_current / _max;
        _fill.style.width = Length.Percent(fraction * 100f);
        _fill.EnableInClassList("health--low", _current > 0 && fraction <= LowHealthFraction);
        _label.text = $"{_current} / {_max}";
    }
}

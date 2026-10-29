using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit adapter: PlayerHealth pushes values in (same Initialize/SetValue
// API as the old uGUI bar) and this paints one knight's ("left"/"right")
// health bar in PlayerHUD.uxml. The bar itself is a PixelBar: light red at
// the first pixel through to deep crimson at the last.
public class HealthBar : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private string sidePrefix = "left";

    private PixelBar _bar;
    private Label _label;
    private TextRim _labelRim;
    private VisualElement _glow;
    private int _max = 1;
    private int _current = 1;
    private float _glowStrength;

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

    /// <summary>
    /// The red low-health halo round the bar, 0 (off) to 1 (full). PlayerHealth
    /// calls this every frame to make it pulse, so a repeat of the same value is
    /// dropped here rather than restyling the element sixty times a second for
    /// a knight who is nowhere near dying.
    /// </summary>
    public void SetWarningGlow(float strength)
    {
        strength = Mathf.Clamp01(strength);
        if (Mathf.Approximately(strength, _glowStrength)) return;
        _glowStrength = strength;
        ApplyGlow();
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
        if (_bar != null && _labelRim != null) return true;
        if (hudDocument == null) return false;
        var root = hudDocument.rootVisualElement;
        if (root == null) return false;
        var holder = root.Q<VisualElement>($"{sidePrefix}-health-bar");
        _label = root.Q<Label>($"{sidePrefix}-health-label");
        // Optional: the bar still works in a HUD built without the halo
        _glow = root.Q<VisualElement>($"{sidePrefix}-health-glow");
        if (holder == null || _label == null) return false;

        _bar = new PixelBar(holder, PixelBar.HealthRamp, sidePrefix == "right");
        // The streak count's outline, round the number (see SpecialBar)
        _labelRim = new TextRim(_label, SpecialBar.CountRimPixels);

        // The player's size and position for the bars, from Settings
        HudLayout.Attach(root);
        return true;
    }

    private void Apply()
    {
        if (!EnsureUI()) return;
        _bar.SetFill((float)_current / _max);
        _labelRim.SetText($"{_current} / {_max}");
        ApplyGlow();
    }

    private void ApplyGlow()
    {
        if (!EnsureUI() || _glow == null) return;
        _glow.style.opacity = _glowStrength;
    }
}

using UnityEngine;
using UnityEngine.UIElements;

// Top-center boss health bar living in the PlayerHUD document (the boss-hud
// block in PlayerHUD.uxml). The boss itself drives it — Show on spawn,
// SetFraction as its health moves, Hide when it dies — so no scene wiring
// is needed beyond the UXML/USS.
public static class BossHealthBar
{
    private const string VisibleClass = "boss-hud--visible";

    private static UIDocument _document;
    private static VisualElement _container;
    private static VisualElement _fill;
    private static Label _title;
    private static Label _amount;
    private static float _lastFraction = -1f;

    // Sets the bar up at full but keeps it at opacity 0 — call Reveal to fade
    // it in (the boss waits out the wave-name banner first)
    public static void Show(string title)
    {
        if (!TryResolve()) return;
        _title.text = string.IsNullOrEmpty(title) ? string.Empty : title.ToUpperInvariant();
        _container.RemoveFromClassList(VisibleClass);
        _container.style.display = DisplayStyle.Flex;
        _lastFraction = -1f;
        SetFraction(1f);
        // Blanked rather than guessed: the boss pushes real numbers on its first
        // frame, and a stale count from the previous boss must not flash first
        if (_amount != null) _amount.text = string.Empty;
    }

    // Starts the USS opacity transition (0.9s, .boss-hud--visible)
    public static void Reveal()
    {
        if (!TryResolve()) return;
        _container.AddToClassList(VisibleClass);
    }

    // Preferred entry point: drives the bar AND the number from one call so the two
    // can never disagree. Bosses with more than one body (the Crimson Twins) pass the
    // combined pool.
    public static void SetHealth(float current, float max)
    {
        if (!TryResolve()) return;

        current = Mathf.Max(0f, current);
        max = Mathf.Max(1f, max);
        SetFraction(current / max);

        if (_amount != null)
        {
            _amount.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }
    }

    public static void SetFraction(float fraction)
    {
        if (!TryResolve()) return;
        fraction = Mathf.Clamp01(fraction);
        if (Mathf.Approximately(fraction, _lastFraction)) return;
        _lastFraction = fraction;
        _fill.style.width = Length.Percent(fraction * 100f);
    }

    // Never re-resolves: hiding only matters while the HUD is still alive
    public static void Hide()
    {
        if (_document == null || _container == null) return;
        _container.RemoveFromClassList(VisibleClass);
        _container.style.display = DisplayStyle.None;
    }

    // The HUD document is found lazily and re-resolved after scene loads;
    // rootVisualElement can also be null briefly after a UXML reimport
    private static bool TryResolve()
    {
        if (_document != null && _container != null) return true;

        if (_document == null)
        {
            _container = null;
            var hudObject = GameObject.Find("PlayerHUD");
            _document = hudObject != null ? hudObject.GetComponent<UIDocument>() : null;
        }

        var root = _document != null ? _document.rootVisualElement : null;
        if (root == null) return false;

        _container = root.Q<VisualElement>("boss-hud");
        _fill = root.Q<VisualElement>("boss-health-fill");
        _title = root.Q<Label>("boss-title");
        // Deliberately not part of the gate below: a HUD missing only the number
        // should still show a working bar rather than no boss HUD at all
        _amount = root.Q<Label>("boss-health-amount");
        return _container != null && _fill != null && _title != null;
    }
}

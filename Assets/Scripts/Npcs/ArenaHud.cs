using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Takes the player HUD down while somebody is standing in the arena talking, and
/// puts it back when they leave.
///
/// Health, special meters and streak counts are the readouts of a fight. During a
/// quest scene there is no fight — the arena is frozen, the board is swept and an
/// NPC has walked into the middle of it — and a pair of bars still sitting in the
/// corners is the clearest way to say "this is still gameplay, keep playing". It
/// is also the only thing that draws over the dialogue plate, since the HUD is a
/// screen-space document and the plate is in the world.
///
/// Resolved lazily off the PlayerHUD document, the way BossHealthBar and
/// VentureCurtain already do — there is no scene object to hang a reference on and
/// the document is rebuilt across scene loads.
/// </summary>
public static class ArenaHud
{
    private const string HudObjectName = "PlayerHUD";
    /// <summary>The element holding both knights' bars and the boss bar. The venture curtain is its SIBLING, so this never touches it.</summary>
    private const string RootName = "player-hud";

    private static UIDocument _document;
    private static VisualElement _hud;

    /// <summary>
    /// Fades the HUD to <paramref name="target"/> on unscaled time, because the
    /// whole scene runs with the game frozen behind it.
    ///
    /// Opacity rather than display: the two knight blocks sit in a flex row and
    /// removing one would shove the other across the screen on the way out.
    /// </summary>
    public static IEnumerator FadeTo(float target, float seconds = 0.25f)
    {
        if (!TryResolve())
        {
            yield break;
        }

        float from = _hud.resolvedStyle.opacity;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _hud.style.opacity = Mathf.Lerp(from, target, elapsed / seconds);
            yield return null;
        }
        _hud.style.opacity = target;
    }

    /// <summary>
    /// Puts the HUD back with no fade. For the paths that must not leave it
    /// invisible whatever happened — a scene cut short, a run abandoned mid-way.
    /// </summary>
    public static void ShowImmediate()
    {
        if (TryResolve()) _hud.style.opacity = 1f;
    }

    private static bool TryResolve()
    {
        if (_document != null && _hud != null) return true;

        if (_document == null)
        {
            _hud = null;
            var hudObject = GameObject.Find(HudObjectName);
            _document = hudObject != null ? hudObject.GetComponent<UIDocument>() : null;
        }

        var root = _document != null ? _document.rootVisualElement : null;
        if (root == null) return false;

        if (_hud == null) _hud = root.Q<VisualElement>(RootName);
        return _hud != null;
    }
}

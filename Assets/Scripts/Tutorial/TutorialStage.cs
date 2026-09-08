using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// What the player can see and touch while the tutorial is running.
///
/// The tutorial opens on one knight and nothing else — no second knight, no bars —
/// and hands each piece over as it is explained. Everything here is reversible and
/// nothing is destroyed: the right knight has to be standing in the scene the whole
/// time (the Spawner found it by tag before the tutorial ever started, and wave one
/// is about to need it), it is simply invisible, inert and out of harm's way.
/// </summary>
public class TutorialStage
{
    private const string HudProbeElement = "left-hud";

    private readonly GameObject _leftKnight;
    private readonly GameObject _rightKnight;

    private readonly List<SpriteRenderer> _rightSprites = new List<SpriteRenderer>();
    private readonly List<Color> _rightSpriteColors = new List<Color>();

    // The right knight's colliders, and whether each was on before we touched it.
    // Invisible is not the same as absent: the right knight stands at (2, -0.5),
    // squarely in the path of anything walking in from the right edge towards the
    // left knight, and EnemyBase destroys itself on contact with a PlayerRight or
    // Shield collider. Left solid, it silently ate every slime the tutorial sent
    // across — the arrow lesson passed itself before the player could fire a shot.
    private readonly List<Collider2D> _rightColliders = new List<Collider2D>();
    private readonly List<bool> _rightColliderEnabled = new List<bool>();

    private VisualElement _leftHud;
    private VisualElement _rightHud;

    // What the HUD SHOULD be showing. Kept separately from the elements because
    // the elements may not exist yet: a UIDocument builds its tree in OnEnable,
    // and while that normally beats the Spawner to the first frame, a root that is
    // still null is a real state. Losing that race must not mean the bars stay up
    // for the whole tutorial, so the wanted values are held here and applied the
    // moment the bind succeeds (see Tick).
    private float _wantLeftHud = 1f;
    private float _wantRightHud = 1f;

    public TutorialStage(GameObject leftKnight, GameObject rightKnight)
    {
        _leftKnight = leftKnight;
        _rightKnight = rightKnight;

        if (_rightKnight != null)
        {
            _rightKnight.GetComponentsInChildren(true, _rightSprites);
            foreach (var sprite in _rightSprites) _rightSpriteColors.Add(sprite.color);

            _rightKnight.GetComponentsInChildren(true, _rightColliders);
            foreach (var collider in _rightColliders) _rightColliderEnabled.Add(collider.enabled);
        }
    }

    /// <summary>
    /// The opening picture: the left knight alone, holding a shield it cannot yet
    /// shoot or swing from, and a screen with nothing else on it.
    /// </summary>
    public void OpenOnLeftKnightAlone()
    {
        SetKnightAlpha(_rightKnight, _rightSprites, _rightSpriteColors, 0f);
        SetInput(_rightKnight, shield: false, bow: false, sword: false, special: false);
        SetInput(_leftKnight, shield: true, bow: false, sword: false, special: false);

        // Nothing in the tutorial targets the right knight, but it is standing on a
        // live board with its bar hidden — an untouchable one cannot be quietly
        // killed by something that goes wrong.
        SetUntouchable(_rightKnight, true);
        SetUntouchable(_leftKnight, true);

        SetHudOpacity(0f, 0f);

        // Out of the way as well as out of sight — see _rightColliders
        for (int i = 0; i < _rightColliders.Count; i++)
        {
            if (_rightColliders[i] != null) _rightColliders[i].enabled = false;
        }

        // Nothing to do about the gold counter: it is off for the whole arena now,
        // not just for the tutorial.
    }

    public void AllowBow() => SetInput(_leftKnight, shield: true, bow: true, sword: false, special: false);
    public void AllowSword() => SetInput(_leftKnight, shield: true, bow: true, sword: true, special: false);
    public void AllowSpecial() => SetInput(_leftKnight, shield: true, bow: true, sword: true, special: true);

    /// <summary>
    /// Hands the game over: both knights visible and answering the sticks, both
    /// sets of bars up, and nobody untouchable any more. From here the arena is in
    /// exactly the state a normal run starts in.
    /// </summary>
    public void RestoreEverything()
    {
        SetKnightAlpha(_rightKnight, _rightSprites, _rightSpriteColors, 1f);
        SetInput(_leftKnight, shield: true, bow: true, sword: true, special: true);
        SetInput(_rightKnight, shield: true, bow: true, sword: true, special: true);
        SetUntouchable(_leftKnight, false);
        SetUntouchable(_rightKnight, false);
        SetHudOpacity(1f, 1f);

        // Restored to what each was, not blanket-enabled — a collider that was off
        // before the tutorial is off for a reason of its own
        for (int i = 0; i < _rightColliders.Count; i++)
        {
            if (_rightColliders[i] != null) _rightColliders[i].enabled = _rightColliderEnabled[i];
        }
    }

    /// <summary>The left knight's bars, brought up on their own for the orb lesson.</summary>
    public void SetLeftHudOpacity(float alpha)
    {
        SetHudOpacity(alpha, _wantRightHud);
    }

    public void SetHudOpacity(float left, float right)
    {
        _wantLeftHud = left;
        _wantRightHud = right;
        ApplyHud();
    }

    // Retrying costs a scan of every UIDocument in the scene, so it gives up after
    // a couple of seconds' worth of frames. Past that the HUD is not late, it is
    // missing, and a scan every frame for the rest of the tutorial buys nothing.
    private const int MaxBindAttempts = 120;
    private int _bindAttempts;

    /// <summary>
    /// Retries the HUD bind until it takes. Pumped every frame by the director —
    /// free once bound, and the only thing standing between a slow-building
    /// UIDocument and a tutorial that opens with both knights' bars on screen.
    /// </summary>
    public void Tick()
    {
        if (_leftHud != null || _bindAttempts >= MaxBindAttempts) return;

        _bindAttempts++;
        ApplyHud();

        if (_leftHud == null && _bindAttempts == MaxBindAttempts)
        {
            Debug.LogWarning("[Tutorial] Never found the player HUD (no 'left-hud' element in any " +
                             "UIDocument), so the bars could not be hidden. The tutorial still runs.");
        }
    }

    private void ApplyHud()
    {
        if (!EnsureHud()) return;
        _leftHud.style.opacity = _wantLeftHud;
        if (_rightHud != null) _rightHud.style.opacity = _wantRightHud;
    }

    /// <summary>Fade in the right knight's sprites. 0 = gone, 1 = fully there.</summary>
    public void SetRightKnightAlpha(float alpha)
    {
        SetKnightAlpha(_rightKnight, _rightSprites, _rightSpriteColors, alpha);
    }

    // The HUD document builds its tree in OnEnable, so by the time the Spawner is
    // starting a run this normally resolves first try — but a null root on the
    // opening frame is a real state, hence the retry rather than a one-shot bind.
    private bool EnsureHud()
    {
        if (_leftHud != null) return true;

        var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var document in documents)
        {
            var root = document.rootVisualElement;
            if (root == null) continue;

            var left = root.Q<VisualElement>(HudProbeElement);
            if (left == null) continue;

            _leftHud = left;
            _rightHud = root.Q<VisualElement>("right-hud");
            return true;
        }

        return false;
    }

    private static void SetKnightAlpha(GameObject knight, List<SpriteRenderer> sprites, List<Color> authored, float alpha)
    {
        if (knight == null) return;
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;
            // Multiply the authored alpha rather than replacing it, so a sprite the
            // artist made semi-transparent stays that way at full fade-in
            Color color = authored[i];
            color.a *= alpha;
            sprites[i].color = color;
        }
    }

    private static void SetInput(GameObject knight, bool shield, bool bow, bool sword, bool special)
    {
        if (knight == null) return;

        foreach (var orbit in knight.GetComponentsInChildren<ShieldOrbit>(true)) orbit.InputEnabled = shield;
        foreach (var shooter in knight.GetComponentsInChildren<PlayerShooter>(true)) shooter.InputEnabled = bow;
        // The sword is built at runtime by ShieldOrbit, so it is only ever found by
        // searching the children — there is nothing to reach on the prefab
        foreach (var swing in knight.GetComponentsInChildren<SwordSwing>(true)) swing.InputEnabled = sword;
        foreach (var playerSpecial in knight.GetComponentsInChildren<PlayerSpecial>(true)) playerSpecial.InputEnabled = special;
    }

    private static void SetUntouchable(GameObject knight, bool untouchable)
    {
        if (knight == null) return;
        var health = knight.GetComponent<PlayerHealth>();
        if (health != null) health.Untouchable = untouchable;
    }
}

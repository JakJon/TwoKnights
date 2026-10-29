using UnityEngine;

/// <summary>
/// Makes a sorting cart flash. Put on every cart still on the track when a phase's
/// clock is about to run out — see CartSorting — so the player is warned before
/// the carts are taken off and the challenge is lost.
///
/// The flash is set in LateUpdate so that it has the last word each frame: the
/// cart's own Animator runs before then, and anything it writes to the renderer
/// would otherwise undo a flash set earlier in the frame.
/// </summary>
public class CartBlink : MonoBehaviour
{
    /// <summary>This long bright, this long dim. The range target's own blink.</summary>
    private const float BlinkSeconds = 0.14f;
    private const float DimAlpha = 0.3f;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (_renderer == null) return;

        bool bright = Mathf.FloorToInt(Time.time / BlinkSeconds) % 2 == 0;
        Color color = _renderer.color;
        color.a = bright ? 1f : DimAlpha;
        _renderer.color = color;
    }
}

using UnityEngine;

/// <summary>
/// Spins an enemy rock's art while it flies. Each rock picks its own direction,
/// clockwise or anticlockwise, and its own speed from a range when it is made, so
/// a volley tumbles at different rates instead of turning in step.
///
/// Only the ART turns, never the rock itself. A rock's rotation is its heading —
/// ProjectileMovement flies it along its own +X, and the Reflector, the mirrors and
/// the IncomingLedger all read transform.right as where it is going — so the sprite
/// (and the RockOutline rim beside it) lives on a child, and this turns that child.
///
/// The turn is about the middle of the sprite, not its pivot. The rock art is
/// imported with its pivot at the bottom edge, and spinning about that would swing
/// the stone round in a circle instead of rolling it on the spot.
///
/// Cosmetic only, so it rolls freely with UnityEngine.Random: it decides nothing
/// about what a wave throws or where, which is the part that has to stay fixed.
/// </summary>
[DisallowMultipleComponent]
public class RockSpin : MonoBehaviour
{
    [Tooltip("The child carrying the rock's sprite. Left empty, the first SpriteRenderer among the children.")]
    [SerializeField] private SpriteRenderer art;

    [Tooltip("Slowest spin, in degrees per second. Each rock picks a speed between this and the fastest when it is made.")]
    [SerializeField] private float minDegreesPerSecond = 120f;

    [Tooltip("Fastest spin, in degrees per second.")]
    [SerializeField] private float maxDegreesPerSecond = 360f;

    // Signed: positive is anticlockwise, negative clockwise
    private float _degreesPerSecond;
    private float _angle;

    void Awake()
    {
        if (art == null) art = GetComponentInChildren<SpriteRenderer>();

        float speed = Random.Range(minDegreesPerSecond, maxDegreesPerSecond);
        _degreesPerSecond = Random.value < 0.5f ? speed : -speed;
    }

    // Scaled time, so a rock frozen in the air by the pause menu stops turning too
    void LateUpdate()
    {
        if (art == null) return;

        _angle = Mathf.Repeat(_angle + _degreesPerSecond * Time.deltaTime, 360f);

        // Rotate about the sprite's middle: turn the child, then slide it so that
        // middle lands back where it started
        Vector3 middle = art.sprite != null ? art.sprite.bounds.center : Vector3.zero;
        Quaternion spin = Quaternion.Euler(0f, 0f, _angle);
        art.transform.localRotation = spin;
        art.transform.localPosition = middle - spin * middle;
    }
}

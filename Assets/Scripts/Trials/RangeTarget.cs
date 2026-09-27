using System.Collections;
using UnityEngine;

/// <summary>
/// One target on the target range: a red-and-white bullseye that pops into view in
/// a puff of white smoke, waits to be shot, and leaves in the same puff whether it
/// was hit or the time ran out (owner, 2026-09-26: "the same as the ninjas but
/// white").
///
/// Hit by exactly what hits the Ninja — any of a knight's arrows, or the blade —
/// and the shot that lands is spent on it, so one arrow breaks one target.
///
/// A target can travel: pattern four's slide from one corner to the next over the
/// whole time limit, starting the moment they can be hit. A still target is simply
/// one whose start and end are the same point.
///
/// Built in code rather than as a prefab, the way the Ninja trial's figure is: a
/// sprite, a trigger circle, and nothing else.
/// </summary>
public class RangeTarget : MonoBehaviour
{
    /// <summary>The puff gets this long on its own before the target is in it — the Ninja's own beat.</summary>
    private const float RevealDelaySeconds = 0.3f;

    /// <summary>
    /// World units. The art is a 28-pixel disc on a 32-pixel canvas at 32 pixels a
    /// unit, so this is its rim plus a hair: a shot that visibly clips the edge
    /// counts.
    /// </summary>
    private const float HitRadius = 0.47f;

    /// <summary>The cloud, sized to a one-unit disc rather than a standing figure.</summary>
    private const float SmokeRadius = 0.8f;

    /// <summary>How the warning blinks: this long bright, this long dim.</summary>
    private const float BlinkSeconds = 0.14f;
    private const float BlinkDimAlpha = 0.3f;

    private SpriteRenderer _renderer;
    private CircleCollider2D _hitbox;
    private Vector2 _from;
    private Vector2 _to;
    private float _travelSeconds;
    private float _revealedAt;
    private bool _warning;

    /// <summary>It can be seen and hit. The clock starts here, not at the puff.</summary>
    public bool Revealed { get; private set; }
    public bool WasHit { get; private set; }

    /// <summary>
    /// Puts a target up at <paramref name="from"/>. It travels to
    /// <paramref name="to"/> over <paramref name="travelSeconds"/>, counted from the
    /// moment it is revealed; pass the same point twice for one that stands still.
    /// </summary>
    public static RangeTarget Appear(Sprite sprite, Vector2 from, Vector2 to, float travelSeconds)
    {
        var go = new GameObject("Range Target");
        go.transform.position = new Vector3(from.x, from.y, 0f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        // Where the NPCs draw, so the smoke — which sorts in front of them —
        // covers it on the way in and out, exactly as it covers the Ninja.
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = NpcFx.NpcOrder;

        var target = go.AddComponent<RangeTarget>();
        target.Begin(renderer, from, to, travelSeconds);
        return target;
    }

    private void Begin(SpriteRenderer renderer, Vector2 from, Vector2 to, float travelSeconds)
    {
        _renderer = renderer;
        _from = from;
        _to = to;
        _travelSeconds = Mathf.Max(0f, travelSeconds);

        _hitbox = gameObject.AddComponent<CircleCollider2D>();
        _hitbox.isTrigger = true;
        _hitbox.radius = HitRadius;
        _hitbox.enabled = false;

        SetAlpha(0f);
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        NpcFx.SmokeCentredOn(transform.position, SmokeRadius, white: true);
        yield return new WaitForSeconds(RevealDelaySeconds);

        SetAlpha(1f);
        _hitbox.enabled = true;
        _revealedAt = Time.time;
        Revealed = true;
    }

    private void Update()
    {
        if (!Revealed) return;

        if (_travelSeconds > 0f && _from != _to)
        {
            float t = Mathf.Clamp01((Time.time - _revealedAt) / _travelSeconds);
            transform.position = Vector2.Lerp(_from, _to, t);
        }

        if (_warning)
        {
            bool bright = Mathf.FloorToInt(Time.time / BlinkSeconds) % 2 == 0;
            SetAlpha(bright ? 1f : BlinkDimAlpha);
        }
    }

    /// <summary>
    /// Time is nearly up: blink until hit or gone (owner, 2026-09-26). Still
    /// hittable while dim — the blink is a warning, not a gap.
    /// </summary>
    public void Warn()
    {
        if (WasHit) return;
        _warning = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Revealed || WasHit) return;
        if (CollectibleOrb.CollectorFor(other) == null) return;

        // The arrow is spent on it: one shot, one target, and it cannot sail on
        // into the next one along the edge.
        TrialOrb.SpendShot(other);
        WasHit = true;

        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(audio.executeFlash);
        Vanish();
    }

    /// <summary>Gone, in white smoke — broken or timed out, it leaves the same way.</summary>
    public void Vanish()
    {
        if (_hitbox != null) _hitbox.enabled = false;
        StopAllCoroutines();
        _warning = false;
        if (Revealed) NpcFx.SmokeCentredOn(transform.position, SmokeRadius, white: true);
        Revealed = false;
        SetAlpha(0f);
        Destroy(gameObject, 0.1f);
    }

    private void SetAlpha(float alpha)
    {
        if (_renderer != null) _renderer.color = new Color(1f, 1f, 1f, alpha);
    }
}

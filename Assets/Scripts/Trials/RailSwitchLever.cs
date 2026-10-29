using System.Collections;
using UnityEngine;

/// <summary>
/// One of the two levers that stand between the knights in the cart sorting
/// challenge, a red one and a blue one. Hit it with a sword or an arrow and the
/// fork whose corner piece is that colour swings over to its other branch. Hit it
/// again and it swings back. See RailNetwork.Throw.
///
/// The lever leans one way at rest and the other way once thrown. Which way is
/// which is the phase's to say, because it depends on the track: toward the
/// branch a fork is sending carts down, or toward a wheel whose doors are open.
///
/// Hit by exactly what hits a range target: any of a knight's arrows, or the
/// blade. An arrow is spent on it, so one arrow throws it once — and an arrow
/// from the far knight stops at the nearer lever, which is what makes the red
/// lever the left knight's and the blue lever the right knight's.
///
/// A prefab (Rail_Switch_Red, Rail_Switch_Blue), unlike the range target, because
/// it needs the same lit material as the rails it stands among.
/// </summary>
public class RailSwitchLever : MonoBehaviour
{
    [Tooltip("Which switch this lever throws: every fork corner of this colour turns with it.")]
    [SerializeField] private RailSwitchColor color = RailSwitchColor.Red;
    [SerializeField] private SpriteRenderer body;
    [Tooltip("The lever leaning left.")]
    [SerializeField] private Sprite leanLeftSprite;
    [Tooltip("The lever leaning right.")]
    [SerializeField] private Sprite leanRightSprite;
    [Tooltip("Played each time the lever is thrown.")]
    [SerializeField] private SoundEffect throwSound;

    /// <summary>The puff gets this long on its own before the lever is in it — the range target's beat.</summary>
    private const float RevealDelaySeconds = 0.3f;

    /// <summary>
    /// One swing throws it once. A blade and the echoes that follow it can each
    /// touch the lever inside the same swing, and two throws put the fork back
    /// where it started.
    /// </summary>
    private const float RehitSeconds = 0.2f;

    private const float SmokeRadius = 0.7f;

    /// <summary>How far the lever jumps in size when thrown, and how long it takes to settle.</summary>
    private const float KickScale = 1.25f;
    private const float KickSeconds = 0.12f;

    private Collider2D _hitbox;
    private RailNetwork _rails;
    private bool _restsLeft;
    private float _readyAt;
    private Coroutine _kick;

    public RailSwitchColor Color => color;

    /// <summary>It can be seen and hit.</summary>
    public bool Revealed { get; private set; }

    /// <summary>
    /// Stands a lever at <paramref name="at"/>, wired to <paramref name="rails"/>.
    /// It arrives in a puff of white smoke and cannot be hit until it is out of it.
    /// <paramref name="restsLeft"/> is the way it leans before it is ever hit.
    /// </summary>
    public static RailSwitchLever Appear(GameObject prefab, Vector2 at, RailNetwork rails, bool restsLeft)
    {
        if (prefab == null) return null;

        var go = Instantiate(prefab, new Vector3(at.x, at.y, 0f), Quaternion.identity);
        var lever = go.GetComponent<RailSwitchLever>();
        if (lever == null)
        {
            Destroy(go);
            return null;
        }

        lever.Begin(rails, restsLeft);
        return lever;
    }

    private void Begin(RailNetwork rails, bool restsLeft)
    {
        _rails = rails;
        _restsLeft = restsLeft;
        if (body == null) body = GetComponent<SpriteRenderer>();
        _hitbox = GetComponent<Collider2D>();
        if (_hitbox != null) _hitbox.enabled = false;

        // Where the NPCs and the range targets draw, so the smoke covers it.
        if (body != null)
        {
            body.sortingLayerName = "Default";
            body.sortingOrder = NpcFx.NpcOrder;
            body.enabled = false;
        }

        ShowSetting();
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        NpcFx.SmokeCentredOn(transform.position, SmokeRadius, white: true);
        yield return new WaitForSeconds(RevealDelaySeconds);

        if (body != null) body.enabled = true;
        if (_hitbox != null) _hitbox.enabled = true;
        Revealed = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Revealed || Time.time < _readyAt) return;
        if (CollectibleOrb.CollectorFor(other) == null) return;

        TrialOrb.SpendShot(other);
        _readyAt = Time.time + RehitSeconds;
        Throw();
    }

    private void Throw()
    {
        if (_rails == null) return;

        _rails.Throw(color);
        ShowSetting();

        var audio = AudioManager.Instance;
        if (audio != null && throwSound != null) audio.PlaySFX(throwSound);

        if (_kick != null) StopCoroutine(_kick);
        _kick = StartCoroutine(Kick());
    }

    private void ShowSetting()
    {
        if (body == null || _rails == null) return;
        bool left = _rails.IsThrown(color) ? !_restsLeft : _restsLeft;
        body.sprite = left ? leanLeftSprite : leanRightSprite;
    }

    private IEnumerator Kick()
    {
        float elapsed = 0f;
        while (elapsed < KickSeconds)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(KickScale, 1f, elapsed / KickSeconds);
            transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
        _kick = null;
    }
}

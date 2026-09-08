using UnityEngine;

// A bead of venom shaken off a poisoned arrow in flight, left hanging in the air
// where the arrow was. Anything that touches it is poisoned; nothing else happens.
//
// This is what makes Venom Tip a PLACEMENT upgrade rather than a damage one. A
// poisoned arrow used to be worth exactly what it hit, so the Serpent's opening
// move was the same as everybody else's: aim at the thing in front of you. Now
// the shot's whole flight path is the contribution — a bead every three units,
// from the shield out to wherever it lands — so an arrow fired down the lane
// something is about to walk is worth more than the same arrow fired into it.
// Missing is still missing, but the line the miss drew stays on the board.
//
// The beads drift, slowly and gently upward, which is doing two jobs. It reads as
// vapour rather than as a mine, so a player is not asked to believe a solid green
// dot is hanging in the air; and it means a lane laid across a wolf's path slowly
// stops being exactly that lane, so laying it EARLY is not simply better than
// laying it late.
//
// One enemy is poisoned at most once by a given bead — the same rule PoisonCloud
// keeps, and for the same reason: a bead that re-poisoned everything standing in
// it every tick would be a damage aura, and the Serpent already has clouds for
// area. What a bead is worth is one application of the arrow's own venom.
//
// Lifetime is the Venom Tip TIER: 5 seconds at rank I, 10 at II, 15 at III. That
// is the only thing the later ranks change about the trail, and it is the right
// dial — a longer bead is a lane you can lay further ahead of what it is for.
public class PoisonTrailBubble : MonoBehaviour
{
    // How far a poisoned arrow travels between beads. Three units is roughly a
    // knight-to-mid-board shot dropping four of them, which is a readable dotted
    // line rather than a solid stripe.
    public const float DropEveryUnits = 3f;

    // A QUARTER the size of the sprite it is drawn from (owner, 2026-09-06). The
    // bead sprite is the one poison_bubble uses, which is sized to be readable as
    // a single effect on its own; a trail lays one of these every three units and
    // at full size a single arrow's flight covered the board in green. Small is
    // also what makes the trail read as a trail rather than as a wall.
    private const float Scale = 0.25f;

    // World-unit radius of the hitbox, set a little proud of the drawn bead. A
    // quarter-size bead is about 0.23 units across, so at 0.15 the reach is a hair
    // past its edge — the same trick FireField and PoisonCloud use, and it matters
    // more here than there because something this small is easy to look like it
    // was touched when it was not.
    private const float HitRadiusUnits = 0.15f;

    private const float DriftSpeed = 0.35f;
    private const float FadeSeconds = 0.6f;

    // Off the board and it is nobody's problem — the same bounds PoisonCloud uses
    private const float OffscreenX = 13f;
    private const float OffscreenY = 8f;

    private int _poisonDamage;
    private float _poisonDuration;
    private float _poisonTickRate;
    private string _ownerTag;

    private float _diesAt;
    private Vector2 _drift;
    private SpriteRenderer _renderer;
    private float _startAlpha;

    /// <summary>
    /// Leave a bead at <paramref name="position"/> carrying the same venom the
    /// arrow that shed it was carrying, so Virulence flows through the trail
    /// without the trail knowing Virulence exists.
    /// </summary>
    public static PoisonTrailBubble Drop(Vector2 position, float seconds, int poisonDamage,
        float poisonDuration, float poisonTickRate, string ownerTag, int index)
    {
        Sprite sprite = PoisonResourceManager.Instance != null
            ? PoisonResourceManager.Instance.GetPoisonBubbleSprite()
            : null;
        if (sprite == null) return null;

        var go = new GameObject("PoisonTrailBubble");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * Scale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;

        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        // Authored in WORLD units and divided back out, because the collider is
        // scaled by the transform above — write 0.15 here and the bead would
        // actually reach 0.0375
        circle.radius = HitRadiusUnits / Scale;

        // Kinematic and simulated: the bead moves under its own drift, and a
        // collider dragged by a transform with no body of its own is the static-
        // collider re-bake Unity asks you not to make.
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;

        var bubble = go.AddComponent<PoisonTrailBubble>();
        bubble._poisonDamage = poisonDamage;
        bubble._poisonDuration = poisonDuration;
        bubble._poisonTickRate = poisonTickRate;
        bubble._ownerTag = ownerTag;
        bubble._diesAt = Time.time + Mathf.Max(0.5f, seconds);
        bubble._renderer = renderer;
        bubble._startAlpha = renderer.color.a;

        // Fixed, not rolled — the no-randomness pillar reaches this far down.
        // Alternating the lean means a trail reads as a line of beads that are
        // wandering rather than a column marching the same way.
        float lean = (index % 2 == 0) ? 0.45f : -0.45f;
        bubble._drift = new Vector2(lean, 1f).normalized * DriftSpeed;

        return bubble;
    }

    private void Update()
    {
        transform.position += (Vector3)(_drift * Time.deltaTime);

        float left = _diesAt - Time.time;
        if (left <= 0f
            || Mathf.Abs(transform.position.x) > OffscreenX
            || Mathf.Abs(transform.position.y) > OffscreenY)
        {
            Destroy(gameObject);
            return;
        }

        // Thins out over its last beat rather than blinking out, so a bead that is
        // about to stop working looks like one
        if (_renderer != null && left < FadeSeconds)
        {
            Color c = _renderer.color;
            c.a = _startAlpha * (left / FadeSeconds);
            _renderer.color = c;
        }
    }

    // Poisons whatever walks into it, once, and is spent doing so. Spent rather
    // than persistent because a bead is one application of one arrow's venom —
    // see the note at the top about why it is not an aura.
    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy == null || enemy.IsDead) return;

        enemy.ApplyPoisonFromTag(_poisonDamage, _poisonDuration, _poisonTickRate, _ownerTag);
        Destroy(gameObject);
    }
}

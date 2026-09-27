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
// Lifetime is the Venom Tip TIER: 1 second at rank I, 3 at II, 5 at III. That
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

    // Drawn part-transparent (owner, 2026-09-16). A bead is vapour shaken off an
    // arrow, and at full opacity a trail read as a row of solid green dots sitting
    // on top of the board rather than hanging in it. Half-and-a-bit keeps it
    // readable as a lane without hiding what is walking through it.
    private const float BeadAlpha = 0.55f;

    // Serpent's Breath beads are a quarter more see-through again (owner,
    // 2026-09-23): 0.55 x 0.75. A swing throws three at a time from rank II, so
    // they sit over the fight in a way a trail's scattered dots do not.
    private const float BreathBeadAlpha = 0.41f;

    // Off the board and it is nobody's problem — the same bounds PoisonCloud uses
    private const float OffscreenX = 13f;
    private const float OffscreenY = 8f;

    private int _poisonDamage;
    private float _poisonDuration;
    private float _poisonTickRate;
    private string _ownerTag;
    private EnemyBase _source; // Airborne Virus: the body that breathed this bead out

    private float _diesAt;
    private Vector2 _drift;
    private SpriteRenderer _renderer;
    private float _startAlpha;

    // Plaguebringer (owner, 2026-09-26): a bead made by a knight who owns it hunts,
    // the way that knight's clouds do - it turns onto the nearest on-screen mob
    // within PoisonCloud.HomingRadius and keeps that mob until it dies or leaves the
    // view. A bead is spent on the first body it touches, so reaching the mob is the
    // whole chase: it never eases off the way a cloud settling over one does.
    //
    // It never slows down to hunt. A thrown bead turns at its own pace, and only a
    // bead drifting slower than the mob can walk speeds up, to that pace plus the
    // cloud's margin, so it always gains.
    private bool _homing;
    private EnemyBase _homingTarget;
    private Collider2D _homingBody;
    private float _nextHomingSearch;

    /// <summary>
    /// Leave a bead at <paramref name="position"/> carrying the same venom the
    /// arrow that shed it was carrying, so the knight's tick bonus flows through
    /// the trail without the trail knowing it exists.
    /// </summary>
    public static PoisonTrailBubble Drop(Vector2 position, float seconds, int poisonDamage,
        float poisonDuration, float poisonTickRate, string ownerTag, int index)
    {
        // Fixed, not rolled - the no-randomness pillar reaches this far down.
        // Alternating the lean means a trail reads as a line of beads that are
        // wandering rather than a column marching the same way.
        float lean = (index % 2 == 0) ? 0.45f : -0.45f;
        Vector2 drift = new Vector2(lean, 1f).normalized * DriftSpeed;

        return Create(position, seconds, poisonDamage, poisonDuration, poisonTickRate,
                      ownerTag, drift, BeadAlpha);
    }

    /// <summary>
    /// A bead the knight THROWS rather than sheds: Serpent's Breath exhales these
    /// along the shield facing, so this one travels a chosen way at a chosen pace
    /// instead of hanging where it was made and wafting upward.
    ///
    /// It is otherwise exactly a bead - same size, same single application, spent
    /// on the first body it touches. That is deliberate: the Serpent has one venom
    /// vocabulary, and a swing that exhales the same thing an arrow sheds is
    /// legible the moment a player has seen either.
    /// </summary>
    public static PoisonTrailBubble Launch(Vector2 position, Vector2 direction, float speed,
        float seconds, int poisonDamage, float poisonDuration, float poisonTickRate,
        string ownerTag)
    {
        return Create(position, seconds, poisonDamage, poisonDuration, poisonTickRate,
                      ownerTag, direction.normalized * speed, BreathBeadAlpha);
    }

    /// <summary>
    /// A bead a poisoned ENEMY breathes out (Airborne Virus). Thrown exactly like
    /// a Serpent's Breath bead, except it never lands on the enemy it came out
    /// of - it is born inside that body, and would otherwise be spent on the
    /// spot re-poisoning its own source.
    /// </summary>
    public static PoisonTrailBubble Release(Vector2 position, Vector2 direction, float speed,
        float seconds, int poisonDamage, float poisonDuration, float poisonTickRate,
        string ownerTag, EnemyBase source)
    {
        PoisonTrailBubble bubble = Create(position, seconds, poisonDamage, poisonDuration,
            poisonTickRate, ownerTag, direction.normalized * speed, BreathBeadAlpha);
        if (bubble != null) bubble._source = source;
        return bubble;
    }

    private static PoisonTrailBubble Create(Vector2 position, float seconds, int poisonDamage,
        float poisonDuration, float poisonTickRate, string ownerTag, Vector2 drift, float alpha)
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
        // Set before _startAlpha is captured below, so the dying fade thins out
        // from THIS alpha rather than snapping up to full first
        renderer.color = new Color(1f, 1f, 1f, alpha);

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
        bubble._drift = drift;
        bubble._homing = PoisonCloud.OwnerHasPlaguebringer(ownerTag);

        return bubble;
    }

    private void Update()
    {
        if (_homing) Home();

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

    // Steers `_drift` toward the current mob. With no mob in reach the bead keeps
    // whatever drift it has, so it carries on exactly as a plain bead would.
    private void Home()
    {
        if (!IsWorthChasing(_homingTarget, _homingBody))
        {
            _homingTarget = null;
            _homingBody = null;
            FindHomingTarget();
        }

        if (_homingTarget == null) return;

        // The body's middle, not its transform - enemy pivots sit at the feet
        Vector2 toTarget = (Vector2)_homingBody.bounds.center - (Vector2)transform.position;
        float distance = toTarget.magnitude;
        if (distance < 1e-4f) return;

        float chaseSpeed = Mathf.Max(_drift.magnitude,
            _homingTarget.MeasuredSpeed + PoisonCloud.HomingSpeedMargin);
        _drift = Vector2.MoveTowards(_drift, toTarget / distance * chaseSpeed,
            PoisonCloud.HomingAcceleration * Time.deltaTime);
    }

    // Not the body it came out of (Airborne Virus), and not one venom cannot touch -
    // a cart would spend the bead and take nothing from it
    private bool IsWorthChasing(EnemyBase enemy, Collider2D body)
    {
        return enemy != null && !enemy.IsDead && enemy != _source
            && !enemy.ImmuneToAreaDamage
            && body != null && body.enabled
            && FireField.IsInsideView(body.bounds.center);
    }

    private void FindHomingTarget()
    {
        if (Time.time < _nextHomingSearch) return;
        _nextHomingSearch = Time.time + PoisonCloud.HomingSearchInterval;

        Vector2 here = transform.position;
        Collider2D[] hits = PoisonCloud.HomingHits;
        int count = Physics2D.OverlapCircle(here, PoisonCloud.HomingRadius,
            PoisonCloud.HomingFilter, hits);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (!IsWorthChasing(enemy, hit)) continue;

            float distance = ((Vector2)hit.bounds.center - here).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            _homingTarget = enemy;
            _homingBody = hit;
        }
    }

    // Poisons whatever walks into it, once, and is spent doing so. Spent rather
    // than persistent because a bead is one application of one arrow's venom —
    // see the note at the top about why it is not an aura.
    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy == null || enemy.IsDead || enemy == _source) return;

        enemy.ApplyPoisonFromTag(_poisonDamage, _poisonDuration, _poisonTickRate, _ownerTag);
        Destroy(gameObject);
    }
}

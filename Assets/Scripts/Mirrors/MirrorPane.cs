using UnityEngine;

// One mirror standing on the castle floor. A shot that touches it comes out of
// its twin.
//
// The pane is the castle's answer to the mine's RailTeleporter, and the two solve
// the same problem from opposite ends. A teleporter is INVISIBLE by construction:
// it sits off-frame, it carries no collider, and a cart consults it by distance
// travelled so the hand-off is never seen. A pane is the exact opposite — it is
// furniture in the middle of the board that the player aims at on purpose — so it
// catches its traffic with a trigger and has to answer a question a rail pad never
// does: what direction does the thing leave at?
//
// The transform holds the pane's CENTRE, not its feet. Every other prefab in the
// game pivots at the ground because that is where a thing stands, but a portal is
// not standing anywhere in the sense that matters: the redirect maths, the twin
// pairing and "aim through the middle of it" all key off the centre, and a wide
// pane authored the other way would silently mean something different from a tall
// one. The sprite is hung off a child, pushed down by half its height, so the art
// still sits where it was drawn to sit.
[RequireComponent(typeof(BoxCollider2D))]
public class MirrorPane : MonoBehaviour
{
    // How far past the twin's own surface a shot is put down. Small enough that
    // it is not a visible jump out of the glass, big enough that the shot is
    // genuinely outside the collider it is standing in.
    private const float Clearance = 0.12f;

    // A shot leaving along the pane's face rather than through it would need an
    // enormous push to clear the collider. Past this shallow it is treated as
    // having grazed the mirror instead of entered it, and it is left alone.
    private const float ShallowestExit = 0.2f;

    [SerializeField] private SpriteRenderer art;

    private MirrorRedirect _rule = MirrorRedirect.MirrorAboutFacing;
    private Sprite[] _frames;
    private float _shimmerInterval = 0.18f;
    private float _shimmerRest = 4.5f;
    private float _shimmerAt;
    private int _frame;
    private BoxCollider2D _catcher;

    /// <summary>Which twin this pane belongs to. Colour is the pairing.</summary>
    public MirrorColor PaneColor { get; private set; }

    public MirrorFacing Facing { get; private set; }

    /// <summary>The other pane of this colour. Null until MirrorNetwork pairs them.</summary>
    public MirrorPane Twin { get; private set; }

    /// <summary>Editor-only note copied off the layout entry.</summary>
    public string Label { get; private set; }

    /// <summary>Unit vector out of the front face.</summary>
    public Vector2 Normal => Facing.Normal();

    /// <summary>Unit vector along the face.</summary>
    public Vector2 Tangent => Facing.Tangent();

    public Vector2 Center => transform.position;

    /// <summary>Length of the working face — 2 world units for the tall pane.</summary>
    public float Span { get; private set; } = 2f;

    /// <summary>Half the pane's depth, measured along its normal.</summary>
    public float HalfThickness { get; private set; } = 0.5f;

    private void Awake()
    {
        _catcher = GetComponent<BoxCollider2D>();
        if (art == null) art = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Stand this pane up as one entry of a layout. Sizes the collider and turns
    /// the art to match the facing, so a single prefab covers both the tall pane
    /// and the wide one.
    /// </summary>
    public void Configure(string label, MirrorColor color, MirrorFacing facing,
                          MirrorRedirect rule, Sprite[] frames, float shimmerInterval,
                          float shimmerRest = 4.5f)
    {
        Label = label;
        PaneColor = color;
        Facing = facing;
        _rule = rule;
        _frames = frames;
        _shimmerInterval = Mathf.Max(0.02f, shimmerInterval);
        _shimmerRest = Mathf.Max(0f, shimmerRest);
        _frame = 0;
        _shimmerAt = Time.time + _shimmerRest;

        if (_catcher == null) _catcher = GetComponent<BoxCollider2D>();
        if (art == null) art = GetComponentInChildren<SpriteRenderer>();

        // Taken off the sprite rather than hardcoded, so retouching the art to a
        // different canvas moves the collider with it instead of leaving a pane
        // that catches shots a quarter of a unit away from where it is drawn.
        Sprite first = frames != null && frames.Length > 0 ? frames[0] : null;
        if (first != null)
        {
            Vector2 size = first.bounds.size;
            Span = Mathf.Max(size.x, size.y);
            HalfThickness = Mathf.Min(size.x, size.y) * 0.5f;
        }

        if (art != null)
        {
            if (first != null) art.sprite = first;

            // The art is drawn tall. A wide pane is that same art laid on its
            // side — one quarter turn, no second sprite to keep in step with the
            // first when a colour is retouched.
            bool tall = facing.IsTall();
            art.transform.localRotation = tall
                ? Quaternion.identity
                : Quaternion.AngleAxis(90f, Vector3.forward);

            // The sprite pivots at its base (the project's convention), so it is
            // hung half a pane below the centre this transform holds. On a wide
            // pane the same offset runs along the rotated axis.
            float drop = Span * 0.5f;
            art.transform.localPosition = tall
                ? new Vector3(0f, -drop, 0f)
                : new Vector3(drop, 0f, 0f);
            _restPosition = art.transform.localPosition;
            art.transform.localScale = Vector3.one;
        }

        if (_catcher != null)
        {
            _catcher.isTrigger = true;
            _catcher.offset = Vector2.zero;
            _catcher.size = facing.IsTall()
                ? new Vector2(HalfThickness * 2f, Span)
                : new Vector2(Span, HalfThickness * 2f);
        }
    }

    /// <summary>Point this pane at its opposite number. Called both ways round.</summary>
    public void PairWith(MirrorPane twin)
    {
        Twin = twin;
    }

    // The shimmer, on a fixed cadence. The first frame is the glass at rest and
    // the others are a glint crossing it: the pane sits still for the rest, the
    // glint runs through once, and it settles again. Played end to end without
    // the rest it read as a strobe. A pane with a single frame is simply still.
    //
    // A counter rather than a roll, like every other cadence in the game (see the
    // no-randomness pillar) — two panes of the same colour laid in the same frame
    // stay in step with each other, which is part of reading them as one pair.
    private void Update()
    {
        TickPulse();

        if (_frames == null || _frames.Length < 2 || art == null) return;
        if (Time.time < _shimmerAt) return;

        _frame = (_frame + 1) % _frames.Length;
        // Back on the resting frame: hold it before the next glint
        _shimmerAt = Time.time + (_frame == 0 ? _shimmerRest : _shimmerInterval);
        art.sprite = _frames[_frame];
    }

    // A pane that has just taken a shot, or given one back, swells for a moment
    // and settles. It is the pane's half of MirrorFx.Passage: the spray says
    // where the shot went, this says which pane did it.
    //
    // The art hangs off its feet (see Configure), so the offset is scaled along
    // with the sprite — otherwise the pane would grow away from its own centre.
    private const float PulseSeconds = 0.2f;
    private const float PulseSwell = 0.16f;
    private float _pulseAt = -10f;
    private bool _pulsing;
    private Vector3 _restPosition;

    public int SortingLayerId => art != null ? art.sortingLayerID : 0;
    public int SortingOrder => art != null ? art.sortingOrder : 0;

    public void Pulse()
    {
        _pulseAt = Time.time;
    }

    private void TickPulse()
    {
        if (art == null) return;

        float t = (Time.time - _pulseAt) / PulseSeconds;
        if (t >= 1f)
        {
            if (!_pulsing) return;
            _pulsing = false;
            art.transform.localScale = Vector3.one;
            art.transform.localPosition = _restPosition;
            return;
        }

        _pulsing = true;
        // Out fast, back slow
        float swell = 1f + PulseSwell * (1f - t) * (1f - t);
        art.transform.localScale = new Vector3(swell, swell, 1f);
        art.transform.localPosition = _restPosition * swell;
    }

    // Only projectiles go through. Enemies, knights and pickups are turned away
    // by construction rather than by a tag check: the two components looked for
    // here are what MOVES a shot in a straight line, and nothing that walks or
    // falls or is picked up carries either of them.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Twin == null || other == null) return;

        // A shuriken keeps its PlayerProjectile on a child, so the collider that
        // fired is not necessarily the object that flies. The body is.
        GameObject shot = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        Vector2 direction;
        float speed;
        Rigidbody2D body;
        if (!TryReadFlight(shot, out direction, out speed, out body)) return;

        MirrorPassenger passenger = MirrorPassenger.For(shot);
        if (!passenger.MayEnter(this)) return;

        // Which face it came in through. Negative means it is travelling against
        // the normal, so it went into the front.
        float approach = Vector2.Dot(direction, Normal);
        if (Mathf.Abs(approach) < ShallowestExit) return; // running along the glass, not through it
        float side = Mathf.Sign(approach);

        Vector2 exitDirection;
        Vector2 exitTangent;

        if (_rule == MirrorRedirect.MirrorAboutFacing)
        {
            // The turn that carries this pane's entry face onto the twin's exit
            // face. Enter the front and you leave the front; enter the back and
            // you leave the back. Applied to the tangent as well as the heading,
            // so where along the mirror a shot went in survives the trip.
            Vector2 from = side * Normal;
            Vector2 to = -side * Twin.Normal;
            exitDirection = Turn(direction, from, to);
            exitTangent = Turn(Tangent, from, to);
        }
        else
        {
            exitDirection = direction;
            exitTangent = Twin.Tangent;
        }

        // How far along this pane's face the shot crossed, carried over to the
        // twin so a shot that enters near the top of one comes out near the top
        // of the other. Clamped because the trigger fires on the collider's edge
        // and a fast shot can be a little past it by the time this runs.
        float half = Span * 0.5f;
        float along = Vector2.Dot((Vector2)shot.transform.position - Center, Tangent);
        along = Mathf.Clamp(along, -half, half);

        // Put down past the twin's far surface. The push runs along the exit
        // heading, so it is divided by how squarely that heading leaves the pane
        // — a shot slicing out at an angle needs more of it to get the same
        // distance clear of the glass.
        float squareness = Mathf.Max(Mathf.Abs(Vector2.Dot(exitDirection, Twin.Normal)), ShallowestExit);
        float push = (Twin.HalfThickness + Clearance) / squareness;

        Vector2 exit = Twin.Center + exitTangent * along + exitDirection * push;
        Vector2 entry = shot.transform.position;

        shot.transform.position = new Vector3(exit.x, exit.y, shot.transform.position.z);
        WriteFlight(shot, exitDirection, speed, body);
        passenger.NoteExit(Twin);

        // Said at both ends, in the pair's colour, so the trip can be read
        MirrorFx.Passage(this, entry, direction, Twin, exit, exitDirection);
        Pulse();
        Twin.Pulse();
    }

    // Where a shot is going and how fast. Two families of projectile fly in this
    // game and they are moved by different machinery:
    //
    //   * the knights' arrows, shadow arrows and shurikens ride a Rigidbody2D
    //     velocity set by PlayerShooter;
    //   * the rats' and gnomes' rocks carry ProjectileMovement, which translates
    //     along its own local +X every frame and so keeps its heading in its
    //     rotation.
    //
    // Anything that steers itself is deliberately left out. EnemyFireball homes
    // and orbits on a heading it owns privately, and a pane that moved it would
    // be arguing with its own guidance a frame later.
    private static bool TryReadFlight(GameObject shot, out Vector2 direction, out float speed, out Rigidbody2D body)
    {
        direction = Vector2.zero;
        speed = 0f;
        body = shot.GetComponent<Rigidbody2D>();

        if (shot.GetComponent<PlayerProjectile>() != null && body != null)
        {
            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                speed = velocity.magnitude;
                direction = velocity / speed;
                return true;
            }
        }

        ProjectileMovement mover = shot.GetComponent<ProjectileMovement>();
        if (mover != null)
        {
            direction = shot.transform.right;
            speed = mover.Speed;
            return direction.sqrMagnitude > 0.0001f;
        }

        return false;
    }

    // Heading lives in the rotation for both families — ProjectileMovement reads
    // it directly, and the arrows' sprites are drawn pointing along +X — so the
    // rotation is set either way and the velocity only when there is one.
    private static void WriteFlight(GameObject shot, Vector2 direction, float speed, Rigidbody2D body)
    {
        float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        shot.transform.rotation = Quaternion.AngleAxis(degrees, Vector3.forward);

        if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f)
        {
            body.linearVelocity = direction * speed;
        }
    }

    // Rotate `v` by the turn that takes unit vector `from` onto unit vector `to`.
    private static Vector2 Turn(Vector2 v, Vector2 from, Vector2 to)
    {
        float cos = Vector2.Dot(from, to);
        float sin = from.x * to.y - from.y * to.x;
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }

#if UNITY_EDITOR
    // A pane's facing is the one thing about it that cannot be seen in the Scene
    // view — the art is symmetrical and both faces work — so it is drawn.
    private void OnDrawGizmos()
    {
        Vector2 centre = transform.position;
        Vector2 tangent = Facing.Tangent();
        float half = Span * 0.5f;

        Gizmos.color = GizmoColor(PaneColor);
        Gizmos.DrawLine(centre - tangent * half, centre + tangent * half);
        Gizmos.DrawLine(centre, centre + Facing.Normal());

        if (Twin != null)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(centre, Twin.transform.position);
        }
    }

    private static Color GizmoColor(MirrorColor color)
    {
        switch (color)
        {
            case MirrorColor.Cyan: return new Color(0.13f, 0.52f, 0.58f);
            case MirrorColor.Vermilion: return new Color(0.61f, 0.22f, 0.16f);
            case MirrorColor.Magenta: return new Color(0.57f, 0.18f, 0.48f);
            default: return new Color(0.16f, 0.54f, 0.32f);
        }
    }
#endif
}

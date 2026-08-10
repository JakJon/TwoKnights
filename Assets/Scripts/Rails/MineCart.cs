using System.Collections.Generic;
using UnityEngine;

// A cart riding the mine's track. It follows the RailLine it was put on and
// nothing else — which way that stretch of track faces is authored on the
// layout, so a cart never decides its own direction.
//
// It rides the line rather than the pieces, which is what lets a wave release
// a cart the instant the track is asked for, while the rails behind it are
// still cascading into place.
//
// Distance is measured from the lead-in point, a little behind the mouth of
// the line, so the cart rolls in from off-screen already at speed and rolls
// out the far side before it despawns.
[RequireComponent(typeof(SpriteRenderer))]
public class MineCart : MonoBehaviour
{
    /// <summary>
    /// The speed every cart in the mine runs at, in world units per second.
    ///
    /// A CONSTANT rather than a per-wave knob, deliberately. Spacing on a loop is
    /// frozen by speed (see BitsAndPieces), the blast and coupling radii on a keg
    /// are measured in units of one release pause, and a rider's throw cooldown is
    /// tuned against how long a lap takes — so a wave that ran its carts faster
    /// would quietly retune half the mine's geometry along with it. One speed
    /// keeps every one of those numbers meaning the same thing in every wave.
    /// </summary>
    public const float TrackSpeed = 2.4f;

    [Header("Motion")]
    [Tooltip("World units per second along the track")]
    [SerializeField] private float speed = TrackSpeed;

    [Tooltip("Start this far back from the line's first cell, so the cart is already moving when it enters frame")]
    [SerializeField] private float leadIn = 6f;

    [Tooltip("Keep rolling this far past the line's last cell before despawning")]
    [SerializeField] private float runOut = 2f;

    [Tooltip("Lift off the rail piece's pivot. The cart art is drawn top-down on the same 32px grid as the rail, so 0 lands its wheels exactly on the two rail bands.")]
    [SerializeField] private float railOffset = 0f;

    [Header("Presentation")]
    [Tooltip("Leave empty to use this object's own renderer")]
    [SerializeField] private SpriteRenderer body;

    [Tooltip("The art is drawn facing east; westward travel mirrors it")]
    [SerializeField] private bool artFacesEast = true;

    [Header("Spacing")]
    [Tooltip("Clearance kept between two carts on top of their own half-lengths")]
    [SerializeField] private float spacingPadding = 0.05f;

    private RailLine _line;
    private float _travelled;
    private bool _riding;

    public bool IsRiding => _riding;
    public RailLine Line => _line;

    /// <summary>
    /// Is there actually laid track under this cart? False while it is still
    /// leading in from off-screen or rolling out the far side. The distinction is
    /// what decides who gives way when two carts meet — see ResolveSpacing.
    /// </summary>
    public bool IsOnRail => _riding && Along >= 0f && Along <= _line.Length;

    /// <summary>
    /// Distance from the mouth of the current line. Negative while the cart is
    /// still leading in from off-screen. This plus Line is the whole ride state,
    /// so a cart can be handed off to a replacement mid-track.
    /// </summary>
    public float Along => _travelled - leadIn;

    /// <summary>
    /// Raised after the cart has been picked up by a teleporter and set down
    /// somewhere else. Riders that watch the cart's position for a crossing must
    /// listen, or the jump reads as a crossing it never made.
    /// </summary>
    public event System.Action Teleported;

    /// <summary>Units per second. Set before Ride, or pass a speed to Ride.</summary>
    public float Speed
    {
        get { return speed; }
        set { speed = value; }
    }

    private float _halfHeight = 0.5f;
    private float _halfLength = 0.5f;

    private void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (body != null && body.sprite != null)
        {
            _halfHeight = body.sprite.bounds.extents.y;
            // Extent ALONG the track. The art is drawn facing east, and a cart on
            // a shaft is turned a quarter turn rather than redrawn, so the sprite's
            // own width is the length of track it occupies either way.
            _halfLength = body.sprite.bounds.extents.x;
        }
    }

    private void OnEnable()
    {
        if (!Live.Contains(this)) Live.Add(this);
    }

    private void OnDisable()
    {
        Live.Remove(this);
    }

    /// <summary>Put this cart on <paramref name="line"/> at its own speed.</summary>
    public void Ride(RailLine line)
    {
        Ride(line, speed);
    }

    /// <summary>Put this cart on <paramref name="line"/> and start it rolling.</summary>
    public void Ride(RailLine line, float ridingSpeed)
    {
        Resume(line, -leadIn, ridingSpeed);
    }

    /// <summary>
    /// Put this cart on <paramref name="line"/> already <paramref name="along"/>
    /// units past its mouth. Used to hand a ride over — a gnome cart's wreck picks
    /// up exactly where the gnome left off — and by the teleporter seam.
    /// </summary>
    public void Resume(RailLine line, float along, float ridingSpeed)
    {
        _line = line;
        speed = ridingSpeed;
        _travelled = along + leadIn;
        _riding = true;
        FaceAlong(line);
        Reposition();
    }

    /// <summary>Leave the cart where it is. It will not resume on its own.</summary>
    public void Halt()
    {
        _riding = false;
    }

    /// <summary>
    /// Stop advancing for <paramref name="seconds"/> without leaving the line.
    /// This is the hit-pause, and it has to live here: the cart's position is
    /// owned by this component, so EnemyBase's isStaggered flag on the RIDER
    /// cannot produce a visible stop on its own.
    ///
    /// Deliberately not Halt() — the cart is still riding, still spaced against
    /// its neighbours, and still leaves a wreck if the rider dies mid-flinch.
    /// Overlapping holds take the longest, never the latest, so a second hit
    /// during a flinch cannot cut the first one short.
    /// </summary>
    public void HoldFor(float seconds)
    {
        if (seconds <= 0f) return;
        _heldUntil = Mathf.Max(_heldUntil, Time.time + seconds);
    }

    private float _heldUntil;

    private void Update()
    {
        if (!_riding) return;
        if (Time.time < _heldUntil) return;

        _travelled += speed * Time.deltaTime;
        Reposition();

        // A pad on this line owns the far end: while one is wired up the cart is
        // relayed instead of despawning, and the layout decides where it goes
        RailTeleporter pad = RailNetwork.Instance != null
            ? RailNetwork.Instance.ExitPadFor(_line.Index)
            : null;

        if (pad != null && pad.Destination != null)
        {
            if (Along >= pad.DistanceAlong) TakePad(pad);
            return;
        }

        if (Along > _line.Length + runOut) Destroy(gameObject);
    }

    // The seam. Whatever the cart had overrun the pad by this frame is carried
    // over to the far side, so the loop keeps perfect pace no matter the frame
    // it lands on — a cart never hitches or gains ground at the join.
    private void TakePad(RailTeleporter pad)
    {
        RailTeleporter destination = pad.Destination;
        RailLine target;
        if (RailNetwork.Instance == null
            || !RailNetwork.Instance.TryGetLine(destination.LineIndex, out target))
        {
            Destroy(gameObject);
            return;
        }

        float overshoot = Along - pad.DistanceAlong;
        Resume(target, destination.DistanceAlong + overshoot, speed);

        var handler = Teleported;
        if (handler != null) handler();
    }

    private void Reposition()
    {
        Vector2 point = _line.PointAt(_travelled - leadIn);
        var onRail = new Vector3(point.x, point.y + railOffset, transform.position.z);

        // The art pivots at its base, which is what puts its wheels on the two
        // bands of a horizontal rail. A quarter turn for a vertical run pivots
        // about that same base and swings the whole body out to one side of the
        // track, so walk the pivot back to wherever it has to be for the BODY to
        // finish centred on the cell. Identity rotation cancels exactly, so
        // horizontal track is untouched by this.
        var bodyFromPivot = new Vector3(0f, _halfHeight, 0f);
        transform.position = onRail + bodyFromPivot - transform.rotation * bodyFromPivot;
    }

    // ---- Spacing ----
    //
    // Two hundred kilos of iron does not pass through two hundred kilos of iron.
    // Carts sharing a run are kept at least their own length apart, which they
    // could not do on their own: each one is following an authored line at an
    // authored speed, and nothing in that arrangement knows the cart in front
    // exists. Without this a wreck handed a dead gnome's exact position and speed
    // sits inside whatever was behind him, and a train released onto a ring that
    // is already full drives straight through the queue.
    //
    // Right of way goes to whoever has track under them. Two carts both on the
    // rail share the correction and shoulder each other apart; if only one is on
    // the rail, the other absorbs the whole of it — which reads as waiting at the
    // mouth for a gap when it is behind, and being shoved along when it is ahead
    // and already rolling out. A halted cart is furniture and never moves at all.
    private static readonly List<MineCart> Live = new List<MineCart>();
    private static int _resolvedFrame = -1;

    // Every cart runs this; the first one to reach it each frame does the sweep
    // for all of them, so the cost is one pass however many are on the track.
    private void LateUpdate()
    {
        if (_resolvedFrame == Time.frameCount) return;
        _resolvedFrame = Time.frameCount;
        ResolveSpacing();
    }

    /// <summary>
    /// The nearest cart ahead of <paramref name="of"/> on the same run, within
    /// <paramref name="reach"/> units. "Ahead" is in travel order, so it means
    /// the same thing on a westbound run as on an eastbound one.
    ///
    /// It lives here because the cart list and the spacing rules do: "what is in
    /// front of me" is the question ResolveSpacing already answers every frame,
    /// and a second copy of that list somewhere else would eventually be a second
    /// answer.
    /// </summary>
    public static MineCart FirstAhead(MineCart of, float reach)
    {
        if (of == null || !of._riding) return null;

        MineCart nearest = null;
        float nearestGap = float.MaxValue;

        for (int i = 0; i < Live.Count; i++)
        {
            MineCart other = Live[i];
            if (other == null || other == of || !other._riding) continue;
            if (other._line.Index != of._line.Index) continue;

            float gap = other.Along - of.Along;
            if (gap <= 0f || gap > reach) continue;

            if (gap < nearestGap)
            {
                nearestGap = gap;
                nearest = other;
            }
        }

        return nearest;
    }

    private static void ResolveSpacing()
    {
        // Two passes settles a queue of carts arriving in any order; a single one
        // can leave the third cart in a stack still touching the second.
        const int Passes = 2;

        Live.RemoveAll(cart => cart == null);

        for (int pass = 0; pass < Passes; pass++)
        {
            bool settled = true;

            for (int i = 0; i < Live.Count; i++)
            {
                MineCart a = Live[i];

                for (int j = i + 1; j < Live.Count; j++)
                {
                    MineCart b = Live[j];

                    // Only carts on the same run can be measured against each
                    // other: distance along one line says nothing about another.
                    if (!a._riding && !b._riding) continue;
                    if (a._line.Index != b._line.Index) continue;

                    float clearance = a._halfLength + b._halfLength
                                      + Mathf.Max(a.spacingPadding, b.spacingPadding);

                    float delta = b.Along - a.Along;
                    float overlap = clearance - Mathf.Abs(delta);
                    if (overlap <= 0f) continue;

                    settled = false;

                    // Which way each has to go to get clear. Exactly coincident
                    // carts are split along the line's own direction rather than
                    // an arbitrary side.
                    float sign = delta >= 0f ? 1f : -1f;

                    if (a._riding && b._riding)
                    {
                        if (a.IsOnRail == b.IsOnRail)
                        {
                            a.Nudge(-sign * overlap * 0.5f);
                            b.Nudge(sign * overlap * 0.5f);
                        }
                        else if (a.IsOnRail)
                        {
                            b.Nudge(sign * overlap);
                        }
                        else
                        {
                            a.Nudge(-sign * overlap);
                        }
                    }
                    else if (a._riding)
                    {
                        a.Nudge(-sign * overlap);
                    }
                    else
                    {
                        b.Nudge(sign * overlap);
                    }
                }
            }

            if (settled) break;
        }
    }

    // Shift along the current line without disturbing anything else about the
    // ride — same line, same speed, same facing. Only the spacing sweep uses it.
    private void Nudge(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        _travelled += delta;
        Reposition();
    }

    private void FaceAlong(RailLine line)
    {
        Vector2 direction = line.Direction;

        if (line.Axis == RailAxis.Vertical)
        {
            // The art is top-down, so turning it a quarter turn genuinely puts
            // the cart along a shaft — the wheels end up on what will be the
            // vertical piece's two rails once that art exists.
            transform.rotation = Quaternion.Euler(0f, 0f, direction.y >= 0f ? 90f : -90f);
            return;
        }

        transform.rotation = Quaternion.identity;
        if (body != null) body.flipX = artFacesEast ? direction.x < 0f : direction.x > 0f;
    }
}

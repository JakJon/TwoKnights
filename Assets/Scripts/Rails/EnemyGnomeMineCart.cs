using UnityEngine;

// The bomb thrower: a gnome who sends a bomb at the exact midpoint between the
// two knights, and only there — over the side from the track above them, or
// lobbed up from the track below.
//
// The threat is positional and the player can read it off the track — a cart
// approaching the centre of the screen is the tell. Release is driven by the
// CROSSING of the midpoint, not by "am I near it": a proximity test fires on
// whichever frame happens to land inside the window, which at cart speed is a
// different spot every lap. Crossing detection puts the bomb on the centre line
// every single time, which is what makes the blast geometry (EnemyBomb) honest.
public class EnemyGnomeMineCart : EnemyGnomeCart
{
    [Header("Bomb")]
    [Tooltip("Dropped at the exact midpoint between the knights. Needs an EnemyBomb.")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("Must be at least this far above OR below the knights to throw. A bomb released level with them would arrive before anyone could shoot it, which is the one way it is allowed to be unanswerable.")]
    [SerializeField] private float minVerticalClearance = 1.5f;

    private static readonly ThrowGate SharedGate = new ThrowGate();

    private float _lastSide; // which side of the midpoint the cart was on last frame
    private float _midX;
    private bool _crossedThisFrame;

    protected override ThrowGate Gate => SharedGate;
    protected override string ThrowName => "bomb";

    // Every frame, cooldown or not. A crossing lasts exactly one frame, so if this
    // only ran while the gates were open the tracker would come back from a
    // cooldown with a stale side and read the first frame as a crossing.
    protected override void TrackPosition()
    {
        _crossedThisFrame = false;
        if (!TryKnightMidpoint(out _midX)) return;

        float side = Mathf.Sign(transform.position.x - _midX);

        // _lastSide is 0 only on the first frame after a spawn or a teleport,
        // which is exactly when there is no crossing to speak of
        _crossedThisFrame = _lastSide != 0f && side != _lastSide;
        _lastSide = side;
    }

    protected override bool ShouldRelease()
    {
        // A circuit layout runs track UNDER the knights as well as over them, and
        // the return leg crosses the midpoint just like the top one does. Both
        // legs throw: the bomb travels towards the knights' level rather than
        // only falling (see EnemyBomb), so the lower one lobs upward and the
        // whole loop is live instead of just its top edge.
        return _crossedThisFrame && HasThrowClearance();
    }

    // Distance, not height — the rule is the same either side of the knights. It
    // is the fuse the clearance buys that matters, and a bomb thrown from a unit
    // away arrives in a heartbeat whichever direction it came from.
    private bool HasThrowClearance()
    {
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        if (left == null) return true;

        Collider2D body = left.GetComponent<Collider2D>();
        float knightY = body != null ? body.bounds.center.y : left.transform.position.y;
        return Mathf.Abs(transform.position.y - knightY) >= minVerticalClearance;
    }

    protected override void Release()
    {
        if (bombPrefab == null) return;

        // Snapped to the midpoint rather than dropped from wherever the cart sat
        // this frame: at cart speed that is a few centimetres of slop, and the
        // bomb's whole contract is that it falls down the centre line.
        Vector3 at = ThrowPoint;
        at.x = _midX;
        Instantiate(bombPrefab, at, Quaternion.identity);
    }

    // The teleporter throws the cart from one edge of the screen to the other,
    // which sails straight across the midpoint. Without this the gnome would drop
    // a bomb off-frame on every lap.
    protected override void HandleTeleported()
    {
        _lastSide = 0f;
        _crossedThisFrame = false;
    }
}

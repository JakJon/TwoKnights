using System.Collections.Generic;
using UnityEngine;

// A pickaxe hurled from a mine cart at the nearer knight, tumbling end over end.
//
// It flies a STOUT arc — enough loft to read as thrown rather than fired, not so
// much that it hangs. That shape comes from solving for the launch velocity that
// puts it on the knight in a fixed flight time under a fixed gravity, so the arc
// looks the same whether the cart is directly overhead or right at the edge of
// the frame. Tuning `flightTime` changes how lazy the throw is; tuning
// `gravity` changes how high it peaks on the way.
//
// One hit point, like the bomb: anything the knights hold stops it. An arrow
// knocks it out of the air and flies on; a sword bats it back the way the swing
// faces. Unlike the bomb it has no blast — it either lands on a knight or it does
// not, so the answer is the shield as much as the bow.
public class EnemyPickaxe : MonoBehaviour, IChillable, IOwnedIncoming
{
    [Header("Flight")]
    [Tooltip("Seconds from the throw to arriving on target. Shorter = flatter, meaner.")]
    [SerializeField] private float flightTime = 1.1f;

    [Tooltip("Downward acceleration. Higher makes the same flight time peak higher.")]
    [SerializeField] private float gravity = 16f;

    [Tooltip("Degrees per second of tumble. Signed by the direction of travel.")]
    [SerializeField] private float spinSpeed = 540f;

    [Tooltip("Killed this long after the throw no matter what, so a miss cannot linger")]
    [SerializeField] private float maxLifetime = 6f;

    [Header("Damage")]
    [SerializeField] private int knightDamage = 12;

    [Tooltip("Shown on the death screen")]
    [SerializeField] private string sourceName = "a Gnome's Pickaxe";

    private Vector2 _velocity;
    private float _spin;
    private bool _spent;

    // Where it was at the end of the last frame, so a hit on a knight can ask
    // whether the axe came through that knight's guard on the way in
    private Vector2 _lastPosition;
    private CircleCollider2D _body;
    private static readonly List<RaycastHit2D> SweepHits = new List<RaycastHit2D>();
    private static readonly List<Collider2D> TouchHits = new List<Collider2D>();

    // Who it is meant for and when it lands there, for the IncomingLedger. Only
    // known when the thrower names the knight rather than a bare point.
    private Transform _knight;
    private float _flightLeft;
    private object _owner;

    // How fast this one plays out against the prefab's own throw: 0.5 is half as
    // fast. It scales the axe's CLOCK, like a chill does, so the arc is exactly
    // the one the prefab throws and only the time it takes changes.
    private float _pace = 1f;

    /// <summary>Seconds from the throw to arriving on target, unchilled.</summary>
    public float FlightTime => Mathf.Max(0.05f, flightTime);

    /// <summary>
    /// Throw one at <paramref name="target"/>. Solves the launch velocity rather
    /// than taking one, so callers only have to say where the cart is and who it
    /// is aiming at — the arc is the prefab's business.
    /// </summary>
    public static EnemyPickaxe Throw(GameObject prefab, Vector3 from, Vector3 target)
    {
        return Throw(prefab, from, target, null);
    }

    /// <summary>
    /// Throw one at <paramref name="target"/>, naming the <paramref name="knight"/>
    /// it is meant for, so it shows up in the IncomingLedger while it flies and a
    /// thrower deciding on its own clock can see it coming.
    /// </summary>
    public static EnemyPickaxe Throw(GameObject prefab, Vector3 from, Vector3 target, Transform knight)
    {
        return Throw(prefab, from, target, knight, null);
    }

    /// <summary>
    /// Throw one on behalf of <paramref name="owner"/> — whatever the thrower
    /// booked its landing under (IncomingLedger), so its own axes in the air
    /// never read as something else in the way.
    /// </summary>
    public static EnemyPickaxe Throw(GameObject prefab, Vector3 from, Vector3 target, Transform knight, object owner)
    {
        return Throw(prefab, from, target, knight, owner, 1f);
    }

    /// <summary>
    /// Throw one at <paramref name="pace"/> times the prefab's speed — the same
    /// arc, taking 1 / pace as long. The Overseer throws at 0.5 (owner,
    /// 2026-10-20: "half as fast").
    /// </summary>
    public static EnemyPickaxe Throw(GameObject prefab, Vector3 from, Vector3 target, Transform knight,
                                     object owner, float pace)
    {
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, from, Quaternion.identity);
        EnemyPickaxe axe = go.GetComponent<EnemyPickaxe>();
        if (axe == null) axe = go.AddComponent<EnemyPickaxe>();

        axe._knight = knight;
        axe._owner = owner;
        axe._pace = Mathf.Clamp(pace, 0.05f, 4f);
        axe.Launch(target);
        return axe;
    }

    /// <summary>The flight time a pickaxe prefab will throw with, or 0 if it is not one.</summary>
    public static float FlightTimeOf(GameObject prefab)
    {
        EnemyPickaxe axe = prefab != null ? prefab.GetComponent<EnemyPickaxe>() : null;
        return axe != null ? axe.FlightTime : 0f;
    }

    // ---- IIncoming ----

    public Transform IncomingTarget => _knight;

    public object IncomingOwner => _owner;

    // The arc is solved for a fixed flight time, so time left is exactly that
    // clock run down — stretched by a chill the same way Update stretches it.
    public float SecondsToArrival => (_spent || !enabled || _knight == null)
        ? float.PositiveInfinity
        : Mathf.Max(0f, _flightLeft) / Mathf.Max(0.05f, ChillScale * _pace);

    private void OnEnable() { IncomingLedger.Add(this); }

    // Also how a reflected or batted axe leaves the ledger: switching this
    // component off is what turns it into the knight's
    private void OnDisable() { IncomingLedger.Remove(this); }

    private void Launch(Vector3 target)
    {
        float t = Mathf.Max(0.05f, flightTime);
        _flightLeft = t;
        Vector2 delta = target - transform.position;

        // Constant acceleration, solved backwards: horizontal is just distance over
        // time, and vertical adds back the half-g-t-squared the fall will eat. The
        // arc's height is therefore whatever `gravity` implies, and the pickaxe is
        // guaranteed to be AT the knight when the clock runs out.
        _velocity = new Vector2(delta.x / t, delta.y / t + 0.5f * gravity * t);

        // Tumbles the way it travels, so a throw to the left spins anticlockwise
        _spin = _velocity.x >= 0f ? -spinSpeed : spinSpeed;

        Destroy(gameObject, maxLifetime);
    }

    private void Awake()
    {
        _body = GetComponent<CircleCollider2D>();
        _lastPosition = transform.position;
    }

    private void Start()
    {
        // A wave is not over while its ordnance is still in the air — the same
        // bookkeeping ProjectileSettings and EnemyFireball do
        BaseWave.RegisterProjectile(gameObject);
    }

    private void OnDestroy()
    {
        BaseWave.UnregisterProjectile(gameObject);
    }


    // Glacial Ward II: enemy ammunition crossing a knight's ring of cold loses
    // half its speed. See IChillable.
    private float _chillMultiplier = 1f;
    private float _chillUntil = -1f;

    public void ApplyChill(float speedMultiplier, float seconds)
    {
        if (seconds <= 0f) return;
        _chillMultiplier = Mathf.Clamp(Mathf.Min(_chillMultiplier, speedMultiplier), 0.05f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + seconds);
    }

    private float ChillScale { get { return Time.time < _chillUntil ? _chillMultiplier : 1f; } }

    private void Update()
    {
        // The launch velocity was SOLVED for a fixed flight time against a fixed
        // gravity, so slowing this by scaling the velocity alone would flatten the
        // arc into something that lands somewhere else. Scaling the clock instead
        // keeps the throw exactly the throw it was and simply stretches it out.
        float dt = Time.deltaTime * ChillScale * _pace;
        _flightLeft -= dt;
        _lastPosition = transform.position;

        _velocity.y -= gravity * dt;
        transform.position += (Vector3)(_velocity * dt);
        transform.Rotate(0f, 0f, _spin * dt);

        // Well below the knights and still going: nothing left to hit
        if (transform.position.y < -9f) Destroy(gameObject);
    }

    // Every tool the knights hold stops it, which is what "one hit point" means
    // here. Mirrors EnemyFireball.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Unity still sends trigger messages to a DISABLED behaviour, and switching
        // this one off is how a pickaxe becomes the knight's (see the reflect and the
        // sword below). Without this a returned axe could still be knocked down by the
        // other knight's arrow or hurt a knight it passed.
        if (_spent || !enabled) return;

        // The arrow flies on through (owner, 2026-10-15): it is spent on bodies, not
        // on the enemy's ammunition
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Spend();
            return;
        }

        // The sword object is untagged by design (see SwordSwing); the detector it
        // grows at swing time is the reliable handle. Any knight's blade bats it back
        // down the line the swing faces (owner, 2026-10-15).
        GuardianReflect.Turn bat = GuardianReflect.TryBat(other, transform.position);
        if (bat.Happened)
        {
            enabled = false;
            ReflectedShot.Attach(gameObject, bat, _velocity.magnitude, knightDamage);
            return;
        }

        if (other.CompareTag("Shield"))
        {
            Block(other);
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            // The guard wins. The axe is checked against the knights once a
            // physics step, and in one step it can reach the guard AND the body
            // behind it — Unity hands over the two touches in no fixed order, so
            // the body's could come first and an axe dead centre on the shield
            // still hurt (owner, 2026-10-20). A fast axe can also cross the thin
            // guard between two steps and be found inside the body. Either way the
            // knight blocked it, so it is a block.
            Collider2D guard = GuardInTheWay();
            if (guard != null)
            {
                Block(guard);
                return;
            }

            other.GetComponent<PlayerHealth>()?.TakeDamage(knightDamage, sourceName);
            Spend();
        }
    }

    // A shield the axe is touching now, or passed through since the last frame
    private Collider2D GuardInTheWay()
    {
        float radius = _body != null
            ? _body.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y))
            : 0.25f;
        Vector2 now = transform.position;

        var filter = new ContactFilter2D();
        filter.useTriggers = true;

        TouchHits.Clear();
        Physics2D.OverlapCircle(now, radius, filter, TouchHits);
        for (int i = 0; i < TouchHits.Count; i++)
        {
            if (TouchHits[i] != null && TouchHits[i].CompareTag("Shield")) return TouchHits[i];
        }

        Vector2 travel = now - _lastPosition;
        float distance = travel.magnitude;
        if (distance < 0.0001f) return null;

        SweepHits.Clear();
        Physics2D.CircleCast(_lastPosition, radius, travel / distance, filter, SweepHits, distance);
        for (int i = 0; i < SweepHits.Count; i++)
        {
            Collider2D hit = SweepHits[i].collider;
            if (hit != null && hit.CompareTag("Shield")) return hit;
        }
        return null;
    }

    // Stopped on a guard: special charge for the knight, and the axe is either
    // eaten or, with Reflector, sent back
    private void Block(Collider2D shield)
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.projectileShield);
        shield.GetComponentInParent<PlayerSpecial>()?.updateSpecial(1);

        // Reflector (Guardian): the guard sends it back rather than eating it.
        // After the special charge, deliberately — a block is a block whether or
        // not the axe came back, and routing the reward through the reflect would
        // quietly delete a charge source on every rebound.
        //
        // The axe tumbles on the way in, so its rotation is not its heading: the
        // solved launch velocity is.
        GuardianReflect.Turn turn = GuardianReflect.TryTurn(shield, _velocity);
        if (turn.Happened)
        {
            // Off first. Everything that made this a hazard — the arc, the tumble,
            // the knight-damage handler — lives on this component, and switching
            // it off is what makes the axe the knight's rather than the gnome's.
            enabled = false;
            ReflectedShot.Attach(gameObject, turn, _velocity.magnitude, knightDamage);
            return;
        }

        Spend();
    }

    private void Spend()
    {
        if (_spent) return;
        _spent = true;
        Destroy(gameObject);
    }
}

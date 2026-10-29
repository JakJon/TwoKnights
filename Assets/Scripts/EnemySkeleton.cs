using UnityEngine;

// The castle skeleton (owner's design, 2026-10-27).
//
// He walks in with a shield that he keeps turned on whichever knight is nearest,
// and that shield stops everything shot or swung into it. So he is not answered
// head-on: the knight he is going for sees mostly shield, and the work falls to
// the partner's crossfire or a shot sent round through the mirrors into his side
// or back.
//
// What he does, in order:
//
//   1. Entering — walks to the entry point the wave gave him (just inside the
//      mirror column, through a gap between panes). Skipped if he was given none.
//   2. Picks his knight once, the moment he is on the field: the nearer one. He
//      never changes it. The shield keeps tracking the nearest knight on its own,
//      which is not always the same one.
//   3. Approaching — walks straight at that knight until he is CircleRadius away.
//   4. Circling — goes CircleDegrees round the knight at that distance, whichever
//      way ends further from the OTHER knight. On a tie (he came in level with
//      the knights) he goes round toward the half of the screen he came in from.
//   5. Rattling — stops for RattlePause and rattles his bones. The warning.
//   6. Charging — straight at the knight at ChargeMultiplier times his walk.
//      He lands like any other body: the knight's guard pays shield damage, the
//      knight pays full damage, and either way he is gone.
//
// A Bulwark guard throws him back instead; he walks in again and goes round again.
//
// Pivot is at his FEET (Aseprite default), so every distance here is measured
// from the middle of his collider, and a knight is measured from the middle of
// its guard's orbit — half a unit above its feet (see ShieldOrbit).
public class EnemySkeleton : EnemyBase
{
    private enum Phase { Entering, Approaching, Circling, Rattling, Charging }

    [Header("Movement")]
    [Tooltip("World units per second. 0.5 is a small slime's pace.")]
    [SerializeField] private float walkSpeed = 0.5f;
    [Tooltip("Charge speed as a multiple of the walk")]
    [SerializeField] private float chargeMultiplier = 2f;
    [Tooltip("How far from the knight's middle he stops walking in and starts going round")]
    [SerializeField] private float circleRadius = 3f;
    [Tooltip("How far round the knight he goes before he charges")]
    [SerializeField] private float circleDegrees = 130f;
    [Tooltip("Seconds he stands and rattles before the charge")]
    [SerializeField] private float rattlePause = 0.2f;
    [SerializeField] private int damage = 20;

    [Header("Shield")]
    [Tooltip("The shield child. Its art is a plank drawn upright, so it faces +x unturned.")]
    [SerializeField] private SpriteRenderer shield;
    [Tooltip("From the middle of his body to the middle of the shield. 0.6 leaves about 0.2 clear of his side.")]
    [SerializeField] private float shieldDistance = 0.6f;
    [Tooltip("Half the plank's length at scale 1. The art is 32 px tall at 32 px a unit. The shield child's scale multiplies it.")]
    [SerializeField] private float shieldHalfLength = 0.5f;

    [Header("Sound")]
    [SerializeField] private SoundEffect rattle;

    // Half a unit above a knight's feet is the middle of its guard's orbit
    private const float KnightMiddleLift = 0.5f;

    // A blocked shot jolts the shield back this far for this long
    private const float BlockJolt = 0.06f;
    private const float BlockJoltSeconds = 0.1f;
    // The guard clang is not replayed faster than this (the Sight beam asks every frame)
    private const float BlockSoundGap = 0.15f;

    private Phase _phase = Phase.Approaching;
    private Vector2 _entryPoint;
    private bool _hasEntry;
    private float _enteredFromY;
    private Transform _target;
    private int _circleSign;
    private float _circleLastAngle;
    private float _circleGone;
    private float _rattleUntil;

    private Collider2D _body;
    private Vector2 _shieldFacing = Vector2.right;
    private float _joltUntil = -1f;
    private float _lastBlockSound = -1f;

    public float WalkSpeed => walkSpeed;
    public float ChargeMultiplier => chargeMultiplier;
    public float CircleRadius => circleRadius;
    public float CircleDegrees => circleDegrees;
    public float RattlePause => rattlePause;

    // How close a charging skeleton's middle gets to a knight's middle before it
    // touches the guard: the guard's one-unit orbit plus about half his width
    private const float StrikeReach = 1.35f;

    /// <summary>How far his middle sits above his feet (his pivot). Read off the
    /// prefab by waves that place him by his middle.</summary>
    public float BodyLift
    {
        get
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            return box != null ? box.offset.y * transform.lossyScale.y : 0.45f;
        }
    }

    /// <summary>
    /// Seconds from being put down (his middle at <paramref name="spawn"/>) to
    /// touching the guard of the knight whose middle is <paramref name="knight"/>,
    /// by his own rules: walk to the entry point, walk to the circle, go round,
    /// rattle, charge. Waves time their groups off it, so a retune of the prefab
    /// moves their timers with it. Ignores stagger, chill and shoves.
    /// </summary>
    public float EstimateRunSeconds(Vector2 spawn, Vector2 entry, Vector2 knight)
    {
        float walk = (entry - spawn).magnitude
                     + Mathf.Max(0f, (knight - entry).magnitude - circleRadius)
                     + circleRadius * circleDegrees * Mathf.Deg2Rad;
        float charge = Mathf.Max(0f, circleRadius - StrikeReach);

        return walk / Mathf.Max(0.01f, walkSpeed)
               + rattlePause
               + charge / Mathf.Max(0.01f, walkSpeed * chargeMultiplier);
    }

    /// <summary>Called by the wave before Start: the point (his feet) he walks to
    /// before he counts as on the field.</summary>
    /// <summary>Which knight he takes when he is exactly as near to both — one
    /// that walks in down the middle. Set by the wave, so it is never a coin toss.</summary>
    public void SetTieBreak(bool right) => _tieGoesRight = right;
    private bool _tieGoesRight;

    public void SetEntry(Vector2 feet)
    {
        _entryPoint = feet;
        _hasEntry = true;
    }

    protected override void Awake()
    {
        base.Awake();
        _body = GetComponent<Collider2D>();
    }

    private void Start()
    {
        attributes = EnemyType.Ground;
        playerDamage = damage;

        _enteredFromY = BodyMiddle.y;
        _phase = _hasEntry ? Phase.Entering : Phase.Approaching;
        if (!_hasEntry) PickTarget();

        PlayAnimatorState(null, "Walking");
        UpdateShield();
    }

    private void Update()
    {
        if (IsStaggered) return;

        if (_target == null && _phase != Phase.Entering) PickTarget();

        switch (_phase)
        {
            case Phase.Entering:    Enter();    break;
            case Phase.Approaching: Approach(); break;
            case Phase.Circling:    Circle();   break;
            case Phase.Rattling:    Rattle();   break;
            case Phase.Charging:    Charge();   break;
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        UpdateShield();
    }

    // ---- Phases ----

    private void Enter()
    {
        Vector3 before = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, _entryPoint, walkSpeed * Time.deltaTime);
        UpdateSpriteDirection(before, transform.position);

        if (((Vector2)transform.position - _entryPoint).sqrMagnitude > 0.0001f) return;

        _enteredFromY = BodyMiddle.y;
        PickTarget();
        _phase = Phase.Approaching;
    }

    private void Approach()
    {
        if (_target == null) return;

        Vector2 middle = KnightMiddle(_target);
        if ((BodyMiddle - middle).magnitude <= circleRadius)
        {
            BeginCircle(middle);
            return;
        }

        StepBodyToward(middle, walkSpeed);
    }

    private void BeginCircle(Vector2 knight)
    {
        Vector2 from = BodyMiddle - knight;
        float start = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;

        // Judged on the whole way round, not where it ends: at 180 degrees both
        // directions end on the same spot, and only the path between tells them apart
        Transform other = OtherKnight(_target);
        if (other != null)
        {
            Vector2 otherMiddle = KnightMiddle(other);
            float up = ClosestApproach(knight, start, 1, otherMiddle);
            float down = ClosestApproach(knight, start, -1, otherMiddle);

            if (Mathf.Abs(up - down) > 0.05f) _circleSign = up > down ? 1 : -1;
            else _circleSign = TowardEntryHalf(knight, start);
        }
        else
        {
            _circleSign = TowardEntryHalf(knight, start);
        }

        _circleLastAngle = start;
        _circleGone = 0f;
        _phase = Phase.Circling;
    }

    // How near the circle comes to a point if he goes round in this direction
    private float ClosestApproach(Vector2 knight, float start, int sign, Vector2 point)
    {
        const float Step = 5f;
        float closest = float.MaxValue;
        for (float t = 0f; t <= circleDegrees + 0.01f; t += Step)
        {
            closest = Mathf.Min(closest, (knight + Around(start + sign * t) - point).magnitude);
        }
        return closest;
    }

    // The tie-break: go round toward the half of the screen he came in from,
    // judged by where each direction is halfway round
    private int TowardEntryHalf(Vector2 knight, float start)
    {
        bool cameFromTop = _enteredFromY >= 0f;
        float upHalfway = (knight + Around(start + circleDegrees * 0.5f)).y;
        float downHalfway = (knight + Around(start - circleDegrees * 0.5f)).y;
        return cameFromTop == (upHalfway >= downHalfway) ? 1 : -1;
    }

    private void Circle()
    {
        if (_target == null) return;

        Vector2 knight = KnightMiddle(_target);
        Vector2 from = BodyMiddle - knight;
        float now = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;

        // Measured from where he actually is, so chill, a stagger or a shove all
        // cost him ground on the circle instead of being walked through. Added up
        // a frame at a time: a single difference from the start angle wraps at 180.
        _circleGone += _circleSign * Mathf.DeltaAngle(_circleLastAngle, now);
        _circleLastAngle = now;
        float gone = _circleGone;
        if (gone >= circleDegrees)
        {
            BeginRattle();
            return;
        }

        // A point just ahead on the circle. Walking to it at his own pace keeps him
        // on the circle, and brings him back to it if something knocked him off.
        float stepDegrees = walkSpeed * Time.deltaTime / circleRadius * Mathf.Rad2Deg;
        float remaining = circleDegrees - gone;
        float ahead = now + _circleSign * Mathf.Min(stepDegrees * 2f, remaining);
        StepBodyToward(knight + Around(ahead), walkSpeed);
    }

    private void BeginRattle()
    {
        _rattleUntil = Time.time + rattlePause;
        _phase = Phase.Rattling;
        if (AudioManager.Instance != null && rattle != null) AudioManager.Instance.PlaySFX(rattle);
    }

    private void Rattle()
    {
        if (Time.time >= _rattleUntil) _phase = Phase.Charging;
    }

    private void Charge()
    {
        if (_target == null) return;
        StepBodyToward(KnightMiddle(_target), walkSpeed * chargeMultiplier);
    }

    // ---- Collision ----

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // Bulwark throws him off the guard rather than taking him. He walks back
        // in and goes round again from wherever he lands.
        bool thrown = !isDead && other != null && other.CompareTag("Shield") && HasBulwark(other);

        base.OnTriggerEnter2D(other);

        if (thrown && !isDead) _phase = Phase.Approaching;
    }

    private static bool HasBulwark(Collider2D shieldCollider)
    {
        GuardianBoost guardian = shieldCollider.GetComponentInParent<GuardianBoost>();
        return guardian != null && guardian.HasBulwark;
    }

    // ---- The shield ----

    private void UpdateShield()
    {
        Transform nearest = NearestKnight();
        if (nearest != null)
        {
            Vector2 toward = KnightMiddle(nearest) - BodyMiddle;
            if (toward.sqrMagnitude > 1e-6f) _shieldFacing = toward.normalized;
        }

        if (shield == null) return;

        float distance = shieldDistance;
        if (Time.time < _joltUntil) distance -= BlockJolt;

        Transform t = shield.transform;
        t.position = (Vector3)(BodyMiddle + _shieldFacing * distance);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_shieldFacing.y, _shieldFacing.x) * Mathf.Rad2Deg);

        // Behind him when it points up the screen, in front of him otherwise
        if (spriteRenderer != null)
        {
            shield.sortingLayerID = spriteRenderer.sortingLayerID;
            shield.sortingOrder = spriteRenderer.sortingOrder + (_shieldFacing.y > 0.35f ? -1 : 1);
        }
    }

    // The plank as a line across the facing. A blow is stopped if the line it came
    // in on crosses it — what the player sees the shield cover is what it covers.
    protected override bool TryBlock(Vector2 from, Vector2 to)
    {
        if (isDead) return false;

        Vector2 middle = BodyMiddle + _shieldFacing * shieldDistance;
        // Scaled with the shield, so resizing it on the prefab resizes what it covers
        float halfLength = shieldHalfLength * (shield != null ? Mathf.Abs(shield.transform.lossyScale.y) : 1f);
        Vector2 across = new Vector2(-_shieldFacing.y, _shieldFacing.x) * halfLength;
        if (!SegmentsCross(from, to, middle - across, middle + across)) return false;

        _joltUntil = Time.time + BlockJoltSeconds;
        if (Time.time - _lastBlockSound >= BlockSoundGap && AudioManager.Instance != null)
        {
            _lastBlockSound = Time.time;
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
        }
        return true;
    }

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);

        float d1 = Cross(c, d, a);
        float d2 = Cross(c, d, b);
        float d3 = Cross(a, b, c);
        float d4 = Cross(a, b, d);
        return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
    }

    // ---- Geometry ----

    // From the transform and the collider's offset, NOT bounds.center. A collider's
    // bounds only catch up with a moved transform at the next physics step, and
    // the wave moves him the frame he is made — Instantiate puts him at the world
    // origin first — so bounds put his middle (and the shield) in the middle of the
    // arena, next to a knight, for his first frame.
    private Vector2 BodyMiddle => _body != null ? (Vector2)transform.TransformPoint(_body.offset) : (Vector2)transform.position;

    protected override Vector2 GuardCentre => BodyMiddle;

    private Vector2 Around(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * circleRadius;
    }

    // Moves the feet so the MIDDLE heads for the point
    private void StepBodyToward(Vector2 point, float speed)
    {
        Vector2 middle = BodyMiddle;
        Vector2 next = Vector2.MoveTowards(middle, point, speed * Time.deltaTime);
        Vector3 step = (Vector3)(next - middle);
        transform.position += step;
        if (step.sqrMagnitude > 1e-8f) UpdateSpriteDirection(step);
    }

    private void PickTarget()
    {
        _target = NearestKnight();
    }

    private Transform NearestKnight()
    {
        Transform left = Knight("PlayerLeft");
        Transform right = Knight("PlayerRight");
        if (left == null) return right;
        if (right == null) return left;

        Vector2 me = BodyMiddle;
        float toLeft = (KnightMiddle(left) - me).sqrMagnitude;
        float toRight = (KnightMiddle(right) - me).sqrMagnitude;

        // Dead level between them (a skeleton walking in down the middle): the
        // wave says which way he leans
        if (Mathf.Abs(toLeft - toRight) < 0.01f) return _tieGoesRight ? right : left;

        return toLeft < toRight ? left : right;
    }

    private Transform OtherKnight(Transform knight)
    {
        Transform left = Knight("PlayerLeft");
        return knight == left ? Knight("PlayerRight") : left;
    }

    private static Transform _left, _right;

    private static Transform Knight(string tag)
    {
        Transform cached = tag == "PlayerLeft" ? _left : _right;
        if (cached != null) return cached;

        GameObject found = GameObject.FindWithTag(tag);
        cached = found != null ? found.transform : null;
        if (tag == "PlayerLeft") _left = cached; else _right = cached;
        return cached;
    }

    public static Vector2 KnightMiddle(Transform knight) => (Vector2)knight.position + Vector2.up * KnightMiddleLift;
}

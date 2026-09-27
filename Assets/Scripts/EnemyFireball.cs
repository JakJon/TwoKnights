using UnityEngine;

// The giant slimes' ammunition. Three flight modes share one prefab because they
// share one rule: EVERY tool the knights hold can pop a fireball — arrow, sword,
// and shield alike. That's deliberate and it differs from ProjectileSettings (the
// rats' arrows, which only the shield stops): the Ward variant's orbit swings out
// past the knight, so whatever the knight is holding when it arrives has to be
// able to clear it.
//
// Bolted-on configuration follows PoisonProjectile/FireballProjectile: the prefab
// carries sprite + collider identity, Launch/Orbit hand it the per-shot state.
public class EnemyFireball : MonoBehaviour, IChillable, IIncoming
{
    public enum Mode
    {
        Straight,    // fired at where the knight stands, no course correction
        ArcThenHome, // one lateral sweep, then it turns and chases the knight
        Ward         // one of a ring orbiting its slime; comes back a few seconds after a pop
    }

    // Visual only — an enemy fireball hurts what it touched and nothing else, so
    // unlike a keg or a player fireball this radius is not a damage circle. The
    // ring is still drawn on it, because a pop that skipped the ring would read
    // as a different KIND of event from every other explosion on screen; what it
    // promises here is the size of the pop, not a blast the knights can be caught
    // by. Keep it small for exactly that reason.
    private const float PopRadius = 0.6f;

    private Mode _mode;
    private Transform _target;
    private float _speed;
    private int _damage;
    private string _sourceName = "a Fireball";

    // ArcThenHome
    private float _heading;          // degrees, current travel direction
    private float _arcRemaining;     // seconds left in the opening sweep
    private float _arcDegreesPerSecond;
    private float _homingTurnRate;   // degrees/second once the sweep is done

    // Ward
    private float _orbitRadius;      // the radius it breathes around
    private float _orbitRadiusSwing; // peak-to-peak, so +-half of this
    private float _orbitPulseSeconds;
    private float _orbitDegreesPerSecond;
    private float _orbitAngle;
    private float _pulseTime;
    private EnemyGiantSlime _owner;

    private bool _popped;

    public Mode CurrentMode => _mode;

    // Where a ward is on its orbit and in its breath, so a fireball coming back
    // can take its place in the ring alongside the ones still up
    public float OrbitAngle => _orbitAngle;
    public float PulseTime => _pulseTime;

    // A shot aimed at a knight. arcSign mirrors the sweep between the twins (+1 /
    // -1) so the pair's volleys are horizontal mirror images of each other.
    public static EnemyFireball Launch(GameObject prefab, Vector2 position, Transform target,
        Mode mode, float speed, int damage, float arcDegrees, float arcSeconds,
        float homingTurnRate, float arcSign, float lifetime, float scale)
    {
        if (prefab == null || target == null) return null;

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;

        EnemyFireball ball = go.GetComponent<EnemyFireball>();
        if (ball == null) ball = go.AddComponent<EnemyFireball>();

        ball._mode = mode;
        ball._target = target;
        ball._speed = speed;
        ball._damage = damage;

        Vector2 toTarget = (Vector2)target.position - position;
        float aim = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

        if (mode == Mode.ArcThenHome)
        {
            // Start the sweep half its width to the OUTSIDE of the aim line so the
            // arc scythes through it at the midpoint — a shot that merely veered
            // off and came back read as a bug, not an arc.
            ball._heading = aim - arcSign * arcDegrees * 0.5f;
            ball._arcRemaining = arcSeconds;
            ball._arcDegreesPerSecond = arcSeconds > 0f ? (arcSign * arcDegrees / arcSeconds) : 0f;
            ball._homingTurnRate = homingTurnRate;
        }
        else
        {
            ball._heading = aim;
        }

        ball.FaceHeading();
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.fireballLaunch);
        if (lifetime > 0f) Destroy(go, lifetime);
        return ball;
    }

    // One fireball of the orbiting ward. It never expires on its own — only a
    // knight's tool clears it, and its owner puts it back in the ring a few
    // seconds later.
    //
    // radiusSwing makes the orbit BREATHE in and out. pulseSeconds is deliberately not
    // a multiple of the orbit period: if the two were locked the ward would always reach
    // its far extreme at the same bearing, whereas beating them against each other walks
    // that extreme around the circle, so it only *sometimes* lunges at the knight — and
    // when it does, the shield is the only answer. Both are pure functions of time, so
    // the pattern stays deterministic (no dice in wave content).
    //
    // pulseTime starts the breath part-way through, so a fireball rejoining the ring
    // breathes in step with the others instead of from the start of its own breath.
    public static EnemyFireball Orbit(GameObject prefab, EnemyGiantSlime owner, float radius,
        float radiusSwing, float pulseSeconds, float degreesPerSecond, float startAngle,
        int damage, float scale, float pulseTime = 0f, bool playSound = true)
    {
        if (prefab == null || owner == null) return null;

        GameObject go = Instantiate(prefab, owner.BodyCenter, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;

        EnemyFireball ball = go.GetComponent<EnemyFireball>();
        if (ball == null) ball = go.AddComponent<EnemyFireball>();

        ball._mode = Mode.Ward;
        ball._owner = owner;
        ball._orbitRadius = radius;
        ball._orbitRadiusSwing = radiusSwing;
        ball._orbitPulseSeconds = pulseSeconds;
        ball._orbitDegreesPerSecond = degreesPerSecond;
        ball._orbitAngle = startAngle;
        ball._pulseTime = pulseTime;
        ball._damage = damage;
        ball._sourceName = "a Slime's Ward";
        ball.PlaceOnOrbit();

        if (playSound) AudioManager.Instance?.PlaySFX(AudioManager.Instance.fireballLaunch);
        return ball;
    }

    // In the IncomingLedger while it flies (see ProjectileMovement). Only a
    // STRAIGHT shot has an arrival time worth stating: an arcing one curves and
    // then chases, and a ward orbits rather than arrives.
    private void OnEnable() { IncomingLedger.Add(this); }
    private void OnDisable() { IncomingLedger.Remove(this); }

    public Transform IncomingTarget => _mode == Mode.Straight ? _target : null;

    public float SecondsToArrival
    {
        get
        {
            if (_mode != Mode.Straight || _target == null) return float.PositiveInfinity;
            return IncomingLedger.StraightEta(transform.position, HeadingVector(), _target.position, _speed * ChillScale);
        }
    }

    private void Start()
    {
        // Same wave bookkeeping as ProjectileSettings: a wave isn't over while its
        // shots are still in the air
        BaseWave.RegisterProjectile(gameObject);
    }

    private void OnDestroy()
    {
        BaseWave.UnregisterProjectile(gameObject);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_mode == Mode.Ward)
        {
            // The owner dying takes the ward with it (EnemyGiantSlime.OnDeath also
            // clears it; this covers despawn and scene unload)
            if (_owner == null)
            {
                Destroy(gameObject);
                return;
            }
            // A sleeping slime's guard sleeps with it: the orbit and the breath
            // both stop where they are rather than sweeping on around a body that
            // is visibly frozen. Position is still written every frame so the ward
            // stays welded to its owner however the owner is moved.
            if (!_owner.IsAsleep)
            {
                _orbitAngle += _orbitDegreesPerSecond * dt;
                _pulseTime += dt;
            }

            PlaceOnOrbit();
            return;
        }

        if (_mode == Mode.ArcThenHome)
        {
            if (_arcRemaining > 0f)
            {
                float step = Mathf.Min(_arcRemaining, dt);
                _heading += _arcDegreesPerSecond * step;
                _arcRemaining -= dt;
            }
            else if (_target != null)
            {
                // The single arc is spent; from here it chases at a capped turn
                // rate so it stays dodgeable and, above all, still shootable
                Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
                float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                _heading = Mathf.MoveTowardsAngle(_heading, desired, _homingTurnRate * dt);
            }
            FaceHeading();
        }

        // Only what is FLYING at a knight is slowed. The ward-orbit mode above
        // returns before it reaches this line, so a boss's guard keeps its pace -
        // chilling that would be chilling the boss's aura, not its ammunition.
        transform.position += (Vector3)(HeadingVector() * _speed * ChillScale * dt);
    }

    // Puts a ward where its angle and breath say it is, around wherever its slime
    // is standing. Breathe: the far extreme reaches past the knight (block it), the
    // near one tucks the ward back against the slime's flank.
    private void PlaceOnOrbit()
    {
        float radius = _orbitRadius;
        if (_orbitRadiusSwing > 0f && _orbitPulseSeconds > 0f)
        {
            radius += Mathf.Sin(_pulseTime / _orbitPulseSeconds * 2f * Mathf.PI)
                * (_orbitRadiusSwing * 0.5f);
        }

        float rad = _orbitAngle * Mathf.Deg2Rad;
        transform.position = _owner.BodyCenter
            + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        transform.rotation = Quaternion.AngleAxis(_orbitAngle, Vector3.forward);
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


    private Vector2 HeadingVector()
    {
        float rad = _heading * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void FaceHeading()
    {
        transform.rotation = Quaternion.AngleAxis(_heading, Vector3.forward);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_popped) return;

        // Arrow (and shuriken/shadow arrow — they all carry the owner tag)
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
            Pop();
            return;
        }

        // Sword. The sword object is untagged by design (see SwordSwing) — the
        // detector component it grows at swing time is the reliable handle.
        if (other.GetComponent<SwordDamageDetector>() != null)
        {
            Pop();
            return;
        }

        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.projectileShield);
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            playerSpecial?.updateSpecial(1);

            // Reflector (Guardian): the guard turns the slime's own fire around.
            // A WARD is exempt and always pops: it is part of the ring orbiting the
            // boss rather than a shot at anybody, and it has no flight speed of its
            // own to turn around — a reflected ward would just hang in the air.
            if (_mode != Mode.Ward)
            {
                GuardianReflect.Turn turn = GuardianReflect.TryTurn(other, HeadingVector());
                if (turn.Happened)
                {
                    // Off first: the arc, the homing and the knight-damage handler all
                    // live on this component, and switching it off is what makes the
                    // fireball the knight's rather than the slime's.
                    enabled = false;
                    ReflectedShot.Attach(gameObject, turn, _speed, _damage);
                    return;
                }
            }

            Pop();
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            playerHealth?.TakeDamage(_damage, _sourceName);
            Pop();
        }
    }

    private void Pop()
    {
        if (_popped) return;
        _popped = true;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.fireballExplode);
        FireFx.Burst(transform.position, PopRadius, markRadius: true);

        // Tell the slime this place in its ring is empty before we vanish
        if (_mode == Mode.Ward && _owner != null)
        {
            _owner.OnWardDestroyed(this);
        }

        Destroy(gameObject);
    }
}

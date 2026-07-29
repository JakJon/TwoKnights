using UnityEngine;
using System.Collections;

// Half of the Camp Fields' true boss: a giant red slime that walks a straight line
// at one knight and never stops. Unlike every other boss in the game it CAN reach a
// knight, and touching one is instant death — the whole fight is a race against that
// approach, so nothing is allowed to interrupt the walk (not even stagger).
//
// The pair is driven by GiantSlimeDuel, not by each slime: volleys have to leave both
// mouths on the same frame, and the shared health bar is the sum of the two. A slime
// on its own only owns its walk, its ward, and its own death.
//
// Blockability is preserved the way the Rat King preserves it: each twin only ever
// shoots the knight on ITS side, so simultaneous volleys never converge on one shield.
public class EnemyGiantSlime : EnemyBase
{
    public enum Side { Left, Right }

    // The two volley patterns, alternated by GiantSlimeDuel
    public enum VolleyPattern { MirroredArc, Straight }

    [System.Serializable]
    public class Config
    {
        [Header("Body")]
        [Tooltip("Health per slime. Default is 2x the gate Rat King's 750.")]
        public float health = 2500f;
        [Tooltip("Double the size-3 slime's scale of 3.")]
        public float scale = 6f;
        [Tooltip("Units/second toward the knight. THIS IS THE FIGHT TIMER — the knights " +
                 "must kill both slimes before either one arrives.")]
        public float approachSpeed = 0.580f;
        [Tooltip("Damage dealt to a knight the slime physically reaches. Lethal by design.")]
        public int contactDamage = 9999;

        [Header("Phase switch")]
        [Tooltip("Fraction of the opening gap left when the slime stops shooting at the " +
                 "knight and raises its orbiting ward instead. 0.5 = halfway there.")]
        public float wardPhaseDistanceFraction = 0.5f;

        [Header("Fireballs (approach phase)")]
        public float volleyInterval = 3.5f;
        public float telegraphPause = 0.8f;
        public float fireballSpeed = 2.4f;
        public int fireballDamage = 15;
        [Tooltip("Seconds a loose fireball lives before expiring off-screen.")]
        public float fireballLifetime = 12f;
        [Tooltip("Fireball.png is 0.32 units at scale 1, so 2.2 gives a ~0.7-unit ball.")]
        public float fireballScale = 2.2f;
        [Header("— mirrored arc shot")]
        [Tooltip("Total sweep of the opening arc, in degrees.")]
        public float arcDegrees = 160f;
        [Tooltip("How long the single arc lasts before the fireball starts chasing.")]
        public float arcSeconds = 1.3f;
        [Tooltip("Turn rate while homing. Lower = easier to outrun and to shoot down.")]
        public float homingTurnRate = 80f;
        [Header("— straight shot")]
        public int straightShotsPerVolley = 2;
        public float straightShotStagger = 0.3f;

        [Header("Ward (second phase)")]
        [Tooltip("Orbit radius the ward fireball breathes around, in units from the " +
                 "slime's body centre.")]
        public float wardRadius = 3.6f;
        [Tooltip("Peak-to-peak swing of that radius. At 3 the ward ranges 2.1 -> 5.1 " +
                 "units out: the far extreme reaches past the knight so the shield has to " +
                 "answer it, the near one tucks back against the slime's flank.")]
        public float wardRadiusSwing = 3f;
        [Tooltip("Seconds for one full out-and-back breath. Kept off the orbit period " +
                 "(360/wardDegreesPerSecond) on purpose so the far extreme drifts around " +
                 "the circle instead of always lunging the same way.")]
        public float wardPulseSeconds = 4f;
        public float wardDegreesPerSecond = 110f;
        [Tooltip("Seconds the slime is exposed after its ward is popped.")]
        public float wardRespawnSeconds = 3f;
        [Tooltip("Bigger than a loose fireball — it has to be an obvious target.")]
        public float wardScale = 3.1f;

        [Header("Rewards")]
        public int goldReward = 100;
        public int specialOnDeathReward = 60;
    }

    private Config _config;
    private GameObject _fireballPrefab;
    private Side _side;
    private Transform _knight;
    private float _openingDistance;
    private EnemyFireball _ward;
    private Coroutine _wardRespawn;
    private bool _wardPhaseEntered;
    private Collider2D _collider;

    public Side WhichSide => _side;
    public bool IsWarded => _ward != null;
    public bool InWardPhase => _wardPhaseEntered;

    // The slime's pivot sits at the BOTTOM of its body (the authored collider spans
    // y 0 -> 0.93 in local space), so transform.position is its feet, ~2.8 units below
    // the middle of a scale-6 body. Fireballs leave from here and the ward orbits it,
    // or shots would hatch out of its underside and the ward would circle its feet.
    // Read off the collider, not the SpriteRenderer: the walk cycle squashes the
    // sprite every frame and the ward's orbit centre would jitter with it.
    public Vector2 BodyCenter =>
        _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;

    // +1 / -1: mirrors the arc sweep and the ward's spin between the twins
    private float MirrorSign => _side == Side.Left ? 1f : -1f;

    public void Initialize(Config config, GameObject fireballPrefab, Side side, Transform knight)
    {
        _config = config;
        _fireballPrefab = fireballPrefab;
        _side = side;
        _knight = knight;

        health = config.health;
        goldOnDeath = config.goldReward;
        specialOnDeath = config.specialOnDeathReward;
        playerDamage = config.contactDamage;
        transform.localScale = Vector3.one * config.scale;

        _openingDistance = knight != null
            ? Vector2.Distance(transform.position, knight.position)
            : 0f;
    }

    protected override void Awake()
    {
        base.Awake();
        _collider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        // Ground, but NOT Splitting: a boss that shed a litter of size-5 slimes on
        // death would bury the arena and the victory check with it
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        if (AudioManager.Instance != null)
        {
            hurtSound = AudioManager.Instance.giantSlimeHurt;
            deathSound = AudioManager.Instance.giantSlimeDeath;
        }
    }

    private void Update()
    {
        if (isDead || _config == null || _knight == null) return;

        // Deliberately NOT gated on IsStaggered (which is what EnemySlime does).
        // The walk is the fight's clock; letting arrow hits stutter it would let a
        // knight stall the boss indefinitely with chip damage.
        Vector3 before = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, _knight.position,
            _config.approachSpeed * Time.deltaTime);
        UpdateSpriteDirection(transform.position - before);

        if (!_wardPhaseEntered && DistanceToKnight <= _openingDistance * _config.wardPhaseDistanceFraction)
        {
            EnterWardPhase();
        }
    }

    public float DistanceToKnight =>
        _knight != null ? Vector2.Distance(transform.position, _knight.position) : float.MaxValue;

    // How long this twin takes to cross the arena and reach its knight — i.e. the
    // length of the whole fight, since arrival ends it. Constant once Initialize has
    // run (the opening distance never changes), and derived from the authored geometry
    // so anything paced against it (the orb drops) follows a retune of approachSpeed
    // or the entrance instead of silently desyncing from hardcoded delays.
    public float EstimatedSecondsToContact
    {
        get
        {
            if (_config == null || _config.approachSpeed <= 0f) return 0f;
            // Contact lands when the body touches, not the pivot — allow its reach
            float reach = _collider != null
                ? Mathf.Max(_collider.bounds.extents.x, _collider.bounds.extents.y)
                : _config.scale * 0.5f;
            return Mathf.Max(0f, (_openingDistance - reach) / _config.approachSpeed);
        }
    }

    #region volleys (driven by GiantSlimeDuel)

    // Telegraph only — the duel flashes both twins together, waits, then fires both
    public void Telegraph(float seconds)
    {
        if (isDead) return;
        glowManager?.StartGlow(new Color(1f, 0.35f, 0.15f), seconds, 9f, 0.7f);
    }

    public void FireVolley(VolleyPattern pattern)
    {
        if (isDead || _config == null || _knight == null || _fireballPrefab == null) return;
        if (_wardPhaseEntered) return; // ward phase replaces shooting entirely

        if (pattern == VolleyPattern.MirroredArc)
        {
            EnemyFireball.Launch(_fireballPrefab, MouthPosition(), _knight,
                EnemyFireball.Mode.ArcThenHome, _config.fireballSpeed, _config.fireballDamage,
                _config.arcDegrees, _config.arcSeconds, _config.homingTurnRate,
                MirrorSign, _config.fireballLifetime, _config.fireballScale);
        }
        else
        {
            StartCoroutine(StraightVolley());
        }
    }

    private IEnumerator StraightVolley()
    {
        int shots = Mathf.Max(1, _config.straightShotsPerVolley);
        for (int i = 0; i < shots; i++)
        {
            if (isDead || _wardPhaseEntered) yield break;
            EnemyFireball.Launch(_fireballPrefab, MouthPosition(), _knight,
                EnemyFireball.Mode.Straight, _config.fireballSpeed, _config.fireballDamage,
                0f, 0f, 0f, MirrorSign, _config.fireballLifetime, _config.fireballScale);
            if (i < shots - 1) yield return new WaitForSeconds(_config.straightShotStagger);
        }
    }

    // Shots leave the leading edge rather than the centre so they don't hatch out
    // from inside a body that's several units wide
    private Vector2 MouthPosition()
    {
        Vector2 center = BodyCenter;
        if (_knight == null) return center;
        Vector2 toward = ((Vector2)_knight.position - center).normalized;
        return center + toward * (_config.scale * 0.42f);
    }

    #endregion

    #region ward

    private void EnterWardPhase()
    {
        _wardPhaseEntered = true;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossRoar);
        glowManager?.StartGlow(new Color(1f, 0.5f, 0.2f), 1f, 10f, 0.8f);
        SpawnWard();
    }

    private void SpawnWard()
    {
        if (isDead || _config == null) return;
        // Twins start their wards on opposite sides, so the pair reads as a mirror
        float startAngle = _side == Side.Left ? 0f : 180f;
        _ward = EnemyFireball.Orbit(_fireballPrefab, this, _config.wardRadius,
            _config.wardRadiusSwing, _config.wardPulseSeconds,
            _config.wardDegreesPerSecond * MirrorSign, startAngle,
            _config.fireballDamage, _config.wardScale);
    }

    // Called by the ward itself as it pops
    public void OnWardDestroyed()
    {
        _ward = null;
        if (isDead) return;
        if (_wardRespawn != null) StopCoroutine(_wardRespawn);
        _wardRespawn = StartCoroutine(RespawnWardAfterDelay());
    }

    private IEnumerator RespawnWardAfterDelay()
    {
        yield return new WaitForSeconds(_config.wardRespawnSeconds);
        if (!isDead && _wardPhaseEntered && _ward == null)
        {
            SpawnWard();
        }
    }

    #endregion

    // The ward eats every direct hit meant for the body: arrows, sword, shield. Note
    // that poison and fire DoT tick straight into `health` (PoisonRoutine and
    // ApplyFireDamage bypass TakeDamage), so Serpent and Ember still chew through a
    // warded slime — intentional, or a fully warded boss would blank two Orders.
    public override void TakeDamage(int damage, GameObject projectile)
    {
        if (isDead) return;

        if (_ward != null)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.projectileShield);
            glowManager?.StartGlow(new Color(1f, 0.85f, 0.4f), 0.2f, 12f, 0.5f);
            return;
        }

        base.TakeDamage(damage, projectile);
    }

    // A boss can't be popped by contact the way a mob can (see EnemyRatKing): the
    // shield gets no purchase on it, and reaching a knight kills the KNIGHT — the
    // slime walks on through.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            // Damage was applied by PlayerProjectile (or refused above); eat the arrow
            Destroy(other.gameObject);
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(GetPlayerCollisionDamage(), DisplayName);
            }
        }
    }

    protected override int GetPlayerCollisionDamage() => _config != null ? _config.contactDamage : playerDamage;

    protected override void OnDeath()
    {
        // No Split() — see the attributes note in Start
        if (_ward != null)
        {
            Destroy(_ward.gameObject);
            _ward = null;
        }
        base.OnDeath();
    }

    public override float GetMaxHealth() => _config != null ? _config.health : base.GetMaxHealth();

    protected override string StatKey => "giantslime";
}

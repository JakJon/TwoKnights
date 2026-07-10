using UnityEngine;
using System.Collections;

// The Rat King: a boss that circles the arena on a fixed rectangle rail.
// Design rules honored: he never body-slams a knight (rail keeps distance, and
// shield contact can't pop him like a regular mob), projectile fans only fire
// from the top/bottom arcs so a volley at one knight never crosses the other,
// and side-edge stops summon adds instead. Phases escalate tempo by HP.
public class EnemyRatKing : EnemyBase
{
    [System.Serializable]
    public class Config
    {
        public float health = 750f;
        public float moveSpeed = 3f;
        public float actionCooldown = 3.5f; // phase-1 seconds between actions
        public float telegraphPause = 0.9f; // glow warning before each attack
        public int fanProjectiles = 5;
        public float fanArcDegrees = 60f;
        public int fanArcCount = 1;
        public int batsPerSummon = 2;
        public int ratsPerSummonLate = 1; // rats join summons from phase 2
        public int goldReward = 50;
        public int specialOnDeathReward = 50;
    }

    // Rectangle rail just inside the visible frame (x +-10, y +-5.6):
    // corners and edge midpoints, walked clockwise
    private static readonly Vector2[] Circuit =
    {
        new Vector2(0f, 4.5f),
        new Vector2(8.5f, 4.5f),
        new Vector2(8.5f, 0f),
        new Vector2(8.5f, -4.5f),
        new Vector2(0f, -4.5f),
        new Vector2(-8.5f, -4.5f),
        new Vector2(-8.5f, 0f),
        new Vector2(-8.5f, 4.5f),
    };

    // Fans only from stops this far off the horizontal midline (steep angles)
    private const float FanMinHeight = 2.5f;

    private Spawner _spawner;
    private Config _config;
    private float _maxHealth;
    private int _waypointIndex;
    private float _sinceLastAction;
    private bool _entering = true;
    private bool _acting;
    private int _lastSeenPhase = 1;
    private Transform _leftKnight;
    private Transform _rightKnight;

    // 1 (fresh) -> 2 (bloodied) -> 3 (enraged)
    public int Phase
    {
        get
        {
            float fraction = _maxHealth > 0f ? health / _maxHealth : 1f;
            if (fraction > 0.66f) return 1;
            if (fraction > 0.33f) return 2;
            return 3;
        }
    }

    public void Initialize(Spawner spawner, Config config)
    {
        _spawner = spawner;
        _config = config;
        _maxHealth = config.health;
        health = config.health;
        goldOnDeath = config.goldReward;
        specialOnDeath = config.specialOnDeathReward;
    }

    private void Start()
    {
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        if (AudioManager.Instance != null)
        {
            hurtSound = AudioManager.Instance.ratHurt;
            deathSound = AudioManager.Instance.ratDeath;
        }

        var left = GameObject.FindWithTag("PlayerLeft");
        var right = GameObject.FindWithTag("PlayerRight");
        _leftKnight = left != null ? left.transform : null;
        _rightKnight = right != null ? right.transform : null;

        // First action comes soon after the entrance settles
        _sinceLastAction = 1.5f;
    }

    private void Update()
    {
        if (isDead || _acting || _config == null) return;

        _sinceLastAction += Time.deltaTime;

        float speed = _config.moveSpeed * PhaseSpeedMultiplier();
        Vector2 target = _entering ? Circuit[0] : Circuit[_waypointIndex];
        Vector3 before = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
        UpdateSpriteDirection(transform.position - before);

        if (Vector2.Distance(transform.position, target) < 0.05f)
        {
            if (_entering)
            {
                _entering = false;
                _waypointIndex = 1;
                return;
            }

            Vector2 stopPosition = Circuit[_waypointIndex];
            _waypointIndex = (_waypointIndex + 1) % Circuit.Length;

            if (_sinceLastAction >= CurrentActionCooldown())
            {
                StartCoroutine(ActRoutine(stopPosition));
            }
        }
    }

    private float PhaseSpeedMultiplier()
    {
        switch (Phase)
        {
            case 3: return 1.35f;
            case 2: return 1.15f;
            default: return 1f;
        }
    }

    private float CurrentActionCooldown()
    {
        switch (Phase)
        {
            case 3: return _config.actionCooldown * 0.55f;
            case 2: return _config.actionCooldown * 0.75f;
            default: return _config.actionCooldown;
        }
    }

    private IEnumerator ActRoutine(Vector2 stopPosition)
    {
        _acting = true;
        _sinceLastAction = 0f;

        // Telegraph: the king rears up and glows before every attack
        Color warn = Phase == 3 ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.85f, 0.65f, 0.2f);
        glowManager?.StartGlow(warn, _config.telegraphPause, 9f, 0.65f);
        yield return new WaitForSeconds(_config.telegraphPause);

        if (!isDead)
        {
            if (Mathf.Abs(stopPosition.y) >= FanMinHeight)
            {
                FireFan();
            }
            else
            {
                SummonAdds();
            }
        }

        yield return new WaitForSeconds(0.4f);
        _acting = false;
    }

    private void FireFan()
    {
        Transform target = NearestKnight();
        if (target == null || _spawner == null) return;

        var direction = transform.position.x >= 0f
            ? Spawner.ArcDirection.Clockwise
            : Spawner.ArcDirection.CounterClockwise;
        int projectiles = _config.fanProjectiles + (Phase - 1);
        int arcs = Phase == 3 ? _config.fanArcCount + 1 : _config.fanArcCount;

        _spawner.SpawnProjectileArc(target, direction, transform.position,
            _config.fanArcDegrees, projectiles, 0.12f, arcs, 0.35f);
    }

    private void SummonAdds()
    {
        if (_spawner == null) return;

        int bats = _config.batsPerSummon + (Phase == 3 ? 1 : 0);
        for (int i = 0; i < bats; i++)
        {
            Vector2 pos = (Vector2)transform.position
                + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-0.8f, 0.8f));
            _spawner.SpawnBat(pos, i * 0.35f);
        }

        if (Phase >= 2 && _config.ratsPerSummonLate > 0)
        {
            for (int i = 0; i < _config.ratsPerSummonLate; i++)
            {
                Transform knight = (i % 2 == 0) ? _leftKnight : _rightKnight;
                if (knight == null) continue;
                Vector2 patrolSpot = new Vector2(
                    knight.position.x + Random.Range(-2.5f, 2.5f),
                    Random.Range(-3f, 3f));
                _spawner.SpawnRat(patrolSpot, _spawner.brownRat, 0f, knight);
            }
        }
    }

    private Transform NearestKnight()
    {
        if (_leftKnight == null) return _rightKnight;
        if (_rightKnight == null) return _leftKnight;
        float toLeft = Vector2.Distance(transform.position, _leftKnight.position);
        float toRight = Vector2.Distance(transform.position, _rightKnight.position);
        return toLeft <= toRight ? _leftKnight : _rightKnight;
    }

    // Phase transitions roar (red flash) and send recovery orbs across the arena
    protected override void OnAfterDamageApplied(int damage, GameObject projectile)
    {
        int phase = Phase;
        if (phase == _lastSeenPhase || isDead) return;
        _lastSeenPhase = phase;

        glowManager?.StartGlow(new Color(0.9f, 0.15f, 0.1f), 1.2f, 10f, 0.85f);
        if (_spawner != null)
        {
            _spawner.SpawnOrb(new Vector2(-12f, 2.5f), new Vector2(12f, 2.5f), true, 0.5f);
            _spawner.SpawnOrb(new Vector2(12f, -2.5f), new Vector2(-12f, -2.5f), false, 0.5f);
        }
    }

    // The king can't be popped by shield or body contact like a regular mob —
    // arrows (handled by PlayerProjectile) are the only way through. His rail
    // never reaches the knights, so no contact damage is dealt either.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            // Damage was applied by PlayerProjectile; just consume the arrow
            Destroy(other.gameObject);
        }
    }

    public override float GetMaxHealth() => _maxHealth > 0f ? _maxHealth : health;
}

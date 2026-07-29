using UnityEngine;
using System.Collections;

// The Rat King: a boss that circles the arena on a fixed rectangle rail.
// Design rules honored: he never body-slams a knight (rail keeps distance, and
// shield contact can't pop him like a regular mob), projectile fans only fire
// from the top/bottom arcs so a volley at one knight never crosses the other,
// two volleys never converge on the same knight at once (FanIsBlockable),
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

    // Launch stagger between projectiles within one fan volley
    private const float FanProjectileStagger = 0.12f;

    // A new fan at a knight may only land after their previous volley has fully
    // arrived plus this margin, so the shield has time to swing to the new arc.
    // Without it, a long-radius corner fan followed by a short-radius mid-edge
    // fan converge on the same knight at once from two angles — unblockable.
    private const float ShieldSwingSeconds = 1.5f;

    // Wave banner runs 5s from wave start (WaveName.cs); the king spawns 1.5s in,
    // so the health bar fades in once the banner has finished fading out
    private const float HealthBarRevealDelay = 3.5f;

    // Brood-throw geometry. The king only summons from the side-edge stops at
    // (+-8.5, 0) — level with the knights — so a rat flung past a knight would
    // cross it. Landing on the king's side of the target knight (within
    // BroodLandingSpread of the king->knight line) and beyond the shield's reach
    // (orbit 0.6 + rat/shield sizes) makes the whole straight flight path
    // provably clear of that knight and its shield.
    private const float BroodLandingMinDistance = 2.2f;
    private const float BroodLandingMaxDistance = 3.2f;
    private const float BroodLandingSpread = 45f; // degrees off the king->knight line

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

    // Time.time when the volley in flight toward each knight fully lands
    private float _leftVolleyClearTime;
    private float _rightVolleyClearTime;

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

    public void Initialize(Spawner spawner, Config config, string bossTitle = null)
    {
        _spawner = spawner;
        _config = config;
        _maxHealth = config.health;
        health = config.health;
        goldOnDeath = config.goldReward;
        specialOnDeath = config.specialOnDeathReward;
        BossHealthBar.Show(string.IsNullOrEmpty(bossTitle) ? DisplayName : bossTitle);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossBanner);
        StartCoroutine(RevealHealthBarAfterBanner());
    }

    private IEnumerator RevealHealthBarAfterBanner()
    {
        yield return new WaitForSeconds(HealthBarRevealDelay);
        if (!isDead)
        {
            BossHealthBar.Reveal();
        }
    }

    private void Start()
    {
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        if (AudioManager.Instance != null)
        {
            hurtSound = AudioManager.Instance.bossHurt;
            deathSound = AudioManager.Instance.bossDeath;
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
        // Push health every frame so poison ticks and mid-action hits all show
        if (_config != null && _maxHealth > 0f)
        {
            BossHealthBar.SetHealth(isDead ? 0f : health, _maxHealth);
        }

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

            // A fan stop where the volley would overlap the previous one is
            // skipped outright (no telegraph); the cooldown keeps accruing so
            // the king acts at the next safe stop instead.
            bool isFanStop = Mathf.Abs(stopPosition.y) >= FanMinHeight;
            if (_sinceLastAction >= CurrentActionCooldown() && (!isFanStop || FanIsBlockable(stopPosition)))
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
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossTelegraph);
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

    // True when a fan fired from this stop would finish arriving cleanly after
    // the volley already heading at the same knight (plus shield-swing time).
    // In phase 3 the king acts at every stop, and a corner fan (long radius,
    // slow to land) chased by the next mid-edge fan (short radius) can converge
    // on one knight simultaneously from two angles — that combination is
    // impossible to block with a single shield, so those fans are disallowed.
    private bool FanIsBlockable(Vector2 stopPosition)
    {
        Transform target = NearestKnight();
        if (target == null || _spawner == null) return true;

        float previousClear = target == _leftKnight ? _leftVolleyClearTime : _rightVolleyClearTime;
        float firstArrival = Time.time + _config.telegraphPause
            + _spawner.ProjectileArcFlightSeconds(target, stopPosition);
        return firstArrival >= previousClear + ShieldSwingSeconds;
    }

    private void FireFan()
    {
        Transform target = NearestKnight();
        if (target == null || _spawner == null) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossFan);

        var direction = transform.position.x >= 0f
            ? Spawner.ArcDirection.Clockwise
            : Spawner.ArcDirection.CounterClockwise;
        // Always a single arc per volley: stacked arcs interleave into confusing
        // back-to-back projectile pairs. Phases escalate via projectile count instead.
        int projectiles = _config.fanProjectiles + (Phase - 1);

        _spawner.SpawnProjectileArc(target, direction, transform.position,
            _config.fanArcDegrees, projectiles, FanProjectileStagger);

        float clearTime = Time.time + (projectiles - 1) * FanProjectileStagger
            + _spawner.ProjectileArcFlightSeconds(target, transform.position);
        if (target == _leftKnight) _leftVolleyClearTime = clearTime;
        else _rightVolleyClearTime = clearTime;
    }

    private void SummonAdds()
    {
        if (_spawner == null) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossSummon);

        int bats = _config.batsPerSummon + (Phase == 3 ? 1 : 0);
        for (int i = 0; i < bats; i++)
        {
            Vector2 pos = (Vector2)transform.position
                + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-0.8f, 0.8f));
            _spawner.SpawnBat(pos, i * 0.35f);
        }

        if (Phase >= 2 && _config.ratsPerSummonLate > 0)
        {
            // Brood is flung at the knight on the king's side of the arena (he only
            // summons from the side edges, so the nearest knight sits roughly
            // straight ahead). SafeBroodLanding keeps the landing spot — and the
            // straight flight path the rat takes to it — clear of that knight and
            // its shield, so a thrown rat can't clip a knight before it patrols.
            Transform knight = NearestKnight();
            if (knight != null)
            {
                for (int i = 0; i < _config.ratsPerSummonLate; i++)
                {
                    // Brood bursts out of the king himself (entryPoint) — without it
                    // rats would instead walk in from the nearest screen edge.
                    _spawner.SpawnRat(SafeBroodLanding(knight), _spawner.brownRat, 0f, knight,
                        bypassTierGate: true, entryPoint: transform.position);
                }
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

    // A landing spot for a rat flung from the king toward a knight. The spot sits
    // on the king's side of the knight (its offset direction is within
    // BroodLandingSpread of the king->knight line) at a safe distance, so the
    // straight segment from the king to the spot only ever approaches the knight
    // as close as the spot itself — never crossing it or its shield. With the king
    // ~6.5u away and the spread under 90 degrees, the closest point of that
    // segment is guaranteed to be the endpoint.
    private Vector2 SafeBroodLanding(Transform knight)
    {
        Vector2 knightPos = knight.position;
        Vector2 towardKing = (Vector2)transform.position - knightPos;
        float baseAngle = Mathf.Atan2(towardKing.y, towardKing.x) * Mathf.Rad2Deg;
        float angle = (baseAngle + Random.Range(-BroodLandingSpread, BroodLandingSpread)) * Mathf.Deg2Rad;
        float distance = Random.Range(BroodLandingMinDistance, BroodLandingMaxDistance);
        return knightPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    // Phase transitions roar (red flash) and send recovery orbs across the arena
    protected override void OnAfterDamageApplied(int damage, GameObject projectile)
    {
        int phase = Phase;
        if (phase == _lastSeenPhase || isDead) return;
        _lastSeenPhase = phase;

        glowManager?.StartGlow(new Color(0.9f, 0.15f, 0.1f), 1.2f, 10f, 0.85f);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossRoar);
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

    // Covers death, despawn, and scene unload alike
    private void OnDestroy()
    {
        BossHealthBar.Hide();
    }
}

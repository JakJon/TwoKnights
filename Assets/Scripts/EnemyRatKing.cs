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
    // No execute: see EnemyBase.IsBoss
    public override bool IsBoss => true;

    public override EnemyFamily Family => EnemyFamily.Vermin;

    [System.Serializable]
    public class Config
    {
        public float health = 1200f;
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
    // (+-8.5, 0) — level with the knights — so a rat thrown along that line reads
    // as being hurled straight AT the knight and lands in its face. The landing
    // spot is therefore pushed well off the horizontal: partway back toward the
    // king (so the straight flight path never crosses the knight) and a full
    // BroodLandingRise above or below it, alternating. That vertical leg is what
    // keeps a thrown rat from ever sharing the king's own lane at the sides.
    //
    // The resulting gap from the knight (~3.5u) clears the shield's reach
    // (orbit 0.6 + rat/shield sizes) with room to spare, and because the spot
    // sits between the king and the knight in x, the closest the flight segment
    // ever comes to the knight is its own endpoint.
    private const float BroodLandingSetback = 2.4f; // toward the king, from the knight
    private const float BroodLandingRise = 2.6f;    // above or below the knight

    // Alternates the throw above/below rather than rolling for it — same rule as
    // the bat cadence: a wave plays out identically every time it is fought.
    private int _broodThrowCount;

    // Enraged, the king reaches a stop far more often than he did at full health,
    // and every one of those stops used to call the swarm. Every third phase-three
    // stop now goes by without a summon, so the brood arrives at roughly two thirds
    // the rate it did (owner's call). Counted rather than rolled, for the same
    // reason the throw alternates: the fight has to play out identically every time.
    private const int EnragedSummonSkipEvery = 3;
    private int _enragedStopCount;

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

    // HP remaining at which the king enrages into his final phase
    private const float EnrageHealthRemaining = 200f;

    // 1 (fresh) -> 2 (bloodied) -> 3 (enraged)
    public int Phase
    {
        get
        {
            // Phase three is keyed to an absolute HP remainder, not a fraction, so
            // the enraged finale is always the same length no matter the max HP.
            if (health <= EnrageHealthRemaining) return 3;
            float fraction = _maxHealth > 0f ? health / _maxHealth : 1f;
            if (fraction > 0.66f) return 1;
            return 2;
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
            //
            // Except in phase three. Enraged, the king is at a fan stop most of
            // the time, so skipping the unsafe ones was leaving him doing
            // nothing but throwing — the brood dried up exactly when the fight
            // was supposed to be at its worst. Now the stop becomes a summon
            // instead, and the swarm keeps arriving until he is dead — thinned
            // by BroodIsDueThisStop, which lets every third one go by empty.
            bool isFanStop = Mathf.Abs(stopPosition.y) >= FanMinHeight;
            if (_sinceLastAction >= CurrentActionCooldown())
            {
                bool fan = isFanStop && FanIsBlockable(stopPosition);
                if (fan || !isFanStop || Phase >= 3)
                {
                    // Asked here rather than inside the routine so a stop with
                    // nothing left to do is skipped outright, the way an unsafe
                    // fan stop is. Telegraphing a rear-up and then doing nothing
                    // would read as the king glitching.
                    bool brood = BroodIsDueThisStop();
                    if (fan || brood) StartCoroutine(ActRoutine(stopPosition, fan, brood));
                }
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

    private IEnumerator ActRoutine(Vector2 stopPosition, bool fan, bool brood)
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
            if (fan)
            {
                FireFan();

                // Enraged, a throw comes with brood. This is the whole point
                // of the phase-three change: the double volley is what the king
                // does, not INSTEAD of calling the swarm but on top of it, so
                // there is never a stretch of the fight with nothing to shoot at
                // but him. Trimmed, because the fan is already going out.
                if (Phase >= 3 && brood) SummonAdds(trimmed: true);
            }
            else if (brood)
            {
                SummonAdds();
            }
        }

        yield return new WaitForSeconds(0.4f);
        _acting = false;
    }

    /// <summary>
    /// Whether this stop calls the swarm. Always true before the king is enraged —
    /// the earlier phases act rarely enough that the brood was never the problem.
    /// In phase three every third stop is skipped, which is the only brake on a
    /// king who now acts at every stop he reaches.
    /// </summary>
    private bool BroodIsDueThisStop()
    {
        if (Phase < 3) return true;

        _enragedStopCount++;
        return _enragedStopCount % EnragedSummonSkipEvery != 0;
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

    /// <param name="trimmed">
    /// A summon riding alongside a fan rather than instead of one: one bat fewer
    /// and one rat, so the enraged king can do both at every stop without the
    /// arena filling faster than two knights can clear it.
    /// </param>
    private void SummonAdds(bool trimmed = false)
    {
        if (_spawner == null) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossSummon);

        int bats = trimmed
            ? Mathf.Max(1, _config.batsPerSummon - 1)
            : _config.batsPerSummon + (Phase == 3 ? 1 : 0);
        for (int i = 0; i < bats; i++)
        {
            Vector2 pos = (Vector2)transform.position
                + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-0.8f, 0.8f));
            _spawner.SpawnBat(pos, i * 0.35f, entersFromOffscreen: false);
        }

        int broodSize = trimmed
            ? Mathf.Min(1, _config.ratsPerSummonLate)
            : _config.ratsPerSummonLate;

        if (Phase >= 2 && broodSize > 0)
        {
            // Brood is flung at the knight on the king's side of the arena (he only
            // summons from the side edges, so the nearest knight sits roughly
            // straight ahead). SafeBroodLanding keeps the landing spot — and the
            // straight flight path the rat takes to it — clear of that knight and
            // its shield, so a thrown rat can't clip a knight before it patrols.
            Transform knight = NearestKnight();
            if (knight != null)
            {
                for (int i = 0; i < broodSize; i++)
                {
                    // Brood bursts out of the king himself (entryPoint) — without it
                    // rats would instead walk in from the nearest screen edge.
                    //
                    // His children are brown whatever the depth, so the type is
                    // named outright rather than taken from the map's cadence. That
                    // also leaves the cadence where it was: naming a rat does not
                    // advance the counter, so a king emptying himself mid-wave
                    // cannot shift the pattern the wave around him is running.
                    _spawner.SpawnRat(SafeBroodLanding(knight), 0f, knight, _spawner.brownRat,
                        bypassStrengthGate: true, entryPoint: transform.position);
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

    // A landing spot for a rat flung from the king toward a knight: set back
    // toward the king and lifted clear of the knight's row, alternating high and
    // low. Both legs matter — the setback keeps the straight segment from the
    // king short of the knight (so its closest approach is the endpoint), and the
    // rise keeps the rat out of the king's own horizontal lane, which at the side
    // stops is also the knight's.
    private Vector2 SafeBroodLanding(Transform knight)
    {
        Vector2 knightPos = knight.position;
        float towardKing = Mathf.Sign(transform.position.x - knightPos.x);
        if (towardKing == 0f) towardKing = 1f;

        float rise = (_broodThrowCount % 2 == 0) ? BroodLandingRise : -BroodLandingRise;
        _broodThrowCount++;

        return knightPos + new Vector2(towardKing * BroodLandingSetback, rise);
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
    // Like the Crimson Twins, the King is not popped by contact — this override
    // exists to eat arrows and nothing else. Bulwark is the one thing the guard can
    // do to him, added 2026-09-08; before that this branch did not exist and the
    // capstone silently did nothing to either boss.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Shield"))
        {
            if (TryBulwarkShove(other) && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
            }
            return;
        }

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

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
    // No execute: see EnemyBase.IsBoss
    public override bool IsBoss => true;

    public override EnemyFamily Family => EnemyFamily.Ooze;

    public enum Side { Left, Right }

    // The two volley patterns, alternated by GiantSlimeDuel
    public enum VolleyPattern { MirroredArc, Straight }

    [System.Serializable]
    public class Config
    {
        [Header("Body")]
        [Tooltip("Health per slime. The shared boss bar shows the pair, so it holds twice this.")]
        public float health = 2000f;
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
        [Tooltip("Fireballs in the ring, spaced evenly around it. One since 2026-09-26 " +
                 "(owner) — was four.")]
        public int wardCount = 1;
        [Tooltip("Orbit radius the ring breathes around, in units from the slime's " +
                 "body centre.")]
        public float wardRadius = 4.6f;
        [Tooltip("Peak-to-peak swing of that radius. 0 holds the fireball at exactly " +
                 "wardRadius (owner, 2026-09-26). It was 3, which breathed the ring " +
                 "3.1 -> 6.1 units out.")]
        public float wardRadiusSwing = 0f;
        [Tooltip("Seconds for one full out-and-back breath. Kept off the orbit period " +
                 "(360/wardDegreesPerSecond) on purpose so the far extreme drifts around " +
                 "the circle instead of always lunging the same way.")]
        public float wardPulseSeconds = 4f;
        public float wardDegreesPerSecond = 110f;
        [Tooltip("Seconds before a popped fireball comes back in its own place in the ring.")]
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
    // One entry per place in the ring. A popped fireball leaves its slot null until
    // it comes back, so the others keep their spacing around the gap.
    private EnemyFireball[] _wards;
    private Coroutine[] _wardRespawns;
    private bool _wardPhaseEntered;
    private Collider2D _collider;

    public Side WhichSide => _side;
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

    // The ward is a HAZARD, not a shield (owner's call, 2026-09-24). It used to
    // refuse every hit on the body while a fireball was up, with a three-second
    // window after each pop, and nothing on screen said so. The twins now take
    // damage at all times; the ring is only something the knights' shields have to
    // answer when it swings out past them.

    private float WardSpacing => 360f / Mathf.Max(1, _wards.Length);

    private void EnterWardPhase()
    {
        _wardPhaseEntered = true;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossRoar);
        glowManager?.StartGlow(new Color(1f, 0.5f, 0.2f), 1f, 10f, 0.8f);

        int count = Mathf.Max(1, _config.wardCount);
        _wards = new EnemyFireball[count];
        _wardRespawns = new Coroutine[count];

        // One launch sound for the whole ring. Four copies of the clip on one frame
        // just play it four times as loud.
        for (int slot = 0; slot < count; slot++)
        {
            SpawnWard(slot, playSound: slot == 0);
        }
    }

    private void SpawnWard(int slot, bool playSound = true)
    {
        if (isDead || _config == null || _wards == null) return;

        // A fireball coming back rejoins the ring where its slot is NOW, read off a
        // fireball that is still up. Starting it from the opening layout instead
        // would drop it wherever that happens to be after the ring has turned, and
        // over a few pops the four would bunch up. Only when the whole ring is down
        // is there nothing to line up with, and it starts fresh.
        int siblingSlot = FirstLiveWardSlot();
        float angle;
        float pulseTime = 0f;
        if (siblingSlot >= 0)
        {
            EnemyFireball sibling = _wards[siblingSlot];
            angle = sibling.OrbitAngle + (slot - siblingSlot) * WardSpacing * MirrorSign;
            pulseTime = sibling.PulseTime;
        }
        else
        {
            // Twins start their rings on opposite sides and spaced the opposite way
            // round, so the pair reads as a mirror
            angle = (_side == Side.Left ? 0f : 180f) + slot * WardSpacing * MirrorSign;
        }

        _wards[slot] = EnemyFireball.Orbit(_fireballPrefab, this, _config.wardRadius,
            _config.wardRadiusSwing, _config.wardPulseSeconds,
            _config.wardDegreesPerSecond * MirrorSign, angle,
            _config.fireballDamage, _config.wardScale, pulseTime, playSound);
    }

    private int FirstLiveWardSlot()
    {
        for (int i = 0; i < _wards.Length; i++)
        {
            if (_wards[i] != null) return i;
        }
        return -1;
    }

    // Called by a ward fireball as it pops
    public void OnWardDestroyed(EnemyFireball ward)
    {
        if (_wards == null) return;
        int slot = System.Array.IndexOf(_wards, ward);
        if (slot < 0) return;

        _wards[slot] = null;
        if (isDead) return;
        if (_wardRespawns[slot] != null) StopCoroutine(_wardRespawns[slot]);
        _wardRespawns[slot] = StartCoroutine(RespawnWardAfterDelay(slot));
    }

    private IEnumerator RespawnWardAfterDelay(int slot)
    {
        yield return new WaitForSeconds(_config.wardRespawnSeconds);
        _wardRespawns[slot] = null;
        if (!isDead && _wardPhaseEntered && _wards[slot] == null)
        {
            SpawnWard(slot);
        }
    }

    #endregion

    // A boss can't be popped by contact the way a mob can (see EnemyRatKing): the
    // shield gets no purchase on it, and reaching a knight kills the KNIGHT — the
    // slime walks on through.
    //
    // Bulwark is the one exception, added 2026-09-08. Until then this override had
    // no Shield branch at all and the capstone silently did nothing to the Twins.
    //
    // IT IS DELIBERATELY NOT WEAKENED FOR A BOSS (owner's call): the full two units,
    // no extra cooldown. Know what that buys — approachSpeed is 0.58 u/s and the
    // config calls that walk THE FIGHT TIMER, so every contact rewinds the clock by
    // about three and a half seconds, and the guard orbits between the knight and
    // the slime. A player holding the shield toward a slime can keep it off more or
    // less indefinitely.
    //
    // That is intended, and it is not a defect waiting on a nerf. A player who has
    // drafted the Guardian capstone and worked out that the guard can hold a boss
    // off for the whole fight has found something, and finding it is the point. The
    // lever is here rather than in Shove, if it is ever genuinely wanted lower.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Shield"))
        {
            // Sound only when it actually lands, so a knight without the capstone
            // hears nothing new — the slime is still walking through the guard.
            if (TryBulwarkShove(other) && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
            }
            return;
        }

        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            // Damage was applied by PlayerProjectile; eat the arrow
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
        if (_wards != null)
        {
            for (int i = 0; i < _wards.Length; i++)
            {
                if (_wards[i] != null) Destroy(_wards[i].gameObject);
                _wards[i] = null;
            }
        }
        base.OnDeath();
    }

    public override float GetMaxHealth() => _config != null ? _config.health : base.GetMaxHealth();

    protected override string StatKey => "giantslime";
}

using System.Collections.Generic;
using UnityEngine;

public enum WolfType
{
    Grey,
    Brown,
    Black
}

public class EnemyWolf : EnemyBase
{
    public override EnemyFamily Family => EnemyFamily.Beast;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f; // Overridden by type in Spawn
    [SerializeField] private int damage = 20; // Collision damage to player
    [SerializeField] private float waypointReachThreshold = 0.1f;
    [Header("Type")]
    [SerializeField] private WolfType wolfType = WolfType.Grey;

    // Runtime state
    private List<Vector3> _waypoints = new List<Vector3>();
    private int _currentWaypointIndex = 0;
    private bool _followingPath = false;
    private bool _isChasing = false;
    private Transform _targetKnight;
    private bool _hasPlayedChaseSFX = false;

    /// <summary>
    /// The least warning a wolf gives (owner, 2026-10-14): it never reaches a
    /// knight or a shield less than this many seconds after coming on screen.
    /// Every wolf wave's paths and timings are tuned to it. In the editor and in
    /// dev builds a wolf that strikes sooner says so in the log, with the wave it
    /// came from, so a new wave or a retuned tier that breaks it shows up the
    /// first time it is played.
    /// </summary>
    public const float MinSecondsOnScreen = 6f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float _onScreenSince = -1f;
    private bool _strikeChecked;
    private Collider2D _body;
#endif

    // Setters called by Spawner before Start
    public void SetWolfType(WolfType type) => wolfType = type;
    public void SetWaypoints(List<Vector3> waypoints) => _waypoints = waypoints != null ? new List<Vector3>(waypoints) : new List<Vector3>();
    public void SetTarget(Transform target) => _targetKnight = target;

    // Speed along the authored path only; 0 keeps the type's own. A pack that
    // runs one path in a line (Wolf Invaders) holds its spacing on it this way,
    // then each wolf charges at its own pace once the path is done.
    private float _pathSpeed;
    public void SetPathSpeed(float speed) => _pathSpeed = Mathf.Max(0f, speed);

    private void Start()
    {
        // Initialize EnemyBase fields with wolf-specific values
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        specialOnDeath = 15;
        shieldDamage = 10;
        playerDamage = damage;

        hurtSound = AudioManager.Instance != null ? AudioManager.Instance.wolfHurt : null;
        deathSound = AudioManager.Instance != null ? AudioManager.Instance.wolfDeath : null;

        // Configure stats by type
        switch (wolfType)
        {
            case WolfType.Brown: // Fastest, least health
                health = 30f;
                moveSpeed = 3.5f; // fastest
                break;
            case WolfType.Grey: // Medium
                health = 45f;
                moveSpeed = 3.0f;
                break;
            case WolfType.Black: // Slowest, most health
                health = 60f;
                moveSpeed = 2.5f; // slowest
                break;
        }

        // Setup path state (Spawner sets initial position if waypoints provided)
        if (_waypoints != null && _waypoints.Count > 0)
        {
            float d0 = Vector3.Distance(transform.position, _waypoints[0]);
            _currentWaypointIndex = (d0 <= waypointReachThreshold) ? 1 : 0;
            _followingPath = _currentWaypointIndex < _waypoints.Count;
            _isChasing = !_followingPath;
        }
        else
        {
            _followingPath = false;
            _isChasing = true;
        }

    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // When the body first shows. The middle of its box rather than its feet,
        // so a wolf half over the edge does not count yet.
        if (_onScreenSince < 0f)
        {
            if (_body == null) _body = GetComponent<Collider2D>();
            Vector2 middle = _body != null ? (Vector2)_body.bounds.center : (Vector2)transform.position;
            if (Spawner.IsInView(middle)) _onScreenSince = Time.time;
        }
#endif

        // Pause movement during stagger
        if (IsStaggered) return;

        if (_followingPath)
        {
            FollowPath();
            return;
        }

        if (_isChasing && _targetKnight != null)
        {
            // A BARK, once, on the frame the wolf gives up its path and picks a
            // knight. This used to be a growl, which is the wrong cue: the growl
            // reads as ambience the player can let wash past, and this moment is
            // the one thing about a wolf the player has to answer - it has chosen
            // a side, and that side's knight has to start turning. Gated on the
            // aggro flip rather than on spawning, so a pack staggers itself
            // instead of barking as one wall of noise.
            if (!_hasPlayedChaseSFX && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.wolfBark);
                _hasPlayedChaseSFX = true;
            }
            ChaseTarget();
        }
    }

    private void FollowPath()
    {
        if (_currentWaypointIndex >= _waypoints.Count)
        {
            _followingPath = false;
            _isChasing = true;
            return;
        }

        Vector3 target = _waypoints[_currentWaypointIndex];

        float speed = _pathSpeed > 0f ? _pathSpeed : moveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        UpdateSpriteDirection(transform.position, target);

        if (Vector3.Distance(transform.position, target) <= waypointReachThreshold)
        {
            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _waypoints.Count)
            {
                _followingPath = false;
                _isChasing = true;
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // The warning-time check. Only the first knight or shield it touches counts:
    // a wolf a Bulwark shield throws back has already given its warning.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDead && !_strikeChecked &&
            (other.CompareTag("Shield") || other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight")))
        {
            _strikeChecked = true;
            float onScreen = _onScreenSince < 0f ? 0f : Time.time - _onScreenSince;
            if (onScreen < MinSecondsOnScreen)
            {
                var wave = WaveManager.ActiveInstance != null ? WaveManager.ActiveInstance.CurrentWave : null;
                Debug.LogWarning($"[Wolves] {(wave != null ? wave.WaveName : "no wave")}: a {wolfType} wolf struck " +
                                 $"{onScreen:0.0}s after coming on screen. The floor is {MinSecondsOnScreen:0}s " +
                                 $"(EnemyWolf.MinSecondsOnScreen) - lengthen its path or its wait.", this);
            }
        }

        base.OnTriggerEnter2D(other);
    }
#endif

    private void ChaseTarget()
    {
        Vector3 targetPos = _targetKnight.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        UpdateSpriteDirection(transform.position, targetPos);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_waypoints == null || _waypoints.Count == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            Gizmos.DrawSphere(_waypoints[i], 0.1f);
            if (i + 1 < _waypoints.Count)
            {
                Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
            }
        }
    }
#endif
}

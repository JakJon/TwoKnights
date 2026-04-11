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

    // Setters called by Spawner before Start
    public void SetWolfType(WolfType type) => wolfType = type;
    public void SetWaypoints(List<Vector3> waypoints) => _waypoints = waypoints != null ? new List<Vector3>(waypoints) : new List<Vector3>();
    public void SetTarget(Transform target) => _targetKnight = target;

    private void Start()
    {
        // Initialize EnemyBase fields with wolf-specific values
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        specialOnDeath = 15;
        shieldDamage = 10;
        playerDamage = damage;

        // Temporary SFX placeholders (replace with wolf SFX when available)
        hurtSound = AudioManager.Instance != null ? AudioManager.Instance.ratHurt : null; // TODO: wolf hurt
        deathSound = AudioManager.Instance != null ? AudioManager.Instance.ratDeath : null; // TODO: wolf death

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

        // Spawn SFX (placeholder)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.ratSpawn);
        }
    }

    private void Update()
    {
        // Pause movement during stagger
        if (IsStaggered) return;

        if (_followingPath)
        {
            FollowPath();
            return;
        }

        if (_isChasing && _targetKnight != null)
        {
            if (!_hasPlayedChaseSFX && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.ratChase); // placeholder
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

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
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

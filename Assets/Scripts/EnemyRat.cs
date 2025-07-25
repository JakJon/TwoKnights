using System.Collections;
using UnityEngine;

public class EnemyRat : EnemyBase
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float moveDistance;
    [SerializeField] private int damage;
    [Header("Spawning")]
    [SerializeField] private float spawnMoveDuration;

    private float chaseDelay = 15f;
    private Vector3 _startPos;
    private Vector3 _targetPosition;
    private bool _movingRight = true;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;
    private bool _isChasing;
    private bool _isSpawning = true;
    private float _chaseTimer;
    private Transform _assignedPlayer;
    private bool _hasPlayedChaseSFX = false; 

    public void InitializeTarget(Transform playerTarget)
    {
        _assignedPlayer = playerTarget;
    }

    void Start()
    {
        // Initialize EnemyBase fields with rat-specific values
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        specialOnDeath = 15;
        shieldDamage = 10;
        playerDamage = damage;
        hurtSound = AudioManager.Instance.ratHurt;
        deathSound = AudioManager.Instance.ratDeath;

        // Spawning setup
        _targetPosition = transform.position;
        transform.position = GetAdjustedSpawnPosition(_targetPosition);
        _startPos = _targetPosition; // Patrol will use this as center point
        _spriteRenderer = GetComponent<SpriteRenderer>();
        StartCoroutine(EnterScreenRoutine());
        AudioManager.Instance.PlaySFX(AudioManager.Instance.ratSpawn);
    }

    private IEnumerator EnterScreenRoutine()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < spawnMoveDuration)
        {
            transform.position = Vector3.Lerp(startPos, _targetPosition, elapsed / spawnMoveDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = _targetPosition;
        _isSpawning = false;
    }

    void Update()
    {
        if (_isSpawning) return;

        if (!_isChasing)
        {
            _chaseTimer += Time.deltaTime;
            if (_chaseTimer >= chaseDelay && _assignedPlayer != null)
            {
                StartChasing(_assignedPlayer);
            }
            _hasPlayedChaseSFX = false; 
        }

        if (_isChasing && _playerTransform != null)
        {
            if (!_hasPlayedChaseSFX)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.ratChase);
                _hasPlayedChaseSFX = true;
            }
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private Vector2 GetAdjustedSpawnPosition(Vector2 originalPosition)
    {
        const float leftBound = -11f;
        const float rightBound = 11f;
        const float topBound = 5f;
        const float bottomBound = -5f;

        float[] distances = {
            Mathf.Abs(originalPosition.x - leftBound),
            Mathf.Abs(originalPosition.x - rightBound),
            Mathf.Abs(originalPosition.y - topBound),
            Mathf.Abs(originalPosition.y - bottomBound)
        };

        int closestIndex = 0;
        for (int i = 1; i < distances.Length; i++)
        {
            if (distances[i] < distances[closestIndex]) closestIndex = i;
        }

        Vector2 adjusted = originalPosition;
        switch (closestIndex)
        {
            case 0: adjusted.x = leftBound - 0.5f; break;
            case 1: adjusted.x = rightBound + 0.5f; break;
            case 2: adjusted.y = topBound + 0.5f; break;
            case 3: adjusted.y = bottomBound - 0.5f; break;
        }
        return adjusted;
    }

    private void Patrol()
    {
        Vector3 targetPos = _startPos + (_movingRight ? Vector3.right : Vector3.left) * moveDistance;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            _movingRight = !_movingRight;
            _spriteRenderer.flipX = !_movingRight;
        }
    }

    private void ChasePlayer()
    {
        Vector3 targetPos = _playerTransform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        _spriteRenderer.flipX = transform.position.x > targetPos.x;
    }

    public void StartChasing(Transform player)
    {
        _isChasing = true;
        _playerTransform = player;
    }

    // Override to handle additional collision with other ground enemies
    protected override void OnAdditionalCollision(Collider2D other)
    {
        // Check if we hit another enemy with Ground attribute
        var otherEnemy = other.GetComponent<MonoBehaviour>() as IHasAttributes;
        if (otherEnemy != null && otherEnemy.HasAttribute(EnemyType.Ground) && !_isChasing)
        {
            _movingRight = !_movingRight;
            _spriteRenderer.flipX = !_movingRight;
        }
    }
}
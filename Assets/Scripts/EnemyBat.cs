using UnityEngine;
using System.Collections;

public class EnemyBat : EnemyBase
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int damage;
    [SerializeField] private float spawnMoveDuration = 1f;

    private Vector3 _initialPosition;
    private Vector3 _intermediatePosition;
    private bool _isMovingToIntermediate = true;
    private SpriteRenderer _spriteRenderer;
    private Transform _assignedPlayer;

    private void Start()
    {
        // Initialize EnemyBase fields with bat-specific values
        attributes = EnemyType.Flying;
        specialOnHit = 5;
        specialOnDeath = 10;
        shieldDamage = 10;
        playerDamage = damage;
        hurtSound = AudioManager.Instance.ratHurt;  // TODO: Replace with bat hurt sound
        deathSound = AudioManager.Instance.ratDeath; // TODO: Replace with bat death sound
        
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _initialPosition = transform.position;
        
        // Determine intermediate position based on spawn Y coordinate
        float yTarget = _initialPosition.y >= 0 ? 3f : -3f;
        _intermediatePosition = new Vector3(0f, yTarget, 0f);
        
        // Determine target player based on spawn X coordinate
        string targetTag = _initialPosition.x < 0 ? "PlayerRight" : "PlayerLeft";
        _assignedPlayer = GameObject.FindWithTag(targetTag).transform;

        StartCoroutine(EnterScreenRoutine());
    }

    private IEnumerator EnterScreenRoutine()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < spawnMoveDuration)
        {
            transform.position = Vector3.Lerp(startPos, _initialPosition, elapsed / spawnMoveDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = _initialPosition;
    }

    private void Update()
    {
        // Stop movement during stagger
        if (IsStaggered) return;
        
        if (_isMovingToIntermediate)
        {
            MoveToIntermediate();
        }
        else if (_assignedPlayer != null)
        {
            ChasePlayer();
        }

        // Update sprite direction
        if (_spriteRenderer != null)
        {
            Vector3 moveDirection = (_isMovingToIntermediate ? 
                _intermediatePosition - transform.position : 
                _assignedPlayer.position - transform.position);
            _spriteRenderer.flipX = moveDirection.x < 0;
        }
    }

    private void MoveToIntermediate()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            _intermediatePosition,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, _intermediatePosition) < 0.1f)
        {
            _isMovingToIntermediate = false;
        }
    }

    private void ChasePlayer()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            _assignedPlayer.position,
            moveSpeed * Time.deltaTime
        );
    }
}
using UnityEngine;
using System.Collections;

// Dark-tier bat (deep forest, post-rat-king): flies like the basic bat, but
// once it has covered sonarFireFraction of its approach to the knight it
// shrieks a sonar wave — a spinning spiral that Confuses the knight (reversed
// shield controls) unless blocked, slashed, or shot down. The bat keeps
// closing in afterward.
public class EnemyDarkBat : EnemyBase
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int damage;
    [SerializeField] private float spawnMoveDuration = 1f;

    [Header("Sonar")]
    [SerializeField] private GameObject sonarPrefab;
    [Tooltip("Fraction of the spawn-to-knight path covered before the sonar fires (0.3 = 30%)")]
    [SerializeField] private float sonarFireFraction = 0.3f;

    // The bat's collider touches the knight at ~1 unit out, so the sonar must
    // always fire before that or the body-slam destroys the bat first
    private const float MinSonarFireDistance = 1.3f;

    private Vector3 _initialPosition;
    private Vector3 _intermediatePosition;
    private bool _isMovingToIntermediate = true;
    private SpriteRenderer _spriteRenderer;
    private Transform _assignedPlayer;
    private float _totalPathDistance = -1f;
    private bool _sonarFired;

    private void Start()
    {
        attributes = EnemyType.Flying;
        specialOnHit = 5;
        specialOnDeath = 10;
        shieldDamage = 10;
        playerDamage = damage;
        if (AudioManager.Instance != null)
        {
            hurtSound = AudioManager.Instance.batHurt;
            deathSound = AudioManager.Instance.batDeath;
        }

        _spriteRenderer = GetComponent<SpriteRenderer>();
        _initialPosition = transform.position;

        float yTarget = _initialPosition.y >= 0 ? 3f : -3f;
        _intermediatePosition = new Vector3(0f, yTarget, 0f);

        string targetTag = _initialPosition.x < 0 ? "PlayerRight" : "PlayerLeft";
        GameObject targetGo = GameObject.FindWithTag(targetTag);
        _assignedPlayer = targetGo != null ? targetGo.transform : null;

        // Full journey: spawn -> intermediate -> knight. The sonar fires once
        // sonarFireFraction of this path is behind the bat.
        if (_assignedPlayer != null)
        {
            _totalPathDistance = Vector3.Distance(_initialPosition, _intermediatePosition)
                + Vector3.Distance(_intermediatePosition, _assignedPlayer.position);
        }

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
        if (IsStaggered) return;

        if (_isMovingToIntermediate)
        {
            MoveToIntermediate();
        }
        else if (_assignedPlayer != null)
        {
            ChasePlayer();
        }

        TryFireSonar();

        if (_spriteRenderer != null && _assignedPlayer != null)
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

    private void TryFireSonar()
    {
        if (_sonarFired || _assignedPlayer == null || _totalPathDistance <= 0f) return;

        float remaining = _isMovingToIntermediate
            ? Vector3.Distance(transform.position, _intermediatePosition)
                + Vector3.Distance(_intermediatePosition, _assignedPlayer.position)
            : Vector3.Distance(transform.position, _assignedPlayer.position);

        float fireDistance = Mathf.Max(_totalPathDistance * (1f - sonarFireFraction), MinSonarFireDistance);
        if (remaining <= fireDistance)
        {
            FireSonar();
        }
    }

    private void FireSonar()
    {
        _sonarFired = true;
        if (sonarPrefab == null || _assignedPlayer == null) return;

        GameObject sonar = Instantiate(sonarPrefab);
        SonarWave wave = sonar.GetComponent<SonarWave>();
        if (wave != null)
        {
            wave.Initialize(transform.position, _assignedPlayer);
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.sonarPing);
        }
    }
}

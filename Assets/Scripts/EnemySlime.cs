using UnityEngine;

public class EnemySlime : MonoBehaviour, IHasAttributes
{
    [Header("Enemy Attributes")]
    [Tooltip("Type attributes of this enemy")]
    [SerializeField]
    private EnemyType attributes = EnemyType.Ground | EnemyType.Splitting;

    [Header("Slime Settings")]
    [Tooltip("Initial size of the slime (1=small, 2=medium, 3=large)")]
    public int size = 1;

    [Tooltip("Health points for the smallest slime")]
    public int baseHealth = 15;

    [Header("Splitting Settings")]
    [Tooltip("Force applied when slimes split apart")]
    public float splitForce = 5f;

    [Header("Movement")]
    [Tooltip("Movement speed towards the target player")]
    public float moveSpeed = 1.5f;

    public Transform targetPlayer; // The player this slime will chase

    // Base collider points for size 1 slime
    private Vector2[] baseColliderPoints = {
        new Vector2(-0.0244208574f, 0.9287871f),
        new Vector2(-0.177549958f, 0.914227962f),
        new Vector2(-0.25619173f, 0.7807456f),
        new Vector2(-0.281485319f, 0.629729748f),
        new Vector2(-0.490371466f, 0.333383322f),
        new Vector2(-0.50760293f, 0.202756524f),
        new Vector2(-0.4248706f, 0.031912446f),
        new Vector2(-0.26308465f, -0.0254480839f),
        new Vector2(0.0004911423f, -0.0194422f),
        new Vector2(0.331075966f, -0.01369369f),
        new Vector2(0.404925048f, 0.0718028545f),
        new Vector2(0.5050699f, 0.23904705f),
        new Vector2(0.456783056f, 0.474078774f),
        new Vector2(0.235103667f, 0.6545874f),
        new Vector2(0.0738047957f, 0.806524038f)
    };

    private int currentHealth;
    private PolygonCollider2D polyCollider; // Renamed from 'collider'
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    private void Awake()
    {
        polyCollider = GetComponent<PolygonCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        InitializeSlime();
    }

    private void Update()
    {
        if (targetPlayer != null)
        {
            Vector2 direction = (targetPlayer.position - transform.position).normalized;
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPlayer.position,
                moveSpeed * Time.deltaTime
            );
            // Flip sprite based on movement direction
            if (direction.x < -0.01f)
                spriteRenderer.flipX = true;
            else if (direction.x > 0.01f)
                spriteRenderer.flipX = false;
        }
    }

    public void InitializeSlime()
    {
        // Set health based on size
        currentHealth = baseHealth * size;

        // Set move speed based on size
        if (size == 3)
            moveSpeed = 0.2f;
        else if (size == 2)
            moveSpeed = 0.35f;
        else
            moveSpeed = 0.5f;

        // Scale the slime
        transform.localScale = Vector3.one * size;

        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (baseColliderPoints == null || baseColliderPoints.Length == 0)
        {
            Debug.LogWarning("Base collider points not set for Slime!");
            return;
        }

        // Scale collider points based on size
        Vector2[] scaledPoints = new Vector2[baseColliderPoints.Length];
        for (int i = 0; i < baseColliderPoints.Length; i++)
        {
            scaledPoints[i] = baseColliderPoints[i];
        }

        polyCollider.SetPath(0, scaledPoints);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            if (size > 1)
            {
                Split();
            }
            else
            {
                Die();
            }
        }
    }

    private void Split()
    {
        // Calculate the world-space width of the current slime
        float slimeWidth = polyCollider.bounds.size.x;
        Vector2 center = transform.position;
        
        // Left and right edge positions
        Vector2 leftEdge = center + Vector2.left * (slimeWidth / 2f);
        Vector2 rightEdge = center + Vector2.right * (slimeWidth / 2f);

        // Spawn two smaller slimes at the edges
        Vector2[] spawnPositions = { leftEdge, rightEdge };
        foreach (var spawnPosition in spawnPositions)
        {
            GameObject newSlime = Instantiate(gameObject, spawnPosition, Quaternion.identity);
            EnemySlime slimeScript = newSlime.GetComponent<EnemySlime>();
            slimeScript.size = size - 1;
            slimeScript.InitializeSlime();
        }

        Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Visual feedback on collision - with fallback
        FlashEffect();
    }

    private void FlashEffect()
    {
        // Try shader-based flash first
        if (spriteRenderer.material.HasProperty("_FlashAmount"))
        {
            spriteRenderer.material.SetFloat("_FlashAmount", 0.7f);
            Invoke("ResetFlash", 0.1f);
        }
        else
        {
            // Fallback to simple color flash
            StartCoroutine(SimpleFlash());
        }
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
            PlayerHealth playerHealth = other.transform.parent?.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(5 * size);
            Destroy(gameObject);
        }
        else if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyPlayer);
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(15 * size);
            Destroy(gameObject);
        }
    }

    private void ResetFlash()
    {
        if (spriteRenderer.material.HasProperty("_FlashAmount"))
        {
            spriteRenderer.material.SetFloat("_FlashAmount", 0f);
        }
    }

    private System.Collections.IEnumerator SimpleFlash()
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor;
    }

    public bool HasAttribute(EnemyType attr)
    {
        return (attributes & attr) == attr;
    }
}
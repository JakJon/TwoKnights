using UnityEngine;
using System.Collections;

public abstract class EnemyBase : MonoBehaviour, IHasAttributes
{
    [Header("Health")]
    [SerializeField] protected float health = 10f;

    [Header("Special Rewards")]
    [SerializeField] protected int specialOnHit = 5; // Points for damage
    [SerializeField] protected int specialOnDeath = 10; // Points for death

    [Header("Collision Damage")]
    [SerializeField] protected int shieldDamage = 10; // Damage to shield
    protected int playerDamage = 20; // Damage to player

    [Header("Stagger")]
    [SerializeField] protected float staggerDuration = 0.2f; // Stun duration
    [SerializeField] protected AnimationClip staggerAnimation; // Stagger anim
    [SerializeField] protected AnimationClip defaultAnimation; // Default anim

    [Header("Damage Text")]
    [SerializeField] protected GameObject damageTextPrefab; // Floating text prefab
    [SerializeField] protected Vector3 damageTextOffset = new Vector3(0, 0.01f, 0); // Text offset

    [Header("Audio")]
    [SerializeField] protected SoundEffect hurtSound; 
    [SerializeField] protected SoundEffect deathSound; 

    [Header("Enemy Attributes")]
    [SerializeField] protected EnemyType attributes;

    [Header("Sprite Flipping")]
    [SerializeField] protected float directionThreshold = 0.01f; 

    protected SpriteRenderer spriteRenderer;
    protected GlowManager glowManager;
    protected Animator animator;
    // Poison system
    protected bool isPoisoned = false;
    protected float poisonTimer = 0f;
    protected int poisonDamage = 0;
    protected float poisonTickRate = 1f;
    protected float lastPoisonTick = 0f;
    protected Coroutine poisonCoroutine = null;
    protected GameObject poisonSourceProjectile = null;
    public bool IsPoisoned => isPoisoned;
    // Stagger system
    protected bool isStaggered = false;
    protected AnimationClip originalAnimationClip;
    public bool IsStaggered => isStaggered;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glowManager = GetComponent<GlowManager>(); // Cache the component
        animator = GetComponent<Animator>(); // Cache the animator
        
        BaseWave.RegisterEnemy(gameObject); // Register for wave tracking
    }

    public virtual void TakeDamage(int damage, GameObject projectile)
    {
        // Extension point for pre-damage effects
        OnBeforeDamageApplied(damage, projectile);
        
        glowManager?.StartGlow(Color.red, 0.3f);
        ShowDamageText(damage);
        
        health -= damage;
        
        // Extension point for post-damage effects
        OnAfterDamageApplied(damage, projectile);
        
        if (health > 0)
        {
            StartCoroutine(StaggerRoutine()); // Stagger if alive
        }
        
        if (health <= 0)
        {
            // Give death special to the player who fired the projectile
            GiveSpecialToPlayer(specialOnDeath, projectile);

            // Play death sound
            if (deathSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(deathSound);
            }

            // Call virtual death method for custom behavior
            OnDeath();
        }
        else
        {
        GiveSpecialToPlayer(specialOnHit, projectile);

        if (hurtSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hurtSound);
        }
        }
    }

    protected virtual IEnumerator StaggerRoutine()
    {
        isStaggered = true;
        
        if (staggerAnimation != null && animator != null)
        {
            animator.Play(staggerAnimation.name);
        }
        
        yield return new WaitForSeconds(staggerDuration);
        
        if (defaultAnimation != null && animator != null)
        {
            animator.Play(defaultAnimation.name);
        }
        
        isStaggered = false;
    }

    public virtual void ApplyPoison(int damage, float duration, float tickRate, GameObject sourceProjectile = null)
    {
        // Stop existing poison coroutine if running
        if (poisonCoroutine != null)
        {
            StopCoroutine(poisonCoroutine);
        }

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.poisoned);
        
        // Set poison parameters (new poison resets timer)
        poisonDamage = damage;
        poisonTimer = duration;
        poisonTickRate = tickRate;
        lastPoisonTick = 0f;
        
        poisonSourceProjectile = sourceProjectile; // For special rewards
        
        poisonCoroutine = StartCoroutine(PoisonRoutine());
    }

    protected virtual IEnumerator PoisonRoutine()
    {
        isPoisoned = true;
        
        yield return new WaitForSeconds(0.4f); // Wait for red dmg glow to end
        
        float remainingDuration = poisonTimer - 0.4f;
        if (remainingDuration > 0f)
        {
            glowManager?.StartGlow(new Color(0.3f, 0.5f, 0.13f), remainingDuration, 5f, 0.75f); // Poison glow
        }
        
        while (poisonTimer > 0f)
        {
            if (lastPoisonTick >= poisonTickRate)
            {
                health -= poisonDamage;
                ShowDamageText(poisonDamage, new Color(0.7f, 0.9f, 0.5f)); // Pale green text
                GiveSpecialToPlayer(specialOnHit, poisonSourceProjectile);
                lastPoisonTick = 0f;
                if (health <= 0)
                {
                    GiveSpecialToPlayer(specialOnDeath, poisonSourceProjectile);
                    AudioManager.Instance.PlaySFX(deathSound);
                    OnDeath();
                    yield break;
                }
            }
            
            // Update timers
            lastPoisonTick += Time.deltaTime;
            poisonTimer -= Time.deltaTime;
            
            yield return null;
        }
        
        isPoisoned = false;
        poisonCoroutine = null;
        poisonSourceProjectile = null;
    }

    protected virtual void ShowDamageText(int damage, Color textColor = default)
    {
        Debug.Log($"ShowDamageText called with damage: {damage}, color: {textColor}");
        
        // Use red as default color if no color specified
        if (textColor == default)
        {
            textColor = Color.red;
            Debug.Log("Using default red color");
        }
        else
        {
            Debug.Log($"Using specified color: {textColor}");
        }
        
        if (damageTextPrefab != null)
        {
            // Calculate position based on sprite height
            float spriteHeight = spriteRenderer != null ? spriteRenderer.bounds.size.y : 1f;
            Vector3 adjustedOffset = damageTextOffset + new Vector3(0, spriteHeight, 0);
            Vector3 spawnPosition = transform.position + adjustedOffset;
            
            GameObject damageTextObj = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);
            
            // Try to get the DamageText component and set the damage value and color
            var damageText = damageTextObj.GetComponent<DamageText>();
            if (damageText != null)
            {
                damageText.Initialize(damage, textColor);
                Debug.Log($"Initialized DamageText with color: {textColor}");
            }
            else
            {
                Debug.LogWarning("DamageText component not found on prefab!");
                
                // Fallback: Set the color directly on text components
                var textMesh = damageTextObj.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    textMesh.color = textColor;
                    Debug.Log($"Set TextMesh color to: {textColor}");
                }
                else
                {
                    // Try TMPro Text component if TextMesh isn't found
                    var tmpText = damageTextObj.GetComponent<TMPro.TextMeshPro>();
                    if (tmpText != null)
                    {
                        tmpText.color = textColor;
                        Debug.Log($"Set TMPro color to: {textColor}");
                    }
                    else
                    {
                        Debug.LogWarning("Neither TextMesh nor TextMeshPro component found on damage text prefab!");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("damageTextPrefab is null! Make sure to assign it in the inspector.");
        }
    }

    protected virtual void OnDeath()
    {
        // Unregister this enemy from wave tracking before destroying
        BaseWave.UnregisterEnemy(gameObject);
        
        // Default behavior: destroy the game object
        Destroy(gameObject);
    }

    // Extension points for custom damage handling
    protected virtual void OnBeforeDamageApplied(int damage, GameObject projectile)
    {
        // Default: no custom behavior
    }

    protected virtual void OnAfterDamageApplied(int damage, GameObject projectile)
    {
        // Default: no custom behavior
    }

    protected void GiveSpecialToPlayer(int amount, GameObject projectile)
    {
        if (projectile == null) return;

        GameObject player = projectile.CompareTag("PlayerLeftProjectile")
            ? GameObject.FindWithTag("PlayerLeft")
            : GameObject.FindWithTag("PlayerRight");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(amount);
            }
        }
    }

    public virtual bool HasAttribute(EnemyType attr)
    {
        return (attributes & attr) == attr;
    }

    // Virtual method for calculating collision damage (can be overridden by enemies like slime)
    protected virtual int GetShieldCollisionDamage()
    {
        return shieldDamage;
    }

    protected virtual int GetPlayerCollisionDamage()
    {
        return playerDamage;
    }

    // Virtual method for additional collision handling (can be overridden by specific enemies)
    protected virtual void OnAdditionalCollision(Collider2D other)
    {
        // Default: no additional collision behavior
    }

    // Virtual method for updating sprite direction based on movement
    protected virtual void UpdateSpriteDirection(Vector3 moveDirection)
    {
        if (spriteRenderer != null)
        {
            // Flip sprite based on horizontal movement direction with threshold
            if (moveDirection.x < -directionThreshold)
                spriteRenderer.flipX = true;  // Moving left
            else if (moveDirection.x > directionThreshold)
                spriteRenderer.flipX = false; // Moving right
        }
    }

    // Overload for updating sprite direction based on current position and target
    protected virtual void UpdateSpriteDirection(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector3 moveDirection = (targetPosition - currentPosition).normalized;
        UpdateSpriteDirection(moveDirection);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            // Damage is handled in PlayerProjectile, just destroy the projectile
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
            PlayerHealth playerHealth = other.transform.parent?.GetComponent<PlayerHealth>();
            if (playerHealth != null) 
            {
                playerHealth.TakeDamage(GetShieldCollisionDamage());
            }
            Destroy(gameObject);
        }
        else if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyPlayer);
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null) 
            {
                playerHealth.TakeDamage(GetPlayerCollisionDamage());
            }
            Destroy(gameObject);
        }
        else
        {
            // Allow derived classes to handle additional collision types
            OnAdditionalCollision(other);
        }
    }

    // Public getter for health (useful for UI or other systems)
    public float GetHealth() => health;
    
    // Public getter for max health (if needed)
    public virtual float GetMaxHealth() => health;
}

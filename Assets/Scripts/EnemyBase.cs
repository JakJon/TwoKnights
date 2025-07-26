using UnityEngine;
using System.Collections;

public abstract class EnemyBase : MonoBehaviour, IHasAttributes
{
    [Header("Health")]
    [SerializeField] protected float health = 10f;

    [Header("Special Rewards")]
    [Tooltip("Special points given to player when this enemy takes damage")]
    [SerializeField] protected int specialOnHit = 5;
    [Tooltip("Special points given to player when this enemy dies")]
    [SerializeField] protected int specialOnDeath = 10;

    [Header("Collision Damage")]
    [Tooltip("Damage dealt to player when hitting their shield")]
    [SerializeField] protected int shieldDamage = 10;
    [Tooltip("Damage dealt to player on direct contact")]
    protected int playerDamage = 20;

    [Header("Stagger")]
    [Tooltip("Duration in seconds that enemy is stunned when taking damage")]
    [SerializeField] protected float staggerDuration = 0.2f;
    [Tooltip("Animation clip to play while staggered")]
    [SerializeField] protected AnimationClip staggerAnimation;
    [Tooltip("Default animation to return to after stagger")]
    [SerializeField] protected AnimationClip defaultAnimation;

    [Header("Audio")]
    [Tooltip("Sound played when enemy takes damage")]
    [SerializeField] protected SoundEffect hurtSound;
    [Tooltip("Sound played when enemy dies")]
    [SerializeField] protected SoundEffect deathSound;

    [Header("Enemy Attributes")]
    [Tooltip("Type attributes of this enemy")]
    [SerializeField] protected EnemyType attributes;

    [Header("Sprite Flipping")]
    [Tooltip("Threshold for sprite direction changes to prevent flickering")]
    [SerializeField] protected float directionThreshold = 0.01f;

    // Protected components that enemies commonly use
    protected SpriteRenderer spriteRenderer;
    protected GlowManager glowManager; // Cache the GlowManager component
    protected Animator animator; // Cache the Animator component
    
    // Stagger system
    protected bool isStaggered = false;
    protected AnimationClip originalAnimationClip; // Store original animation during stagger
    public bool IsStaggered => isStaggered;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glowManager = GetComponent<GlowManager>(); // Cache the component
        animator = GetComponent<Animator>(); // Cache the animator
    }

    public virtual void TakeDamage(int damage, GameObject projectile)
    {
        // Use cached component instead of GetComponent call
        glowManager?.StartGlow(Color.red, 0.3f);
        health -= damage;
        
        // Trigger stagger effect (only if enemy survives)
        if (health > 0)
        {
            StartCoroutine(StaggerRoutine());
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
            // Give hit special to the player who fired the projectile
            GiveSpecialToPlayer(specialOnHit, projectile);

            // Play hurt sound
            if (hurtSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(hurtSound);
            }
        }
    }

    protected virtual IEnumerator StaggerRoutine()
    {
        isStaggered = true;
        
        // Play stagger animation if available
        if (staggerAnimation != null && animator != null)
        {
            animator.Play(staggerAnimation.name);
        }
        
        yield return new WaitForSeconds(staggerDuration);
        
        // Return to default animation
        if (defaultAnimation != null && animator != null)
        {
            animator.Play(defaultAnimation.name);
        }
        
        isStaggered = false;
    }

    protected virtual void OnDeath()
    {
        // Default behavior: destroy the game object
        Destroy(gameObject);
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

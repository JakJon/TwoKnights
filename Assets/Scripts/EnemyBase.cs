using UnityEngine;

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
    [SerializeField] protected int playerDamage = 20;

    [Header("Audio")]
    [Tooltip("Sound played when enemy takes damage")]
    [SerializeField] protected SoundEffect hurtSound;
    [Tooltip("Sound played when enemy dies")]
    [SerializeField] protected SoundEffect deathSound;

    [Header("Enemy Attributes")]
    [Tooltip("Type attributes of this enemy")]
    [SerializeField] protected EnemyType attributes;

    public virtual void TakeDamage(int damage, GameObject projectile)
    {
        health -= damage;

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

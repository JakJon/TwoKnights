using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] protected float damageTextStackSeparation = 0.25f; // Vertical spacing between stacked texts

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
    // Death guard to avoid double-kill/race conditions
    protected bool isDead = false;
    // Poison system
    protected bool isPoisoned = false;
    protected float poisonTimer = 0f;
    protected int poisonDamage = 0;
    protected float poisonTickRate = 1f;
    protected float lastPoisonTick = 0f;
    protected Coroutine poisonCoroutine = null;
    protected PoisonBubbleEffect poisonBubbles = null; // Poison bubble effect
    
    // Track poison sources and their contributions
    protected List<PoisonSource> poisonSources = new List<PoisonSource>();
    
    [System.Serializable]
    public class PoisonSource
    {
        public string playerTag; // Store player tag instead of projectile reference
        public int damageContribution; //
        
        public PoisonSource(string playerTag, int damage)
        {
            this.playerTag = playerTag;
            damageContribution = damage;
        }
    }
    
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
        // Ignore any damage once death has been triggered
        if (isDead) return;
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
            // Mark as dead immediately to prevent re-entrancy in the same frame
            isDead = true;
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
        // Add or update poison source
        if (sourceProjectile != null)
        {
            // Extract player tag from projectile tag
            string playerTag = sourceProjectile.CompareTag("PlayerLeftProjectile") ? "PlayerLeft" : "PlayerRight";
            
            var existingSource = poisonSources.Find(ps => ps.playerTag == playerTag);
            if (existingSource != null)
            {
                existingSource.damageContribution += damage; // Stack damage from same source
            }
            else
            {
                poisonSources.Add(new PoisonSource(playerTag, damage)); // Add new source
            }
        }
        
        // Stack poison damage and reset timer
        poisonDamage += damage;
        poisonTimer = duration; // Reset timer to new duration
        poisonTickRate = tickRate; // Use latest tick rate
        lastPoisonTick = 0f;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.poisoned);
        
        // Create poison bubbles if not already created
        if (poisonBubbles == null)
        {
            GameObject bubbleObject;
            GameObject bubblePrefab = PoisonResourceManager.Instance?.GetPoisonBubblePrefab();
            
            if (bubblePrefab != null)
            {
                // Use prefab from resource manager
                bubbleObject = Instantiate(bubblePrefab, transform.position, Quaternion.identity);
                bubbleObject.transform.SetParent(transform);
                bubbleObject.transform.localPosition = Vector3.zero;
                poisonBubbles = bubbleObject.GetComponent<PoisonBubbleEffect>();
                
                // If prefab doesn't have the component, add it
                if (poisonBubbles == null)
                {
                    poisonBubbles = bubbleObject.AddComponent<PoisonBubbleEffect>();
                }
            }
            else
            {
                // Fallback: create dynamically and try to get sprite from resource manager
                bubbleObject = new GameObject("PoisonBubbles");
                bubbleObject.transform.SetParent(transform);
                bubbleObject.transform.localPosition = Vector3.zero;
                poisonBubbles = bubbleObject.AddComponent<PoisonBubbleEffect>();
                
                // Try to set sprite from resource manager
                Sprite bubbleSprite = PoisonResourceManager.Instance?.GetPoisonBubbleSprite();
                if (bubbleSprite != null)
                {
                    poisonBubbles.SetBubbleSprite(bubbleSprite);
                }
            }
        }
        
        // Start poison coroutine if not already running
        if (poisonCoroutine == null)
        {
            poisonCoroutine = StartCoroutine(PoisonRoutine());
        }
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
        
        // Start poison bubbles
        poisonBubbles?.StartBubbles();
        
        while (poisonTimer > 0f)
        {
            if (lastPoisonTick >= poisonTickRate)
            {
                health -= poisonDamage;
                ShowDamageText(poisonDamage, new Color(0.7f, 0.9f, 0.5f)); // Pale green text
                
                // Don't give special points for poison ticks - only for kills
                
                lastPoisonTick = 0f;
                if (health <= 0)
                {
                    if (isDead)
                    {
                        // Already handled by another damage source
                        yield break;
                    }
                    isDead = true;
                    Debug.Log($"Enemy died from poison! Poison sources count: {poisonSources.Count}");
                    
                    // Stop poison bubbles but let them finish their animation
                    poisonBubbles?.StopBubblesAndDetach();
                    
                    // Give death special to all contributors
                    foreach (var poisonSource in poisonSources)
                    {
                        if (!string.IsNullOrEmpty(poisonSource.playerTag))
                        {
                            Debug.Log($"Giving death special to poison contributor with player tag: {poisonSource.playerTag}");
                            GiveSpecialToPlayer(specialOnDeath, poisonSource.playerTag);
                        }
                        else
                        {
                            Debug.LogWarning("Poison source player tag is null or empty!");
                        }
                    }
                    
                    if (deathSound != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(deathSound);
                    }
                    OnDeath();
                    yield break;
                }
            }
            
            // Update timers
            lastPoisonTick += Time.deltaTime;
            poisonTimer -= Time.deltaTime;
            
            yield return null;
        }
        
        // Stop poison bubbles when effect ends but let them finish their animation
        poisonBubbles?.StopBubblesAndDetach();
        
        // Poison effect ended - clear all data
        isPoisoned = false;
        poisonCoroutine = null;
        poisonSources.Clear();
        poisonDamage = 0;
    }

    protected virtual void ShowDamageText(int damage, Color textColor = default)
    {        
        // Use red as default color if no color specified
        if (textColor == default)
        {
            textColor = Color.red;
        }
        if (damageTextPrefab != null)
        {
            // Calculate position based on sprite height
            float spriteHeight = spriteRenderer != null ? spriteRenderer.bounds.size.y : 1f;
            Vector3 adjustedOffset = damageTextOffset + new Vector3(0, spriteHeight, 0);
            Vector3 spawnPosition = transform.position + adjustedOffset;
            
            // Before spawning the new text, nudge existing texts on this enemy upward so they stack
            // Look for DamageText components under this enemy
        var existingTexts = GetComponentsInChildren<DamageText>(includeInactive: false);
            if (existingTexts != null && existingTexts.Length > 0)
            {
                foreach (var dt in existingTexts)
                {
                    // Push each existing damage text up by one slot
            dt.PushUp(damageTextStackSeparation);
                }
            }
            
            GameObject damageTextObj = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);
            // Parent to this enemy so subsequent spawns can find and stack
            damageTextObj.transform.SetParent(transform);
            
            // Try to get the DamageText component and set the damage value and color
            var damageText = damageTextObj.GetComponent<DamageText>();
            if (damageText != null)
            {
                damageText.Initialize(damage, textColor);
            }
            else
            {
                
                // Fallback: Set the color directly on text components
                var textMesh = damageTextObj.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    textMesh.color = textColor;
                }
                else
                {
                    // Try TMPro Text component if TextMesh isn't found
                    var tmpText = damageTextObj.GetComponent<TMPro.TextMeshPro>();
                    if (tmpText != null)
                    {
                        tmpText.color = textColor;
                    }
                }
            }
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
        if (projectile == null)
        {
            Debug.LogWarning("GiveSpecialToPlayer: projectile is null!");
            return;
        }


        GameObject player = projectile.CompareTag("PlayerLeftProjectile")
            ? GameObject.FindWithTag("PlayerLeft")
            : GameObject.FindWithTag("PlayerRight");

        Debug.Log($"GiveSpecialToPlayer: found player = {(player != null ? player.name : "null")}");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                Debug.Log($"GiveSpecialToPlayer: Giving {amount} special to {player.name}");
                playerSpecial.updateSpecial(amount);
            }
            else
            {
                Debug.LogWarning($"GiveSpecialToPlayer: PlayerSpecial component not found on {player.name}");
            }
        }
        else
        {
            Debug.LogWarning($"GiveSpecialToPlayer: No player found for projectile tag {projectile.tag}");
        }
    }

    // Overloaded version that accepts a player tag directly
    protected void GiveSpecialToPlayer(int amount, string playerTag)
    {
        if (string.IsNullOrEmpty(playerTag))
        {
            Debug.LogWarning("GiveSpecialToPlayer: playerTag is null or empty!");
            return;
        }

        GameObject player = GameObject.FindWithTag(playerTag);
        Debug.Log($"GiveSpecialToPlayer: found player = {(player != null ? player.name : "null")} for tag {playerTag}");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                Debug.Log($"GiveSpecialToPlayer: Giving {amount} special to {player.name}");
                playerSpecial.updateSpecial(amount);
            }
            else
            {
                Debug.LogWarning($"GiveSpecialToPlayer: PlayerSpecial component not found on {player.name}");
            }
        }
        else
        {
            Debug.LogWarning($"GiveSpecialToPlayer: No player found for tag {playerTag}");
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
    if (isDead) return; // Ignore collisions after death triggered
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

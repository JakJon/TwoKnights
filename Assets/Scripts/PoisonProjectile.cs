using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("Poison Settings")]
    [Tooltip("Damage dealt per poison tick")]
    [SerializeField] private int poisonDamage = 3;
    [Tooltip("Duration of poison effect in seconds")]
    [SerializeField] private float poisonDuration = 20f;
    [Tooltip("Time between poison damage ticks in seconds")]
    [SerializeField] private float poisonTickRate = 1f;
    
    // Visual indicator for poisoned projectiles
    [Header("Visual Effects")]
    [Tooltip("Color tint for poisoned projectiles")]
    [SerializeField] private Color poisonTint = Color.green;
    [Tooltip("Glow effect for poisoned projectiles")]
    [SerializeField] private bool enableGlow = true;
    [Tooltip("Prefab to use for poison bubble trail effects")]
    [SerializeField] private GameObject poisonBubblePrefab;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private GlowManager glowManager;
    private PoisonBubbleEffect poisonBubbles;
    
    public int PoisonDamage => poisonDamage;
    public float PoisonDuration => poisonDuration;
    public float PoisonTickRate => poisonTickRate;
    
    private void Start()
    {
        // Get components
        spriteRenderer = GetComponent<SpriteRenderer>();
        glowManager = GetComponent<GlowManager>();
        
        // Store original color and apply poison tint
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.Lerp(originalColor, poisonTint, 0.3f);
        }
        
        // Add glow effect if enabled
        if (enableGlow && glowManager != null)
        {
            glowManager.StartGlow(Color.blue, 10f, 5f, 0.4f); // Long duration, medium speed waves
        }
        
        // Create and start poison bubble trail
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
            bubbleObject = new GameObject("PoisonBubbleTrail");
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
        
        // Configure bubbles for projectile trail (faster rate, shorter lifetime)
        float bubbleRate = PoisonResourceManager.Instance?.projectileBubbleRate ?? 5f;
        poisonBubbles.SetBubbleRate(bubbleRate);
        poisonBubbles.StartBubbles();
    }
    
    // Method to apply poison to an enemy
    public void ApplyPoisonToEnemy(EnemyBase enemy, GameObject sourceProjectile)
    {
        if (enemy != null)
        {
            enemy.ApplyPoison(poisonDamage, poisonDuration, poisonTickRate, sourceProjectile);
        }
    }
    
    private void OnDestroy()
    {
        // Stop bubbles but let them finish their animation when projectile is destroyed
        if (poisonBubbles != null)
        {
            
            // Alternative approach: Create a completely independent bubble GameObject
            GameObject independentBubbles = new GameObject("IndependentPoisonBubbles");
            independentBubbles.transform.position = transform.position;
            
            // Copy the particle system to the independent object
            var newBubbleEffect = independentBubbles.AddComponent<PoisonBubbleEffect>();
            
            // Copy the sprite from the current bubble effect
            Sprite bubbleSprite = PoisonResourceManager.Instance?.GetPoisonBubbleSprite();
            if (bubbleSprite != null)
            {
                newBubbleEffect.SetBubbleSprite(bubbleSprite);
            }
            
            // Start the independent bubbles and immediately stop emission (let existing ones finish)
            newBubbleEffect.StartBubbles();
            newBubbleEffect.StopBubbles();
            
            // Destroy the independent bubbles after their lifetime
            float bubbleLifetime = 2f; // Default bubble lifetime
            Destroy(independentBubbles, bubbleLifetime + 1f);
            
            
            // Also try the original method
            poisonBubbles.StopBubblesAndDetach();
        }
        else
        {
        }
    }
}

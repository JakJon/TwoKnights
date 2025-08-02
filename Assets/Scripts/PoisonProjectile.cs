using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("Poison Settings")]
    [Tooltip("Damage dealt per poison tick")]
    [SerializeField] private int poisonDamage = 3;
    [Tooltip("Duration of poison effect in seconds")]
    [SerializeField] private float poisonDuration = 5f;
    
    // Visual indicator for poisoned projectiles
    [Header("Visual Effects")]
    [Tooltip("Color tint for poisoned projectiles")]
    [SerializeField] private Color poisonTint = Color.green;
    [Tooltip("Glow effect for poisoned projectiles")]
    [SerializeField] private bool enableGlow = true;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private GlowManager glowManager;
    
    public int PoisonDamage => poisonDamage;
    public float PoisonDuration => poisonDuration;
    
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
            glowManager.StartGlow(Color.green, 10f, 5f, 0.4f); // Long duration, medium speed waves
        }
    }
    
    // Method to apply poison to an enemy
    public void ApplyPoisonToEnemy(EnemyBase enemy)
    {
        if (enemy != null)
        {
            enemy.ApplyPoison(poisonDamage, poisonDuration);
        }
    }
}

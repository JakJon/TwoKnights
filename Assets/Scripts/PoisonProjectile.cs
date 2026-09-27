using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("Poison Settings")]
    // Poison is the SLOW half of the DoT pair: it ticks lighter than fire and runs
    // far longer. A rat is on the field ~18s (15s of patrol before it even chases)
    // and a wolf 8-14s on its route, so a 30s debt overshoots what any trash mob
    // needs on purpose — the surplus is only ever collected by something with the
    // health to outlast it. Fire is sized to the approach; poison is sized to a boss.
    [Tooltip("Damage dealt per poison tick")]
    [SerializeField] private int poisonDamage = 2;
    [Tooltip("Duration of poison effect in seconds")]
    [SerializeField] private float poisonDuration = 30f;
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

    // The trail. `trailSeconds` is 0 for any poisoned arrow whose owner has no
    // Venom Tip rank — an arrow that picked its venom up flying through a cloud,
    // for instance — and 0 means the arrow simply does not shed.
    private float trailSeconds;
    private Vector3 lastBeadAt;
    private bool trailStarted;
    private int beadsShed;
    private string ownerTag;
    
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
    
    // Bend the serialized defaults by the firing knight's poison tick bonus
    // (equipment), and take the trail's lifetime off the same sheet (Venom Tip).
    // Called right after AddComponent, before Start.
    //
    // shedsTrail is false for echoes - shadow arrows and shurikens (owner's call,
    // 2026-09-25). They still poison what they hit; they just lay no beads, so
    // the trail stays a line the knight's own shot drew rather than a fan of them
    // covering the board.
    public void ConfigureFromBoost(PoisonTipBoost boost, bool shedsTrail = true)
    {
        if (boost == null) return;
        poisonDamage += boost.TickDamageBonus;
        trailSeconds = shedsTrail ? boost.TrailBubbleSeconds : 0f;
        ownerTag = boost.gameObject.tag;
    }

    // Every three units of flight, a bead of venom is left where the arrow was.
    //
    // Measured in DISTANCE rather than on a timer, and that is the whole point:
    // the spacing is then a property of the line the shot drew, so a slow arrow
    // and a fast one lay the same lane and the player is reading geometry rather
    // than reading the fire-rate upgrades they happen to own.
    //
    // The first bead is not dropped at the shield. The arrow has to have gone its
    // three units first, or every shot would garnish the knight's own feet with
    // venom that nothing is ever going to walk into.
    private void Update()
    {
        if (trailSeconds <= 0f) return;

        if (!trailStarted)
        {
            trailStarted = true;
            lastBeadAt = transform.position;
            return;
        }

        float step = PoisonTrailBubble.DropEveryUnits;

        // A while, not an if. A fast arrow under a low frame rate can cover two
        // steps between frames, and a lane with holes in it where the game
        // stuttered is a lane the player cannot trust.
        while ((transform.position - lastBeadAt).sqrMagnitude >= step * step)
        {
            Vector3 along = (transform.position - lastBeadAt).normalized * step;
            lastBeadAt += along;
            PoisonTrailBubble.Drop(lastBeadAt, trailSeconds, poisonDamage,
                                   poisonDuration, poisonTickRate, ownerTag, beadsShed++);
        }
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

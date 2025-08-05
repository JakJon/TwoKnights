using UnityEngine;

public class PoisonBubbleEffect : MonoBehaviour
{
    [Header("Bubble Settings")]
    [Tooltip("Poison bubble sprite to use for particles")]
    [SerializeField] private Sprite bubbleSprite;
    [Tooltip("Alternative: Use a pre-made bubble prefab instead of creating dynamically")]
    [SerializeField] private GameObject bubblePrefab;
    [Tooltip("Number of bubbles to spawn per second")]
    [SerializeField] private float bubbleRate = 2f;
    [Tooltip("Lifetime of each bubble in seconds")]
    [SerializeField] private float bubbleLifetime = 2f;
    [Tooltip("Speed bubbles float upward")]
    [SerializeField] private float bubbleSpeed = 1f;
    [Tooltip("Random spread for bubble movement")]
    [SerializeField] private float bubbleSpread = 0.5f;
    
    [Header("Visual Settings")]
    [Tooltip("Size range for bubbles (min, max)")]
    [SerializeField] private Vector2 bubbleSizeRange = new Vector2(0.1f, 0.3f);
    [Tooltip("Color tint for bubbles")]
    [SerializeField] private Color bubbleColor = new Color(0.3f, 0.8f, 0.3f, 0.7f);
    
    private ParticleSystem particles;
    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.ShapeModule shapeModule;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private ParticleSystem.SizeOverLifetimeModule sizeModule;
    private ParticleSystem.ColorOverLifetimeModule colorModule;
    private ParticleSystem.TextureSheetAnimationModule textureModule;
    
    private void Awake()
    {
        // Create particle system if it doesn't exist
        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = gameObject.AddComponent<ParticleSystem>();
        }
        
        SetupParticleSystem();
    }
    
    private void SetupParticleSystem()
    {
        // Get modules
        mainModule = particles.main;
        emissionModule = particles.emission;
        shapeModule = particles.shape;
        velocityModule = particles.velocityOverLifetime;
        sizeModule = particles.sizeOverLifetime;
        colorModule = particles.colorOverLifetime;
        textureModule = particles.textureSheetAnimation;
        
        // Main settings
        mainModule.startLifetime = bubbleLifetime;
        mainModule.startSpeed = 0f; // We'll handle movement with velocity module
        mainModule.startSize3D = false; // Use uniform scaling to maintain aspect ratio
        mainModule.startSize = new ParticleSystem.MinMaxCurve(bubbleSizeRange.x, bubbleSizeRange.y);
        mainModule.startColor = bubbleColor;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        
        // Emission settings
        emissionModule.rateOverTime = bubbleRate;
        
        // Shape settings (small circle for spawn area)
        shapeModule.enabled = true;
        shapeModule.shapeType = ParticleSystemShapeType.Circle;
        shapeModule.radius = 0.2f;
        
        // Velocity settings (bubbles float up with some randomness)
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;
        velocityModule.y = new ParticleSystem.MinMaxCurve(bubbleSpeed * 0.7f, bubbleSpeed);
        velocityModule.x = new ParticleSystem.MinMaxCurve(-bubbleSpread, bubbleSpread);
        
        // Size over lifetime (bubbles shrink as they fade)
        sizeModule.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Color over lifetime (fade out)
        colorModule.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(bubbleColor, 0f), new GradientColorKey(bubbleColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(bubbleColor.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorModule.color = colorGradient;
        
        // Set custom sprite if provided
        if (bubbleSprite != null)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                // Create a material using the sprite's texture
                Material spriteMaterial = new Material(Shader.Find("Sprites/Default"));
                spriteMaterial.mainTexture = bubbleSprite.texture;
                
                // Set the material to the renderer
                renderer.material = spriteMaterial;
                
                // Configure texture sheet animation for sprite
                textureModule.enabled = false; // Disable for single sprite
            }
        }
        else
        {
            // If no sprite provided, use default particle material
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }
    
    public void StartBubbles()
    {
        if (particles != null)
        {
            particles.Play();
        }
    }
    
    public void StopBubbles()
    {
        if (particles != null)
        {
            particles.Stop();
        }
    }
    
    public void SetBubbleRate(float rate)
    {
        bubbleRate = rate;
        if (particles != null)
        {
            var emission = particles.emission;
            emission.rateOverTime = rate;
        }
    }
    
    public void SetBubbleSprite(Sprite sprite)
    {
        bubbleSprite = sprite;
        if (particles != null)
        {
            SetupParticleSystem(); // Re-setup with new sprite
        }
    }
    
    public bool IsPlaying => particles != null && particles.isPlaying;
}

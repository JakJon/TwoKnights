using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class SwordSwing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference swordSwingAction;
    
    [Header("Settings")]
    [SerializeField] private float swingDuration = 0.15f; // 300ms to complete swing (increased for visibility)
    [SerializeField] private float totalEffectDuration = 0.3f; // 600ms total visibility (increased)
    [SerializeField] private float swingAngleRange = 60f; // 30 degrees each direction (reduced from 90)
    [SerializeField] private int swingDamage = 10;
    [SerializeField] private float cooldownTime = 1f;
    
    private InputAction swordInputAction;
    private bool canSwing = true;
    private Transform swordSpriteTransform;
    private Transform slashSpriteTransform;
    private HashSet<GameObject> damagedEnemies; // Track enemies that have already been damaged this swing
    private ShieldOrbit shield; // Found automatically
    
    void Awake()
    {
        Debug.Log("SwordSwing: Awake called!");
        
        // Find the sword and slash sprites as direct children
        swordSpriteTransform = transform.Find("Sword");
        slashSpriteTransform = transform.Find("Slash");
        
        if (swordSpriteTransform == null)
        {
            Debug.LogError("SwordSwing: Could not find 'Sword' child GameObject!");
        }
        else
        {
            Debug.Log("SwordSwing: Found Sword child GameObject successfully!");
        }
        
        if (slashSpriteTransform == null)
        {
            Debug.LogError("SwordSwing: Could not find 'Slash' child GameObject!");
        }
        else
        {
            Debug.Log("SwordSwing: Found Slash child GameObject successfully!");
        }
        
        // Find the shield component from parent or siblings
        shield = GetComponentInParent<ShieldOrbit>();
        if (shield == null)
        {
            // Try to find it in the parent's children (siblings)
            if (transform.parent != null)
            {
                shield = transform.parent.GetComponentInChildren<ShieldOrbit>();
            }
        }
        
        if (shield == null)
        {
            Debug.LogError("SwordSwing: Could not find ShieldOrbit component in parent or siblings!");
        }
        else
        {
            Debug.Log("SwordSwing: Found ShieldOrbit component successfully!");
        }
        
        if (swordSwingAction != null)
        {
            swordInputAction = swordSwingAction.action;
            swordInputAction.Enable();
            Debug.Log("SwordSwing: Input action enabled successfully!");
        }
        else
        {
            Debug.LogError("SwordSwing: swordSwingAction is null! Please assign it in the inspector.");
        }
        
        // Add damage detection components to both sprites (they start inactive)
        AddDamageDetection(swordSpriteTransform);
        AddDamageDetection(slashSpriteTransform);
    }
    
    void Update()
    {
        // Check for input
        if (canSwing && shield != null && swordInputAction != null && swordInputAction.WasPressedThisFrame())
        {
            Debug.Log("SwordSwing: Input detected! Starting sword swing...");
            StartCoroutine(PerformSwordSwing());
        }
    }
    
    private IEnumerator PerformSwordSwing()
    {
        Debug.Log("SwordSwing: PerformSwordSwing started!");
        canSwing = false;
        
        // Initialize damage tracking for this swing
        damagedEnemies = new HashSet<GameObject>();
        
        // Check if we have the required components
        if (swordSpriteTransform == null || slashSpriteTransform == null)
        {
            Debug.LogError("SwordSwing: Missing sword or slash sprite transforms!");
            canSwing = true;
            yield break;
        }
        
        // Calculate angles based on shield position
        float shieldAngle = shield.CurrentAngle;
        Debug.Log($"SwordSwing: Shield angle: {shieldAngle}");
        float startAngle = shieldAngle - 45f; // 45 degrees counter-clockwise from shield
        float endAngle = shieldAngle + 45f; // 45 degrees clockwise from shield
        
        // Enable both sprites
        swordSpriteTransform.gameObject.SetActive(true);
        slashSpriteTransform.gameObject.SetActive(true);
        Debug.Log("SwordSwing: Sword and slash sprites activated");
        
        // Position and rotate slash sprite to match shield angle (stationary)
        float shieldAngleRad = shieldAngle * Mathf.Deg2Rad;
        Vector3 slashPosition = new Vector3(Mathf.Cos(shieldAngleRad), Mathf.Sin(shieldAngleRad), 0) * 0.8f;
        slashSpriteTransform.localPosition = slashPosition;
        slashSpriteTransform.localRotation = Quaternion.Euler(0, 0, shieldAngle - 90f); // Subtract 90 degrees to be parallel with shield
        
        // Position and rotate sword sprite at starting angle
        float startAngleRad = startAngle * Mathf.Deg2Rad;
        Vector3 swordStartPos = new Vector3(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad), 0) * 1f;
        swordSpriteTransform.localPosition = swordStartPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, startAngle - 90f); // Subtract 90 degrees so sword points outward
        
        // Perform swing animation over the duration
        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / swingDuration;
            
            // Interpolate angle from start to end for sword only
            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
            float currentAngleRad = currentAngle * Mathf.Deg2Rad;
            
            // Update only the sword sprite position and rotation
            Vector3 swordCurrentPos = new Vector3(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad), 0) * 1f;
            swordSpriteTransform.localPosition = swordCurrentPos;
            swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, currentAngle - 90f); // Subtract 90 degrees so sword points outward
            
            // Slash sprite stays in place (no changes needed)
            
            Debug.Log($"SwordSwing: Progress: {progress:F2}, Sword Angle: {currentAngle:F1}, Slash stays at: {shieldAngle:F1}");
            
            yield return null;
        }
        
        // Ensure sword ends at the exact end angle
        float endAngleRad = endAngle * Mathf.Deg2Rad;
        Vector3 swordEndPos = new Vector3(Mathf.Cos(endAngleRad), Mathf.Sin(endAngleRad), 0) * 1f;
        swordSpriteTransform.localPosition = swordEndPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, endAngle - 90f); // Subtract 90 degrees so sword points outward
        
        // Wait for remaining effect duration (200ms more for total 400ms)
        yield return new WaitForSeconds(totalEffectDuration - swingDuration);
        
        // Disable both sprites
        swordSpriteTransform.gameObject.SetActive(false);
        slashSpriteTransform.gameObject.SetActive(false);
        Debug.Log("SwordSwing: Sword and slash sprites deactivated");
        
        // Start cooldown
        yield return new WaitForSeconds(cooldownTime);
        
        canSwing = true;
    }
    
    private void AddDamageDetection(Transform spriteTransform)
    {
        if (spriteTransform == null) return;
        
        // Add collider if it doesn't exist
        Collider2D collider = spriteTransform.GetComponent<Collider2D>();
        if (collider == null)
        {
            collider = spriteTransform.gameObject.AddComponent<BoxCollider2D>();
        }
        collider.isTrigger = true;
        
        // Add damage detection component
        SwordDamageDetector damageDetector = spriteTransform.gameObject.AddComponent<SwordDamageDetector>();
        damageDetector.Initialize(this, swingDamage);
    }
    
    public void OnEnemyHit(GameObject enemy)
    {
        // Check if enemy was already damaged this swing
        if (damagedEnemies.Contains(enemy)) return;
        
        // Try to damage the enemy
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            // Create a temporary projectile GameObject to satisfy the damage system
            GameObject tempProjectile = new GameObject("SwordHit");
            tempProjectile.tag = gameObject.tag + "Projectile";
            
            enemyBase.TakeDamage(swingDamage, tempProjectile);
            
            // Clean up temporary projectile
            Destroy(tempProjectile);
            
            // Mark this enemy as damaged
            damagedEnemies.Add(enemy);
        }
    }
    
    void OnDestroy()
    {
        if (swordInputAction != null)
        {
            swordInputAction.Disable();
        }
    }
}

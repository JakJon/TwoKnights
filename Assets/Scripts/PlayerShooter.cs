using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playerProjectilePrefab;
    [SerializeField] private ShieldOrbit shield;

    [Header("Settings")]
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private InputActionReference shootAction;
    
    private bool _canShoot = true;
    private bool _isHoldingButton = false;
    
    [Header("Upgrades")]
    private int damageBonus = 0; // Total damage bonus from upgrades

    public float cooldownTime = 1.5f;
    public bool rapidFireEnabled = false;

    private void OnEnable()
    {
        shootAction.action.performed += _ => _isHoldingButton = true;
        shootAction.action.canceled += _ => _isHoldingButton = false;
        shootAction.action.Enable();
    }

    private void OnDisable()
    {
        shootAction.action.Disable();
        _isHoldingButton = false;
    }

    private void Update()
    {
        if (_canShoot && (_isHoldingButton || rapidFireEnabled))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.playerProjectile);
            StartCoroutine(ShootProjectile());
        }
    }

    private IEnumerator ShootProjectile()
    {
        _canShoot = false;
        
        // Show reload bar and start it full
        shield.SetReloadBarVisible(true);
        shield.SetReloadBarFill(1.0f);

        // Create projectile
        Vector2 spawnPosition = shield.transform.position;
        Quaternion spawnRotation = Quaternion.Euler(0, 0, shield.CurrentAngle);
        GameObject projectile = Instantiate(playerProjectilePrefab, spawnPosition, spawnRotation);
        projectile.tag = gameObject.tag + "Projectile";

        // Check if this projectile should be poisoned
        PoisonTipBoost poisonTipBoost = GetComponent<PoisonTipBoost>();
        if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
        {
            // Add PoisonProjectile component to make this projectile poisonous
            projectile.AddComponent<PoisonProjectile>();
        }

        // Check if shadow arrow should be spawned
        ShadowArrowBoost shadowArrowBoost = GetComponent<ShadowArrowBoost>();
        if (shadowArrowBoost != null && shadowArrowBoost.GetShadowArrowPrefab() != null)
        {
            // Get shadow spawn position with variance
            Vector2 shadowSpawnPosition = shadowArrowBoost.GetShadowSpawnPosition(spawnPosition, shield.Direction);
            GameObject shadowArrow = Instantiate(shadowArrowBoost.GetShadowArrowPrefab(), shadowSpawnPosition, spawnRotation);
            shadowArrow.tag = gameObject.tag + "Projectile";
            
            // Shadow arrow has independent poison chance
            if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
            {
                shadowArrow.AddComponent<PoisonProjectile>();
            }
            
            // Set shadow arrow velocity (same direction and speed as main projectile)
            shadowArrow.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * projectileSpeed;
            
            // Set shadow arrow damage (reduced damage)
            PlayerProjectile shadowProjectileComponent = shadowArrow.GetComponent<PlayerProjectile>();
            if (shadowProjectileComponent != null)
            {
                // Calculate damage: base damage + damage bonus, then apply shadow multiplier
                int mainProjectileDamage = shadowProjectileComponent.damage + damageBonus;
                shadowProjectileComponent.damage = Mathf.RoundToInt(mainProjectileDamage * shadowArrowBoost.GetDamageMultiplier());
            }
            
            // Start lifetime countdown for shadow arrow
            StartCoroutine(DestroyProjectile(shadowArrow));
        }

        // Set velocity
        projectile.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * projectileSpeed;
        
        // Apply damage bonus to the projectile
        PlayerProjectile playerProjectileComponent = projectile.GetComponent<PlayerProjectile>();
        if (playerProjectileComponent != null)
        {
            playerProjectileComponent.damage += damageBonus;
        }

        // Start lifetime countdown
        StartCoroutine(DestroyProjectile(projectile));

        // Gradual cooldown with fill amount updates
        float elapsed = 0f;
        while (elapsed < cooldownTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / cooldownTime;
            float remainingFill = 1f - progress; // Start at 1, go to 0
            shield.SetReloadBarFill(remainingFill);
            yield return null;
        }
        
        // Hide reload bar and re-enable shooting
        shield.SetReloadBarVisible(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.reload);
        _canShoot = true;
    }

    // New projectile destruction coroutine
    private IEnumerator DestroyProjectile(GameObject projectile)
    {
        yield return new WaitForSeconds(projectileLifetime);
        
        if (projectile != null)
        {
            Destroy(projectile);
        }
    }

    // Method for upgrades to modify projectile speed
    public void ModifyProjectileSpeed(float multiplier)
    {
        projectileSpeed *= multiplier;
    }
    
    // Method for damage upgrades to increase damage
    public void IncreaseDamage(int amount)
    {
        damageBonus += amount;
    }
}
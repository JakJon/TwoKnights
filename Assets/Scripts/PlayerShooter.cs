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

    // (Moved shadow spawn below after computing final damage)

        // Set velocity
        projectile.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * projectileSpeed;
        
        // Apply damage bonus to the projectile
        PlayerProjectile playerProjectileComponent = projectile.GetComponent<PlayerProjectile>();
        if (playerProjectileComponent != null)
        {
            playerProjectileComponent.damage += damageBonus;
        }

        // Capture final main projectile damage to drive shadow damage scaling
        int mainProjectileFinalDamage = playerProjectileComponent != null ? playerProjectileComponent.damage : 0;

        // Check if shadow arrow(s) should be spawned
        ShadowArrowBoost shadowArrowBoost = GetComponent<ShadowArrowBoost>();
        if (shadowArrowBoost != null && shadowArrowBoost.GetShadowArrowPrefab() != null)
        {
            // Spawn chain asynchronously with small delay between spawns so they don't all appear at once
            StartCoroutine(SpawnShadowArrows(shadowArrowBoost, projectile, shield.Direction, spawnRotation, gameObject.tag + "Projectile", poisonTipBoost, mainProjectileFinalDamage));
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

    // Spawns the chain of shadow arrows with a 0.1s delay between each
    private IEnumerator SpawnShadowArrows(ShadowArrowBoost shadowArrowBoost, GameObject initialLeader, Vector2 direction, Quaternion rotation, string projectileTag, PoisonTipBoost poisonTipBoost, int mainProjectileFinalDamage)
    {
        int amount = shadowArrowBoost.GetShadowArrowAmount();
        if (amount <= 0)
            yield break;

        GameObject leaderGO = initialLeader;
        for (int i = 0; i < amount; i++)
        {
            if (i > 0)
            {
                yield return new WaitForSeconds(0.0485f); // DELAY BETWEEN SHADOW ARROWS
            }

            // Use the current leader position at the time of spawn so distance matches settings
            if (leaderGO == null)
            {
                // If leader disappeared, stop spawning remaining shadows to avoid odd placement
                yield break;
            }

            Vector2 leaderPositionNow = leaderGO.transform.position;
            Vector2 shadowSpawnPosition = shadowArrowBoost.GetShadowSpawnPosition(leaderPositionNow, direction);
            GameObject shadowArrow = Instantiate(shadowArrowBoost.GetShadowArrowPrefab(), shadowSpawnPosition, rotation);
            shadowArrow.tag = projectileTag;

            // Independent poison chance per arrow
            if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
            {
                shadowArrow.AddComponent<PoisonProjectile>();
            }

            // Velocity and lifetime same as main projectile
            shadowArrow.GetComponent<Rigidbody2D>().linearVelocity = direction * projectileSpeed;
            StartCoroutine(DestroyProjectile(shadowArrow));

                // Damage reduced by multiplier relative to the main projectile's final damage
                int scaledShadowDamage = Mathf.RoundToInt(mainProjectileFinalDamage * shadowArrowBoost.GetDamageMultiplier());
                var shadowProjComponents = shadowArrow.GetComponentsInChildren<PlayerProjectile>(true);
                foreach (var comp in shadowProjComponents)
                {
                    comp.damage = scaledShadowDamage;
                }

            // Next shadow trails this one; update leader to the new shadow arrow GameObject
            leaderGO = shadowArrow;
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
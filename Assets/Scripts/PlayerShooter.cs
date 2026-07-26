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

    // No-cooldown window: shared by the RapidFire special and Thousand Cuts
    // (Shadow capstone). Never mutate cooldownTime for temporary effects — it's
    // the persistent, upgrade-modified value (Reload upgrades multiply it).
    private float _noCooldownUntil = -1f;
    // While a window is open, the cooldown collapses to this floor (seconds
    // between shots) instead of the full cooldownTime. Callers pick the floor so
    // Rapid Fire and Thousand Cuts can each tune how fast they fire.
    private float _noCooldownFloor = 0.16f;

    public void OpenNoCooldownWindow(float duration, float cooldownFloor = 0.16f)
    {
        // Fresh window resets the floor; overlapping windows keep the fastest one
        if (Time.time >= _noCooldownUntil)
            _noCooldownFloor = cooldownFloor;
        else
            _noCooldownFloor = Mathf.Min(_noCooldownFloor, cooldownFloor);
        _noCooldownUntil = Mathf.Max(_noCooldownUntil, Time.time + duration);
    }

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

        // Ember: the deterministic fireball cadence. Every Nth shot leaves the
        // shield as a fireball instead of an arrow — a counter, not a roll, so the
        // player can count to five and time the big one into a cluster.
        EmberBoost emberBoost = GetComponent<EmberBoost>();
        bool isFireball = emberBoost != null && emberBoost.AdvanceShotAndCheckFireball();
        GameObject prefabToFire = playerProjectilePrefab;
        if (isFireball && emberBoost.FireballPrefab != null)
        {
            prefabToFire = emberBoost.FireballPrefab;
        }

        GameObject projectile = Instantiate(prefabToFire, spawnPosition, spawnRotation);
        projectile.tag = gameObject.tag + "Projectile";

        // Check if this projectile should be poisoned
        PoisonTipBoost poisonTipBoost = GetComponent<PoisonTipBoost>();
        if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
        {
            // Add PoisonProjectile component to make this projectile poisonous
            PoisonProjectile poisonComponent = projectile.AddComponent<PoisonProjectile>();
            poisonComponent.ConfigureFromBoost(poisonTipBoost);
        }

    // (Moved shadow spawn below after computing final damage)

        // Set velocity — a fireball lobs slower so the player can read it coming
        float launchSpeed = isFireball ? projectileSpeed * FireballSpeedFactor : projectileSpeed;
        projectile.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * launchSpeed;

        // Apply damage bonus to the projectile
        NinjaBoost ninjaBoost = GetComponent<NinjaBoost>();
        PlayerProjectile playerProjectileComponent = projectile.GetComponent<PlayerProjectile>();
        if (playerProjectileComponent != null)
        {
            playerProjectileComponent.damage += damageBonus;
            playerProjectileComponent.ownerNinjaBoost = ninjaBoost;
        }

        // Capture final main projectile damage to drive shadow damage scaling.
        // Read BEFORE the fireball multiplier so shadow arrows and shurikens keep
        // scaling off a normal arrow instead of spiking every Nth shot.
        int mainProjectileFinalDamage = playerProjectileComponent != null ? playerProjectileComponent.damage : 0;

        if (playerProjectileComponent != null && emberBoost != null)
        {
            // Ignited Tips: independent roll per projectile, same as poison
            if (emberBoost.ShouldIgnite())
            {
                playerProjectileComponent.ignitesOnHit = true;
                FireFx.AttachArrowTrail(projectile);
                // Fireballs always ignite and announce themselves with their own launch sound
                if (!isFireball)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.arrowIgnite);
                }
            }

            if (isFireball)
            {
                ConfigureFireball(projectile, playerProjectileComponent, emberBoost, mainProjectileFinalDamage);
                AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballLaunch);
            }
        }

        // Shuriken Fan (Shadow Order): the main shot splits into angled copies
        if (ninjaBoost != null && ninjaBoost.ShurikenLevel > 0)
        {
            SpawnShurikens(ninjaBoost, spawnPosition, mainProjectileFinalDamage, poisonTipBoost, emberBoost);
        }

        // Check if shadow arrow(s) should be spawned
        ShadowArrowBoost shadowArrowBoost = GetComponent<ShadowArrowBoost>();
        if (shadowArrowBoost != null && shadowArrowBoost.GetShadowArrowPrefab() != null)
        {
            // Spawn chain asynchronously with small delay between spawns so they don't all appear at once
            StartCoroutine(SpawnShadowArrows(shadowArrowBoost, projectile, shield.Direction, spawnRotation, gameObject.tag + "Projectile", poisonTipBoost, mainProjectileFinalDamage, emberBoost));
        }

        // Start lifetime countdown
        StartCoroutine(DestroyProjectile(projectile));

        // Gradual cooldown with fill amount updates
        float elapsed = 0f;
    while (elapsed < cooldownTime)
    {
            elapsed += Time.deltaTime;
            // An open no-cooldown window collapses the cooldown to its floor
            if (Time.time < _noCooldownUntil && elapsed >= _noCooldownFloor)
            {
                break;
            }
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

    // Ember tuning knobs
    private const float FireballSpeedFactor = 0.7f;
    private const float FireballDirectDamageFactor = 1.5f;

    // A fireball is a sanctioned ignition source: it always lights what it touches
    // and always leaves a crater. Blast damage stays at a normal arrow's value; the
    // direct hit is what's multiplied.
    private void ConfigureFireball(GameObject projectile, PlayerProjectile component,
        EmberBoost emberBoost, int baseDamage)
    {
        component.damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * FireballDirectDamageFactor));
        component.ignitesOnHit = true;

        FireballProjectile fireball = projectile.AddComponent<FireballProjectile>();
        fireball.blastRadius = emberBoost.FireballBlastRadius;
        fireball.blastDamage = Mathf.Max(1, baseDamage);
        fireball.ownerBoost = emberBoost;
        fireball.ownerTag = gameObject.tag;
    }

    // Shuriken Fan: angled copies of the main arrow at 35% damage; each rolls
    // its own poison chance so Serpent/Shadow cross-builds keep their bite
    private void SpawnShurikens(NinjaBoost ninjaBoost, Vector2 spawnPosition, int mainDamage, PoisonTipBoost poisonTipBoost, EmberBoost emberBoost)
    {
        int shurikenDamage = Mathf.Max(1, Mathf.RoundToInt(mainDamage * 0.35f));

        // One swish per volley, not per shuriken
        AudioManager.Instance.PlaySFX(AudioManager.Instance.shurikenThrow);

        // Prefab travels with the upgrade (see ShurikenFanUpgrade); arrow as fallback
        GameObject fanPrefab = ninjaBoost.ShurikenPrefab != null ? ninjaBoost.ShurikenPrefab : playerProjectilePrefab;

        var angles = new System.Collections.Generic.List<float> { -12f, 12f };
        if (ninjaBoost.ShurikenLevel >= 2)
        {
            angles.Add(-24f);
            angles.Add(24f);
        }

        // Perpendicular to the shot direction: fan each shuriken out along it so
        // they start spaced apart instead of stacking on the muzzle and colliding
        Vector2 perpendicular = new Vector2(-shield.Direction.y, shield.Direction.x);
        const float lateralSpacing = 0.3f; // world units per 12° of fan angle

        foreach (float angle in angles)
        {
            Vector2 direction = Quaternion.Euler(0, 0, angle) * shield.Direction;
            Vector2 shurikenSpawn = spawnPosition + perpendicular * (angle / 12f * lateralSpacing);
            GameObject shuriken = Instantiate(fanPrefab, shurikenSpawn,
                Quaternion.Euler(0, 0, shield.CurrentAngle + angle));
            shuriken.tag = gameObject.tag + "Projectile";

            if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
            {
                PoisonProjectile poison = shuriken.AddComponent<PoisonProjectile>();
                poison.ConfigureFromBoost(poisonTipBoost);
            }

            Rigidbody2D shurikenBody = shuriken.GetComponent<Rigidbody2D>();
            shurikenBody.linearVelocity = direction * projectileSpeed;

            // Spin toward its side of the fan; the prefab's sprite pivots on center
            if (ninjaBoost.ShurikenPrefab != null)
                shurikenBody.angularVelocity = angle >= 0f ? 540f : -540f;

            PlayerProjectile projectileComponent = shuriken.GetComponent<PlayerProjectile>();
            if (projectileComponent != null)
            {
                projectileComponent.damage = shurikenDamage;
                projectileComponent.ownerNinjaBoost = ninjaBoost;
                // Shurikens don't absorb fire/poison from the field — only the main shot does
                projectileComponent.absorbsFieldEffects = false;

                // Independent ignite roll per shuriken, mirroring the poison roll
                if (emberBoost != null && emberBoost.ShouldIgnite())
                {
                    projectileComponent.ignitesOnHit = true;
                    FireFx.AttachArrowTrail(shuriken);
                }
            }

            StartCoroutine(DestroyProjectile(shuriken));
        }
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
    private IEnumerator SpawnShadowArrows(ShadowArrowBoost shadowArrowBoost, GameObject initialLeader, Vector2 direction, Quaternion rotation, string projectileTag, PoisonTipBoost poisonTipBoost, int mainProjectileFinalDamage, EmberBoost emberBoost)
    {
        int amount = shadowArrowBoost.GetShadowArrowAmount();
        if (amount <= 0)
            yield break;

        // One echo swish per chain — the arrows land 0.05s apart, per-arrow would machine-gun
        AudioManager.Instance.PlaySFX(AudioManager.Instance.shadowArrow);

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
                PoisonProjectile shadowPoison = shadowArrow.AddComponent<PoisonProjectile>();
                shadowPoison.ConfigureFromBoost(poisonTipBoost);
            }

            // Velocity and lifetime same as main projectile
            shadowArrow.GetComponent<Rigidbody2D>().linearVelocity = direction * projectileSpeed;
            StartCoroutine(DestroyProjectile(shadowArrow));

                // Damage reduced by multiplier relative to the main projectile's final damage
                int scaledShadowDamage = Mathf.RoundToInt(mainProjectileFinalDamage * shadowArrowBoost.GetDamageMultiplier());
                var shadowNinjaBoost = GetComponent<NinjaBoost>();
                var shadowProjComponents = shadowArrow.GetComponentsInChildren<PlayerProjectile>(true);
                // One ignite roll for the arrow, applied to all its components, so a
                // multi-part shadow arrow can't roll ignite several times over
                bool shadowIgnites = emberBoost != null && emberBoost.ShouldIgnite();
                foreach (var comp in shadowProjComponents)
                {
                    comp.damage = scaledShadowDamage;
                    comp.ownerNinjaBoost = shadowNinjaBoost;
                    comp.ignitesOnHit = shadowIgnites;
                    // Shadow arrows don't absorb fire/poison from the field — only the main shot does
                    comp.absorbsFieldEffects = false;
                }
                if (shadowIgnites)
                {
                    FireFx.AttachArrowTrail(shadowArrow);
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
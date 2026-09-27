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

    /// <summary>
    /// Whether this knight's bow answers the trigger. Only the tutorial lowers
    /// it, so it can hand the controls over one at a time; everything else in the
    /// game leaves a knight's weapons live from the moment it is on the field.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

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
        SetFireballChargeFx(false);
        SetVialChargeFx(false);
    }

    // Ember tell: while the loaded shot is the fireball, the shield smoulders. The
    // cadence is deterministic, so this is a promise the player can act on — built
    // lazily because EmberBoost only exists once the Fireball upgrade lands.
    private ParticleSystem _fireballChargeFx;
    private bool _fireballChargeActive;

    private void UpdateFireballChargeFx()
    {
        EmberBoost emberBoost = GetComponent<EmberBoost>();
        bool shouldCharge = emberBoost != null && emberBoost.NextShotIsFireball;

        if (shouldCharge && _fireballChargeFx == null && shield != null)
        {
            _fireballChargeFx = FireFx.AttachShieldCharge(shield.gameObject);
        }

        SetFireballChargeFx(shouldCharge);
    }

    private void SetFireballChargeFx(bool active)
    {
        if (_fireballChargeFx == null || active == _fireballChargeActive) return;

        var emission = _fireballChargeFx.emission;
        emission.enabled = active;
        if (active)
        {
            _fireballChargeFx.Play();
        }
        _fireballChargeActive = active;
    }

    // Serpent/Shadow tell: Zs drift off the shield while the loaded shot is the
    // sleeping dart. Deliberately the same shape of promise as the fireball's
    // smoulder above, and deliberately independent of it - when both are loaded
    // the shield shows BOTH, because both are going to happen.
    private void UpdateDartChargeFx()
    {
        SleepBoost sleepBoost = GetComponent<SleepBoost>();
        if (sleepBoost == null || shield == null) return;

        // SleepFx owns the lifetime: it raises the Zs when the dart is next and
        // takes them off by itself the moment it isn't, so there is no enabled
        // flag to keep in step here.
        if (sleepBoost.NextShotIsDart) SleepFx.ShowShieldTell(shield.gameObject, sleepBoost);
    }

    // Serpent tell: the shield steams while the loaded shot is the vial. Same
    // shape of promise as the fireball's smoulder and the dart's Zs, and
    // deliberately independent of both - when several are loaded the shield shows
    // all of them, because all of them are going to happen.
    private ParticleSystem _vialChargeFx;
    private bool _vialChargeActive;

    private void UpdateVialChargeFx()
    {
        PoisonVialBoost vialBoost = GetComponent<PoisonVialBoost>();
        bool shouldCharge = vialBoost != null && vialBoost.NextShotIsVial;

        if (shouldCharge && _vialChargeFx == null && shield != null)
        {
            _vialChargeFx = PoisonFx.AttachShieldCharge(shield.gameObject);
        }

        SetVialChargeFx(shouldCharge);
    }

    private void SetVialChargeFx(bool active)
    {
        if (_vialChargeFx == null || active == _vialChargeActive) return;

        var emission = _vialChargeFx.emission;
        emission.enabled = active;
        if (active) _vialChargeFx.Play();
        _vialChargeActive = active;
    }

    private void Update()
    {
        UpdateFireballChargeFx();
        UpdateDartChargeFx();
        UpdateVialChargeFx();

        if (InputEnabled && _canShoot && (_isHoldingButton || rapidFireEnabled))
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

        // Sleeping Dart. The counter runs on EVERY shot, independent of Ember: the
        // two cadences are separate promises and neither gets to eat the other.
        // When they land on the same shot the shield fires one of each - the
        // fireball is the shot that was already leaving, and the dart goes out
        // beside it rather than waiting its turn.
        SleepBoost sleepBoost = GetComponent<SleepBoost>();
        bool isDart = sleepBoost != null && sleepBoost.AdvanceShotAndCheckDart();
        if (isDart && !isFireball)
        {
            projectile.AddComponent<SleepDartProjectile>()
                      .Configure(sleepBoost.SleepSeconds, sleepBoost.DartSprite);
        }

        // Guided Shot (Guardian): the MAIN shot bends onto what it passes close to.
        // Shadow arrows and shurikens deliberately fly straight — the same line
        // absorbsFieldEffects draws, and for the same reason: the knight's own shot is
        // the one the Order is paying to land, and a steered shuriken fan would clear
        // a screen without anybody aiming at anything.
        //
        // Added only when the knight owns the chain, so a run with no Guardian picks
        // never puts a per-frame search on an arrow.
        GuardianBoost guardianBoost = GetComponent<GuardianBoost>();
        if (guardianBoost != null && guardianBoost.GuidedShotRadius > 0f)
        {
            projectile.AddComponent<GuidedShot>().Configure(guardianBoost.GuidedShotRadius, true, gameObject.tag);
        }

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

        // Dawn: holy damage, worth +2 for every Dawn upgrade this knight owns. It
        // is added HERE rather than folded into damageBonus because the echoes are
        // paid a different share of it, and that needs two numbers to exist at once.
        DawnBoost dawnBoost = GetComponent<DawnBoost>();
        int holyDamage = dawnBoost != null ? dawnBoost.HolyDamage : 0;
        int echoHolyDamage = dawnBoost != null ? dawnBoost.EchoHolyDamage : 0;

        // What the ECHOES scale off: the arrow WITHOUT its holy. A shadow arrow is a
        // fifth of an arrow, so scaling the holy down by that fifth as well would
        // round it away to nothing; instead each echo takes its own flat share below.
        int echoBaseDamage = playerProjectileComponent != null ? playerProjectileComponent.damage : 0;

        if (playerProjectileComponent != null && holyDamage > 0)
        {
            playerProjectileComponent.damage += holyDamage;
            playerProjectileComponent.holyDamage = holyDamage;
            DawnFx.AttachArrowTrail(projectile, dawnBoost.DawnPicks);
        }

        // Capture final main projectile damage — holy included, because a fireball
        // and the companion dart are both the knight's own shot. Read BEFORE the
        // fireball multiplier so the dart keeps an arrow's damage instead of
        // spiking every Nth shot.
        int mainProjectileFinalDamage = playerProjectileComponent != null ? playerProjectileComponent.damage : 0;

        // Frigid: every shot a Frigid knight fires carries the sheet. There is no
        // roll and no counter - the Order is the one with no dice in it, so the
        // only question is whether the knight has bought the arrow chain yet.
        FrigidBoost frigidBoost = GetComponent<FrigidBoost>();
        if (playerProjectileComponent != null && frigidBoost != null)
        {
            playerProjectileComponent.ownerFrigidBoost = frigidBoost;
            if (frigidBoost.ArrowsChill) FrostFx.AttachArrowTrail(projectile);
        }

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
                ConfigureFireball(projectile, playerProjectileComponent, emberBoost,
                                  mainProjectileFinalDamage, holyDamage);
                AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballLaunch);
            }
        }

        // Echo shots — shurikens and shadow arrows — are held while an orb trial is
        // on the board (owner's call): there the knight's own arrow is the test.
        bool echoesHeld = TrialRunner.EchoShotsHeld;

        // Shuriken Fan (Shadow Order): the main shot splits into angled copies
        if (!echoesHeld && ninjaBoost != null && ninjaBoost.ShurikenLevel > 0)
        {
            SpawnShurikens(ninjaBoost, spawnPosition, echoBaseDamage, echoHolyDamage, poisonTipBoost, emberBoost);
        }

        // Check if shadow arrow(s) should be spawned
        ShadowArrowBoost shadowArrowBoost = GetComponent<ShadowArrowBoost>();
        if (!echoesHeld && shadowArrowBoost != null && shadowArrowBoost.GetShadowArrowPrefab() != null)
        {
            // Spawn chain asynchronously with small delay between spawns so they don't all appear at once
            StartCoroutine(SpawnShadowArrows(shadowArrowBoost, projectile, shield.Direction, spawnRotation, gameObject.tag + "Projectile", poisonTipBoost, echoBaseDamage, echoHolyDamage, emberBoost));
        }

        // The dart riding alongside a fireball. Spawned here rather than above so
        // it can be paid the same damage bonus the main shot just worked out.
        if (isDart && isFireball)
        {
            SpawnCompanionDart(sleepBoost, spawnPosition, spawnRotation, mainProjectileFinalDamage,
                               ninjaBoost, poisonTipBoost);
        }

        // Vial Throw (Serpent): the vials go out BESIDE the arrow, never instead of
        // it. A vial is the knight's other hand - it costs the shot nothing, which
        // is the whole reason a counter can promise one every fifth arrow without
        // the promise also being a hole in the knight's damage.
        //
        // Its own counter, advanced on every shot and independent of Ember's and
        // the dart's, for the reason SleepBoost states: three cadences that could
        // eat each other are three promises the player cannot rely on.
        PoisonVialBoost vialBoost = GetComponent<PoisonVialBoost>();
        if (vialBoost != null && vialBoost.AdvanceShotAndCheckVial())
        {
            ThrowVials(vialBoost, spawnPosition);
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
        
        // Hide reload bar and re-enable shooting. Silently: the reload cry used to
        // go off here, a second and a half after a shot with nothing on screen to
        // explain it, and it read as the arrow making a noise on its way out. The
        // bar coming back is the tell that the bow is ready.
        shield.SetReloadBarVisible(false);
        _canShoot = true;
    }

    // Every vial of one throw, out at once. Thrown from the SHIELD rather than
    // from the knight, and measured along the shield's facing, so "two units along
    // the aim" means two units from where the player is already looking - the same
    // frame of reference every other thing that leaves this knight uses.
    private void ThrowVials(PoisonVialBoost boost, Vector2 from)
    {
        Sprite sprite = PoisonVialSprite.Current;
        if (sprite == null) return;

        Vector2 aim = shield.Direction;
        var throws = boost.Throws;

        for (int i = 0; i < throws.Count; i++)
        {
            PoisonVialBoost.Throw t = throws[i];
            Vector2 direction = t.alongAim ? aim : -aim;
            PoisonVial.Throw(sprite, from, direction, t.distance, gameObject.tag);
        }
    }

    // Ember tuning knobs
    private const float FireballSpeedFactor = 0.7f;
    private const float FireballDirectDamageFactor = 1.5f;

    // A fireball is a sanctioned ignition source: it always lights what it touches
    // and always leaves a crater. Blast damage stays at a normal arrow's value; the
    // direct hit is what's multiplied.
    private void ConfigureFireball(GameObject projectile, PlayerProjectile component,
        EmberBoost emberBoost, int baseDamage, int holyOnThisShot)
    {
        component.damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * FireballDirectDamageFactor));
        // Scaled with the rest of the hit. baseDamage already has the holy inside
        // it, so the direct hit multiplies Dawn's contribution along with
        // everything else — and the white number has to say so, or the split reads
        // as the Order going quiet the moment the arrow becomes a fireball.
        component.holyDamage = Mathf.RoundToInt(component.holyDamage * FireballDirectDamageFactor);
        component.ignitesOnHit = true;

        FireballProjectile fireball = projectile.AddComponent<FireballProjectile>();
        fireball.blastRadius = emberBoost.FireballBlastRadius;
        fireball.blastDamage = Mathf.Max(1, baseDamage);
        // The blast is paid baseDamage flat, holy and all, so it carries the
        // undivided holy share rather than the direct hit's scaled one.
        fireball.blastHolyDamage = holyOnThisShot;
        fireball.ownerBoost = emberBoost;
        fireball.ownerTag = gameObject.tag;
    }

    // An ordinary arrow wearing the dart, sent out beside a fireball on the shot
    // where both cadences came due. It is offset across the facing so the two
    // leave as a readable pair instead of one hiding inside the other - and the
    // fireball lobs slower anyway, so they separate on their own after that.
    //
    // Damage is the arrow's, not the fireball's: the dart is a normal shot that
    // happens to put things to sleep, and paying it the fireball's multiplier
    // would make the doubled-up shot the best damage in the game by accident.
    private void SpawnCompanionDart(SleepBoost sleepBoost, Vector2 spawnPosition,
                                    Quaternion spawnRotation, int arrowDamage,
                                    NinjaBoost ninjaBoost, PoisonTipBoost poisonTipBoost)
    {
        Vector2 perpendicular = new Vector2(-shield.Direction.y, shield.Direction.x);
        Vector2 dartSpawn = spawnPosition + perpendicular * 0.22f;

        GameObject dart = Instantiate(playerProjectilePrefab, dartSpawn, spawnRotation);
        dart.tag = gameObject.tag + "Projectile";
        dart.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * projectileSpeed;

        PlayerProjectile component = dart.GetComponent<PlayerProjectile>();
        if (component != null)
        {
            component.damage = Mathf.Max(1, arrowDamage);
            // The dart is paid the arrow's damage holy and all (see above), so it
            // carries the same holy share for the split number over the body
            component.holyDamage = GetComponent<DawnBoost>() != null
                ? GetComponent<DawnBoost>().HolyDamage : 0;
            component.ownerNinjaBoost = ninjaBoost;
            // The main shot is the one that drinks from the field; a second
            // projectile absorbing the same cloud would pay the build twice
            component.absorbsFieldEffects = false;
            component.ownerFrigidBoost = GetComponent<FrigidBoost>();
        }

        // Its own poison roll, exactly as each shuriken gets one
        if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
        {
            dart.AddComponent<PoisonProjectile>().ConfigureFromBoost(poisonTipBoost);
        }

        dart.AddComponent<SleepDartProjectile>()
            .Configure(sleepBoost.SleepSeconds, sleepBoost.DartSprite);

        // The dart is paid the arrow's damage, holy and all, so it wears the tell
        DawnBoost dartDawn = GetComponent<DawnBoost>();
        if (dartDawn != null && dartDawn.HolyDamage > 0) DawnFx.AttachArrowTrail(dart, dartDawn.DawnPicks);

        StartCoroutine(DestroyProjectile(dart));
    }

    // How far down the Dawn Order this knight has gone. The holy glow on a shot is
    // drawn from it (see DawnFx.AttachArrowTrail), and the echo spawners are given
    // their holy as a plain number rather than the sheet it came off, so they ask
    // here rather than being handed a fifth parameter apiece.
    private int DawnPickCount
    {
        get
        {
            DawnBoost boost = GetComponent<DawnBoost>();
            return boost != null ? boost.DawnPicks : 0;
        }
    }

    // Shuriken Fan: angled copies of the main arrow at 35% damage; each rolls
    // its own poison chance so Serpent/Shadow cross-builds keep their bite
    private void SpawnShurikens(NinjaBoost ninjaBoost, Vector2 spawnPosition, int mainDamage, int holyDamage, PoisonTipBoost poisonTipBoost, EmberBoost emberBoost)
    {
        // Nightglass Shard adds to the multiplier here too, the same way it does
        // for shadow arrows: 0.35 + 0.15 = half the main arrow
        var equipment = GetComponent<EquipmentBoost>();
        float shurikenMultiplier = 0.35f + (equipment != null ? equipment.ShadowArrowDamageBonus : 0f);
        // Dawn's holy rides the fan at its echo share, added AFTER the scaling so a
        // shuriken at 35% of an arrow still feels the Order it was fired by
        int shurikenDamage = Mathf.Max(1, Mathf.RoundToInt(mainDamage * shurikenMultiplier) + holyDamage);

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

        // One counter shared by this fan, so "four from the same volley landed"
        // is answerable without a registry
        var volley = new ShurikenVolley();

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
            foreach (var comp in shuriken.GetComponentsInChildren<PlayerProjectile>(true))
            {
                comp.volley = volley;
            }

            // Frigid rides every projectile the knight puts out, this one included
            FrigidBoost shurikenFrigid = GetComponent<FrigidBoost>();

            if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
            {
                // Poisons what it hits, but lays no bead trail
                PoisonProjectile poison = shuriken.AddComponent<PoisonProjectile>();
                poison.ConfigureFromBoost(poisonTipBoost, shedsTrail: false);
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
                projectileComponent.holyDamage = holyDamage;
                projectileComponent.ownerNinjaBoost = ninjaBoost;
                // Shurikens don't absorb fire/poison from the field — only the main shot does
                projectileComponent.absorbsFieldEffects = false;

                projectileComponent.ownerFrigidBoost = shurikenFrigid;
                if (shurikenFrigid != null && shurikenFrigid.ArrowsChill)
                {
                    FrostFx.AttachArrowTrail(shuriken);
                }

                // Independent ignite roll per shuriken, mirroring the poison roll
                if (emberBoost != null && emberBoost.ShouldIgnite())
                {
                    projectileComponent.ignitesOnHit = true;
                    FireFx.AttachArrowTrail(shuriken);
                }

                if (holyDamage > 0) DawnFx.AttachArrowTrail(shuriken, DawnPickCount);
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
            // A fireball that reaches the end of its flight burns out; every other
            // way one can end bursts it (see FireballProjectile.OnDestroy)
            FireballProjectile fireball = projectile.GetComponent<FireballProjectile>();
            if (fireball != null) fireball.MarkBurnedOut();

            Destroy(projectile);
        }
    }

    // Spawns the chain of shadow arrows with a 0.1s delay between each
    private IEnumerator SpawnShadowArrows(ShadowArrowBoost shadowArrowBoost, GameObject initialLeader, Vector2 direction, Quaternion rotation, string projectileTag, PoisonTipBoost poisonTipBoost, int mainProjectileFinalDamage, int holyDamage, EmberBoost emberBoost)
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
            FrigidBoost shadowFrigid = GetComponent<FrigidBoost>();

            if (poisonTipBoost != null && poisonTipBoost.ShouldApplyPoison())
            {
                // Poisons what it hits, but lays no bead trail
                PoisonProjectile shadowPoison = shadowArrow.AddComponent<PoisonProjectile>();
                shadowPoison.ConfigureFromBoost(poisonTipBoost, shedsTrail: false);
            }

            // Velocity and lifetime same as main projectile
            shadowArrow.GetComponent<Rigidbody2D>().linearVelocity = direction * projectileSpeed;
            StartCoroutine(DestroyProjectile(shadowArrow));

                // Damage reduced by multiplier relative to the main projectile's final damage
                // Nightglass Shard adds to the multiplier rather than the final
                // number, so it scales with the knight's damage like the arrow does
                var shadowEquipment = GetComponent<EquipmentBoost>();
                float shadowMultiplier = shadowArrowBoost.GetDamageMultiplier()
                                         + (shadowEquipment != null ? shadowEquipment.ShadowArrowDamageBonus : 0f);
                // Dawn's holy, at the echo share and flat on top of the scaling —
                // see SpawnShurikens for why it is not scaled with the rest
                int scaledShadowDamage = Mathf.RoundToInt(mainProjectileFinalDamage * shadowMultiplier) + holyDamage;
                var shadowNinjaBoost = GetComponent<NinjaBoost>();
                var shadowProjComponents = shadowArrow.GetComponentsInChildren<PlayerProjectile>(true);
                // One ignite roll for the arrow, applied to all its components, so a
                // multi-part shadow arrow can't roll ignite several times over
                bool shadowIgnites = emberBoost != null && emberBoost.ShouldIgnite();
                foreach (var comp in shadowProjComponents)
                {
                    comp.isShadowArrow = true;
                    comp.damage = scaledShadowDamage;
                    comp.holyDamage = holyDamage;
                    comp.ownerNinjaBoost = shadowNinjaBoost;
                    comp.ignitesOnHit = shadowIgnites;
                    comp.ownerFrigidBoost = shadowFrigid;
                    // Shadow arrows don't absorb fire/poison from the field — only the main shot does
                    comp.absorbsFieldEffects = false;
                }
                if (shadowIgnites)
                {
                    FireFx.AttachArrowTrail(shadowArrow);
                }
                if (holyDamage > 0)
                {
                    DawnFx.AttachArrowTrail(shadowArrow, DawnPickCount);
                }

            // Next shadow trails this one; update leader to the new shadow arrow GameObject
            leaderGO = shadowArrow;
        }
    }

    // The speed the prefab was authored at. Cached on first use rather than in
    // Awake so an upgrade applied at any point in a knight's life reads the same
    // baseline — see SetProjectileSpeedMultiplier.
    private float _baseProjectileSpeed = -1f;

    /// <summary>
    /// Reflector (Guardian) sets how fast everything this knight puts out flies —
    /// the rocks coming off the guard and the arrows leaving it, one number for
    /// both. ABSOLUTE, not compounding: rank II replaces rank I's multiplier
    /// rather than stacking on it, so buying the chain in order and buying it out
    /// of order land on the same speed.
    /// </summary>
    public void SetProjectileSpeedMultiplier(float multiplier)
    {
        if (_baseProjectileSpeed < 0f) _baseProjectileSpeed = projectileSpeed;
        projectileSpeed = _baseProjectileSpeed * Mathf.Max(1f, multiplier);
    }

    /// <summary>
    /// Shadow Arrow's reload. Compounds deliberately — each tier of the chain is
    /// another 10% off, so five of them are 0.9^5 — and it is the persistent
    /// cooldown being changed, never the temporary no-cooldown window.
    /// </summary>
    public void MultiplyCooldown(float multiplier)
    {
        cooldownTime *= Mathf.Clamp(multiplier, 0.05f, 1f);
    }

    
    // Method for damage upgrades to increase damage
    public void IncreaseDamage(int amount)
    {
        damageBonus += amount;
    }
}
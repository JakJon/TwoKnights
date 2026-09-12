using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class SwordSwing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference swordSwingAction;
    
    [Header("Settings")]
    [SerializeField] private float swingDuration = 0.15f;
    [SerializeField] private float totalEffectDuration = 0.3f;
    [SerializeField] private float swingAngleRange = 60f;
    [SerializeField] private int swingDamage = 10;
    [SerializeField] private float cooldownTime = 1f;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private InputAction swordInputAction;
    private bool canSwing = true;
    private int currentSwingDamage; // Full damage for real swings, halved for Phantom Blade echoes
    private Transform swordSpriteTransform;
    private Vector3 _bladeBaseScale = Vector3.one;
    private Transform slashSpriteTransform;
    private HashSet<GameObject> damagedEnemies;
    private ShieldOrbit shield;
    private GameObject owningKnight;

    /// <summary>The knight this blade belongs to. Exposed because things the sword
    /// touches sometimes have to pay that knight rather than damage what they hit —
    /// an orb the blade sweeps through is collected for them, exactly as an arrow
    /// that reaches it is. Null until the first swing wires the shield up.</summary>
    public GameObject OwningKnight => owningKnight;

    // Long Sword lives on the knight, so it is looked up per swing rather than
    // cached in Awake: the upgrade can land between one swing and the next.
    private LongSwordBoost LongSword =>
        owningKnight != null ? owningKnight.GetComponent<LongSwordBoost>() : null;

    // Stock values unless a Long Sword rank says otherwise.
    private float BladeReach => LongSword != null ? LongSword.LengthMultiplier : 1f;
    private float SwingSlow => LongSword != null ? LongSword.SlowMultiplier : 1f;
    private int EffectiveSwingDamage
    {
        get
        {
            var boost = LongSword;
            return boost != null && boost.BaseDamage > 0 ? boost.BaseDamage : swingDamage;
        }
    }

    void Awake()
    {
        swordSpriteTransform = transform.Find("Sword");
        // The prefab's own blade scale is not 1 - Long Sword multiplies THIS,
        // never replaces it, or a knight without the upgrade gets a sword that
        // silently grew to full size the first time it swung.
        if (swordSpriteTransform != null) _bladeBaseScale = swordSpriteTransform.localScale;
        slashSpriteTransform = transform.Find("Slash");
        shield = GetComponentInParent<ShieldOrbit>();
        if (shield == null && transform.parent != null)
            shield = transform.parent.GetComponentInChildren<ShieldOrbit>();
        if (swordSwingAction != null)
        {
            swordInputAction = swordSwingAction.action;
            swordInputAction.Enable();
        }
        AddDamageDetection(swordSpriteTransform);
        AddDamageDetection(slashSpriteTransform);

        // The sword attack lives under its knight (knight_left/knight_right) —
        // Serpent's Breath reads the knight's poison boost per swing
        var parentHealth = GetComponentInParent<PlayerHealth>();
        if (parentHealth != null)
        {
            owningKnight = parentHealth.gameObject;
        }
    }

    void Update()
    {
        if (InputEnabled && canSwing && shield != null && swordInputAction != null && swordInputAction.WasPressedThisFrame())
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.swordSwing);
            StartCoroutine(PerformSwordSwing());
        }
    }

    private IEnumerator PerformSwordSwing()
    {
        canSwing = false;
        damagedEnemies = new HashSet<GameObject>();
        currentSwingDamage = EffectiveSwingDamage;
        TryExhaleSerpentsBreath();
        TryHurlFirebrand();
        TryReleaseRimeblade();
        TryQueueAcidDagger();
        if (swordSpriteTransform == null || slashSpriteTransform == null)
        {
            canSwing = true;
            yield break;
        }
        float shieldAngle = shield.CurrentAngle;
        // Feat tracking: a phase is the real swing or one echo. The Shadow quest
        // wants a swing where the real strike AND both echoes each connected.
        _phaseLanded = false;
        _phasesLanded = 0;

        // Only the ARC is slowed by a long blade, not the hold at the end of it:
        // the weight is meant to be felt in the sweep the player is waiting on.
        yield return StartCoroutine(AnimateSwingArc(shieldAngle, totalEffectDuration - swingDuration,
                                                    swingDuration * SwingSlow, BladeReach));
        if (_phaseLanded) _phasesLanded++;

        // Phantom Blade (Shadow Order): dark after-images repeat the swing. They
        // trail the real swing after a short beat, then whip through at 2x speed.
        NinjaBoost ninja = owningKnight != null ? owningKnight.GetComponent<NinjaBoost>() : null;
        int echoCount = ninja != null ? ninja.TotalPhantomEchoes : 0;
        for (int i = 0; i < echoCount; i++)
        {
            yield return new WaitForSeconds(0.1f);
            damagedEnemies = new HashSet<GameObject>();
            _phaseLanded = false;
            currentSwingDamage = Mathf.Max(1, EffectiveSwingDamage / 2);
            SetSwingTint(PhantomTint);
            AudioManager.Instance.PlaySFX(AudioManager.Instance.phantomStrike);
            yield return StartCoroutine(AnimateSwingArc(shieldAngle, 0.025f, swingDuration * 0.5f * SwingSlow,
                                                       (PhantomReach + i * PhantomReachStep) * BladeReach));
            SetSwingTint(Color.white);
            if (_phaseLanded) _phasesLanded++;
        }
        currentSwingDamage = EffectiveSwingDamage;

        // Three connected phases can only happen with two echoes behind the real
        // swing, which is what "a fully upgraded Phantom Blade" means
        if (echoCount >= 2 && _phasesLanded >= 3) Feats.Record(Feats.PhantomFullThree);

        yield return new WaitForSeconds(cooldownTime);
        canSwing = true;
    }

    // Phantom Blade echoes arc twice as far out as the real swing, so the dark
    // repeat sweeps ground the knight's own reach never covers instead of
    // redrawing itself on top of the strike that just happened. Each echo after
    // the first steps a unit further again, so the set fans outward — the third
    // swing of a full Phantom Blade lands at 3, not on top of the second.
    private const float PhantomReach = 2f;
    private const float PhantomReachStep = 1f;

    // One full swing animation pass (sprites on -> arc -> hold -> sprites off);
    // shared by the real swing and its Phantom Blade echoes. `reach` scales how
    // far from the knight the arc is drawn — 1 is the sword's own reach.
    private IEnumerator AnimateSwingArc(float shieldAngle, float endHold, float arcDuration,
                                        float reach = 1f)
    {
        float startAngle = shieldAngle - 45f;
        float endAngle = shieldAngle + 45f;
        swordSpriteTransform.gameObject.SetActive(true);
        // Position slash sprite but keep it disabled for now
        float shieldAngleRad = shieldAngle * Mathf.Deg2Rad;
        Vector3 slashPosition = (new Vector3(Mathf.Cos(shieldAngleRad), Mathf.Sin(shieldAngleRad), 0) * 0.6f * reach) + rotationOffset;
        slashSpriteTransform.localPosition = slashPosition;
        slashSpriteTransform.localRotation = Quaternion.Euler(0, 0, shieldAngle - 90f);
        // Position and rotate sword sprite at starting angle
        // The blade is drawn longer, not just swung wider - the sword sprite is
        // rotated so its length runs along local Y, and scaling that also grows
        // the box collider added in AddDamageDetection, so what the player sees
        // and what actually connects stay the same shape.
        Vector3 bladeScale = _bladeBaseScale;
        bladeScale.y = _bladeBaseScale.y * BladeReach;
        swordSpriteTransform.localScale = bladeScale;

        float startAngleRad = startAngle * Mathf.Deg2Rad;
        Vector3 swordStartPos = (new Vector3(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad), 0) * 0.8f * reach) + rotationOffset;
        swordSpriteTransform.localPosition = swordStartPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, startAngle - 90f);

        // Activate slash sprite immediately
        slashSpriteTransform.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / arcDuration;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
            float currentAngleRad = currentAngle * Mathf.Deg2Rad;
            Vector3 swordCurrentPos = (new Vector3(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad), 0) * reach) + rotationOffset;
            swordSpriteTransform.localPosition = swordCurrentPos;
            swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, currentAngle - 90f);
            yield return null;
        }
        float endAngleRad = endAngle * Mathf.Deg2Rad;
        Vector3 swordEndPos = (new Vector3(Mathf.Cos(endAngleRad), Mathf.Sin(endAngleRad), 0) * .8f * reach) + rotationOffset;
        swordSpriteTransform.localPosition = swordEndPos;
        swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, endAngle - 90f);
        yield return new WaitForSeconds(endHold);
        swordSpriteTransform.gameObject.SetActive(false);
        slashSpriteTransform.gameObject.SetActive(false);
    }

    private static readonly Color PhantomTint = new Color(0.45f, 0.4f, 0.75f, 0.75f);

    private void SetSwingTint(Color tint)
    {
        var swordSprite = swordSpriteTransform.GetComponent<SpriteRenderer>();
        if (swordSprite != null) swordSprite.color = tint;
        var slashSprite = slashSpriteTransform.GetComponent<SpriteRenderer>();
        if (slashSprite != null) slashSprite.color = tint;
    }

    // Serpent's Breath: swings can exhale a venom cloud that drifts along the
    // shield facing. Chance/duration/size live on the knight's PoisonTipBoost.
    private void TryExhaleSerpentsBreath()
    {
        if (owningKnight == null || shield == null) return;

        PoisonTipBoost boost = owningKnight.GetComponent<PoisonTipBoost>();
        if (boost == null || !boost.ShouldExhaleSwordCloud()) return;

        Vector2 direction = shield.Direction;
        Vector2 origin = (Vector2)owningKnight.transform.position + direction * 1.3f;
        PoisonCloud.SpawnTraveling(origin, direction, boost.SwordCloudDuration,
            boost.SwordCloudLarge, owningKnight.tag);
    }

    // Firebrand (Ember Order): a swing can hurl a spread of fireballs along the
    // shield facing. Rank II raises the COUNT, not the odds — the rare moment hits
    // harder rather than happening more often, so it stays a payoff you can't fish
    // for and the sword doesn't become a primary fire delivery system.
    private const float FirebrandSpeed = 7f;
    private const float FirebrandLifetime = 4f;
    private const float FirebrandDirectDamageFactor = 1.5f;

    // Rimeblade (Frigid Order): the swing throws off a burst of cold around the
    // knight, dealing the sword's own damage to everything it catches.
    //
    // Every Order hangs a discipline off the sword and each one does something
    // different with it - Serpent exhales a cloud, Shadow echoes the swing, Ember
    // throws ordnance. Frigid's blade is cold iron, and it answers the thing that
    // has ALREADY closed the distance: a body that was chilled by an arrow on the
    // way in gets stopped dead at arm's length, which is the strongest single
    // moment in the Order and costs two picks from different chains to reach.
    //
    // Note what this is not: a projectile. It does not travel — it opens once,
    // one step out along the swing, and is gone. Nudging it a unit down the facing
    // rather than sitting it on the knight's own feet means the half of the circle
    // behind the knight (where nothing the sword just hit can be) stops being
    // wasted, and the burst covers the ground the blade actually swept.
    private const float RimebladeForwardOffset = 1f;

    private void TryReleaseRimeblade()
    {
        if (owningKnight == null) return;

        FrigidBoost boost = owningKnight.GetComponent<FrigidBoost>();
        if (boost == null || !boost.ShouldReleaseRimeblade()) return;

        float radius = boost.RimebladeRadius;
        Vector2 facing = shield != null ? shield.Direction : Vector2.zero;
        Vector2 center = (Vector2)owningKnight.transform.position + facing * RimebladeForwardOffset;
        int damage = EffectiveSwingDamage;

        FrostFx.Burst(center, radius);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.frostChill);
        }

        // A temporary carrier so the damage lands credited to the right knight,
        // the same trick OnEnemyHit already uses for the blade itself - sword
        // objects are untagged children, so the tag has to be built here.
        GameObject carrier = new GameObject("RimebladeBurst");
        carrier.tag = owningKnight.tag + "Projectile";

        Collider2D[] caught = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < caught.Length; i++)
        {
            EnemyBase enemy = caught[i] != null ? caught[i].GetComponent<EnemyBase>() : null;
            if (enemy == null || enemy.IsDead) continue;

            // Damage first, cold second - the same order an arrow uses, and for
            // the same reason. A blow ends a freeze, so chilling first would let
            // one swing stop a body and then break its own ice on the next line.
            // This way a swing shatters what was already frozen and leaves it
            // chilled again, or stops what was already chilled. Never both.
            int blow = EquipmentBoost.ScaleHit(damage, enemy, owningKnight.tag);
            bool shatterHit = enemy.IsFrozen && boost.ShatterMultiplier > 1f;
            if (shatterHit)
            {
                blow = Mathf.CeilToInt(blow * boost.ShatterMultiplier);
            }
            enemy.TakeDamage(blow, carrier);
            // Counted only when this blow actually broke the ice.
            if (shatterHit && !enemy.IsFrozen) PlayerStats.Increment("frigid.shattered");

            // isBlow: true. This is a hit the knight swung for, so it is one of
            // the two things in the game allowed to freeze.
            if (!enemy.IsDead) boost.TouchWithCold(enemy, true);
        }

        Destroy(carrier);
    }

    // Acid Dagger (Serpent capstone): half a second after the swing starts, a small
    // dagger jabs straight out along wherever the shield points at that moment — out,
    // a beat held, back. No arc and no sweep: one short, narrow box that only hurts on
    // the way out and while held, so it reads as a stab rather than a second swing.
    // Its point stops at two thirds of the sword's reach (Long Sword included),
    // measured off the sword's own sprite so the two can never drift apart.
    //
    // Its own coroutine rather than a step in PerformSwordSwing, because Phantom
    // Blade's echoes are still arcing at the half-second mark and the jab must neither
    // wait for them nor share their sprites.
    private const float AcidDaggerDelay = 0.5f;
    private const float AcidDaggerReachFraction = 0.66f;
    private const float AcidDaggerStartFraction = 0.3f; // point starts tucked in by the knight
    private const float AcidDaggerOutSeconds = 0.06f;
    private const float AcidDaggerHoldSeconds = 0.06f;
    private const float AcidDaggerBackSeconds = 0.06f;

    // The poison it leaves is a Venom Tip arrow's (see PoisonProjectile), bent by
    // Virulence the same way
    private const int AcidDaggerPoisonDamage = 2;
    private const float AcidDaggerPoisonSeconds = 30f;
    private const float AcidDaggerPoisonTickRate = 1f;

    // The blade's box in the dagger's own space, measured off acid_dagger.aseprite
    // (9x17 at 32 PPU, centre pivot): the ten rows above the guard, five pixels
    // across. The guard and grip never hurt anything.
    private static readonly Vector2 AcidDaggerBladeSize = new Vector2(5f / 32f, 10f / 32f);
    private static readonly Vector2 AcidDaggerBladeCenter = new Vector2(0f, 3.5f / 32f);
    private const float AcidDaggerTipOffset = 8.5f / 32f; // pivot to the point

    // The stock sword's point, for when its sprite cannot be measured
    private const float FallbackSwordTipReach = 1.375f;

    // Enemy colliders are triggers, so the filter has to ask for them (as ShieldSight does)
    private static readonly Collider2D[] DaggerHits = new Collider2D[32];
    private static readonly ContactFilter2D DaggerFilter = new ContactFilter2D { useTriggers = true };
    private Transform _daggerTransform;
    private SpriteRenderer _daggerRenderer;

    private void TryQueueAcidDagger()
    {
        if (owningKnight == null || shield == null) return;

        PoisonTipBoost boost = owningKnight.GetComponent<PoisonTipBoost>();
        if (boost == null || !boost.AcidDagger) return;

        StartCoroutine(AcidDaggerJab(boost));
    }

    private IEnumerator AcidDaggerJab(PoisonTipBoost boost)
    {
        yield return new WaitForSeconds(AcidDaggerDelay);
        if (shield == null || owningKnight == null) yield break;

        EnsureDagger(boost);

        // Locked at the moment it leaves — a jab is a straight line, so it does not
        // follow the shield round while it is out
        float angle = shield.CurrentAngle;
        float radians = angle * Mathf.Deg2Rad;
        Vector3 facing = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);

        float swordTip = SwordTipReach();
        float tipOffset = AcidDaggerTipOffset * _daggerTransform.localScale.y;
        float from = swordTip * AcidDaggerStartFraction - tipOffset;
        float to = swordTip * AcidDaggerReachFraction - tipOffset;

        _daggerTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        _daggerTransform.localPosition = facing * from + rotationOffset;
        _daggerTransform.gameObject.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.swordSwing);

        var struck = new HashSet<EnemyBase>();
        int damage = boost.AcidDaggerDamage;

        // Out fast and settling at the end of the reach
        float elapsed = 0f;
        while (elapsed < AcidDaggerOutSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / AcidDaggerOutSeconds);
            float eased = 1f - (1f - progress) * (1f - progress);
            _daggerTransform.localPosition = facing * Mathf.Lerp(from, to, eased) + rotationOffset;
            StrikeWithDagger(boost, damage, struck);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < AcidDaggerHoldSeconds)
        {
            elapsed += Time.deltaTime;
            StrikeWithDagger(boost, damage, struck);
            yield return null;
        }

        // Drawn back harmlessly
        elapsed = 0f;
        while (elapsed < AcidDaggerBackSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / AcidDaggerBackSeconds);
            _daggerTransform.localPosition = facing * Mathf.Lerp(to, from, progress) + rotationOffset;
            yield return null;
        }

        _daggerTransform.gameObject.SetActive(false);
    }

    // Built once, beside the blade, and drawn like it: same sorting, same material so
    // the same lights fall on it, and the same scale so its pixels are the sword's size
    private void EnsureDagger(PoisonTipBoost boost)
    {
        if (_daggerTransform == null)
        {
            var dagger = new GameObject("AcidDagger");
            dagger.SetActive(false);
            _daggerTransform = dagger.transform;
            _daggerTransform.SetParent(transform, false);
            _daggerRenderer = dagger.AddComponent<SpriteRenderer>();

            var swordRenderer = swordSpriteTransform != null
                ? swordSpriteTransform.GetComponent<SpriteRenderer>()
                : null;
            if (swordRenderer != null)
            {
                _daggerRenderer.sharedMaterial = swordRenderer.sharedMaterial;
                _daggerRenderer.sortingLayerID = swordRenderer.sortingLayerID;
                _daggerRenderer.sortingOrder = swordRenderer.sortingOrder;
            }
        }

        _daggerTransform.localScale = _bladeBaseScale;
        _daggerRenderer.sprite = boost.AcidDaggerSprite;
    }

    // How far from the swing's centre the sword's point reaches: the arc radius plus
    // the blade above its pivot, both stretched by Long Sword exactly as
    // AnimateSwingArc stretches them
    private float SwordTipReach()
    {
        var swordRenderer = swordSpriteTransform != null
            ? swordSpriteTransform.GetComponent<SpriteRenderer>()
            : null;
        if (swordRenderer == null || swordRenderer.sprite == null)
        {
            return FallbackSwordTipReach * BladeReach;
        }
        return BladeReach * (1f + swordRenderer.sprite.bounds.max.y * _bladeBaseScale.y);
    }

    private void StrikeWithDagger(PoisonTipBoost boost, int damage, HashSet<EnemyBase> struck)
    {
        Vector2 center = _daggerTransform.TransformPoint(AcidDaggerBladeCenter);
        Vector3 scale = _daggerTransform.lossyScale;
        Vector2 size = new Vector2(AcidDaggerBladeSize.x * Mathf.Abs(scale.x),
                                   AcidDaggerBladeSize.y * Mathf.Abs(scale.y));

        int count = Physics2D.OverlapBox(center, size, _daggerTransform.eulerAngles.z, DaggerFilter, DaggerHits);
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = DaggerHits[i] != null ? DaggerHits[i].GetComponent<EnemyBase>() : null;
            if (enemy == null || enemy.IsDead || !struck.Add(enemy)) continue;
            LandDagger(enemy, boost, damage);
        }
    }

    private void LandDagger(EnemyBase enemy, PoisonTipBoost boost, int damage)
    {
        string knightTag = owningKnight.tag;

        // Credited like the blade: the sword objects are untagged, so the carrier
        // wears the knight's projectile tag for the length of the hit
        GameObject carrier = new GameObject("AcidDaggerHit");
        carrier.tag = knightTag + "Projectile";
        enemy.TakeDamage(EquipmentBoost.ScaleHit(damage, enemy, knightTag), carrier);
        Destroy(carrier);

        // Damage first, venom second — the order an arrow uses — so a jab that kills
        // leaves nothing on the body and is not counted as a venom kill
        if (!enemy.IsDead)
        {
            enemy.ApplyPoisonFromTag(
                AcidDaggerPoisonDamage + boost.TickDamageBonus,
                AcidDaggerPoisonSeconds,
                Mathf.Max(0.2f, AcidDaggerPoisonTickRate * boost.TickRateMultiplier),
                knightTag);
        }
    }

    private void TryHurlFirebrand()
    {
        if (owningKnight == null || shield == null) return;

        EmberBoost boost = owningKnight.GetComponent<EmberBoost>();
        if (boost == null || boost.FireballPrefab == null || !boost.ShouldHurlFirebrand()) return;

        // One fireball goes straight down the facing; two straddle it; three do
        // both. Driven off the count rather than off the rank so the spread and
        // the tier can never disagree — Firebrand I hurls a single fireball, and
        // a single fireball fired at -15 degrees would just look like a miss.
        float[] angles;
        if (boost.FirebrandCount >= 3) angles = new float[] { -20f, 0f, 20f };
        else if (boost.FirebrandCount == 2) angles = new float[] { -15f, 15f };
        else angles = new float[] { 0f };

        Vector2 facing = shield.Direction;
        Vector2 origin = (Vector2)owningKnight.transform.position + facing * 1.1f;
        int directDamage = Mathf.Max(1, Mathf.RoundToInt(EffectiveSwingDamage * FirebrandDirectDamageFactor));

        foreach (float angle in angles)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * facing;
            FireballProjectile.Spawn(boost.FireballPrefab, origin, direction, FirebrandSpeed,
                directDamage, EffectiveSwingDamage, boost.FireballBlastRadius, boost,
                owningKnight.tag, FirebrandLifetime);
        }
    }

    private void AddDamageDetection(Transform spriteTransform)
    {
        if (spriteTransform == null) return;
        Collider2D collider = spriteTransform.GetComponent<Collider2D>();
        if (collider == null)
            collider = spriteTransform.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        SwordDamageDetector damageDetector = spriteTransform.gameObject.AddComponent<SwordDamageDetector>();
        damageDetector.Initialize(this, swingDamage);
    }

    // Set by whichever swing phase is currently arcing; read at the end of each
    private bool _phaseLanded;
    private int _phasesLanded;

    /// <summary>
    /// Whether the bumper swings this blade. Only the tutorial lowers it. Lives on
    /// the sword object rather than the knight, so reach it with
    /// GetComponentsInChildren — ShieldOrbit builds the sword at runtime.
    /// See PlayerShooter.InputEnabled.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// Raised the moment a swing lands: the enemy's instance id, and whether that
    /// blow was the one that killed it. An id and not the object because the kill
    /// may already have destroyed it. The tutorial listens so it can tell a sword
    /// kill from an arrow kill and hold the player to the one it asked for.
    /// </summary>
    public static event System.Action<int, bool> OnSwordLanded;

    public void OnEnemyHit(GameObject enemy)
    {
        if (damagedEnemies.Contains(enemy)) return;
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            // Shatter (Frigid): the blade breaks ice like anything else the knight
            // lands. Asked before the damage, because landing it is what breaks it.
            FrigidBoost frigid = owningKnight != null ? owningKnight.GetComponent<FrigidBoost>() : null;
            bool frigidShatter = frigid != null && enemyBase.IsFrozen && frigid.ShatterMultiplier > 1f;

            GameObject tempProjectile = new GameObject("SwordHit");
            // Credit the OWNING knight: the sword object itself is untagged, so
            // using its own tag produced "UntaggedProjectile" and sword kills
            // fed the wrong knight's special
            tempProjectile.tag = (owningKnight != null ? owningKnight.tag : gameObject.tag) + "Projectile";

            // Equipment banes apply to the sword too — an item that says vermin
            // take more damage would read as broken if only arrows honoured it
            int swingDamage = EquipmentBoost.ScaleHit(currentSwingDamage, enemyBase,
                                                      owningKnight != null ? owningKnight.tag : null);

            if (frigidShatter)
            {
                swingDamage = Mathf.CeilToInt(swingDamage * frigid.ShatterMultiplier);
            }

            int enemyId = enemy.GetInstanceID();
            Vector3 hitPoint = enemyBase.transform.position;
            enemyBase.TakeDamage(swingDamage, tempProjectile);

            // The burst and tally belong to the blow that actually broke the ice.
            if (frigidShatter && !enemyBase.IsFrozen)
            {
                FrostFx.Burst(hitPoint, FrigidBoost.SplinterRadius);
                PlayerStats.Increment("frigid.shattered");
            }
            OnSwordLanded?.Invoke(enemyId, enemyBase.IsDead);
            Destroy(tempProjectile);
            damagedEnemies.Add(enemy);
            _phaseLanded = true;
        }
    }

    void OnDisable()
    {
        // A jab cut off mid-thrust stops its coroutine with it, and would otherwise
        // come back standing in the air the next time this is enabled
        if (_daggerTransform != null) _daggerTransform.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (swordInputAction != null)
            swordInputAction.Disable();
    }
}

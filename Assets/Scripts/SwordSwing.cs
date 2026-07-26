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
    private Transform slashSpriteTransform;
    private HashSet<GameObject> damagedEnemies;
    private ShieldOrbit shield;
    private GameObject owningKnight;

    void Awake()
    {
        swordSpriteTransform = transform.Find("Sword");
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
        if (canSwing && shield != null && swordInputAction != null && swordInputAction.WasPressedThisFrame())
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.swordSwing);
            StartCoroutine(PerformSwordSwing());
        }
    }

    private IEnumerator PerformSwordSwing()
    {
        canSwing = false;
        damagedEnemies = new HashSet<GameObject>();
        currentSwingDamage = swingDamage;
        TryExhaleSerpentsBreath();
        TryHurlFirebrand();
        if (swordSpriteTransform == null || slashSpriteTransform == null)
        {
            canSwing = true;
            yield break;
        }
        float shieldAngle = shield.CurrentAngle;
        yield return StartCoroutine(AnimateSwingArc(shieldAngle, totalEffectDuration - swingDuration, swingDuration));

        // Phantom Blade (Shadow Order): dark after-images repeat the swing. They
        // trail the real swing after a short beat, then whip through at 2x speed.
        NinjaBoost ninja = owningKnight != null ? owningKnight.GetComponent<NinjaBoost>() : null;
        int echoCount = ninja != null ? ninja.PhantomLevel : 0;
        for (int i = 0; i < echoCount; i++)
        {
            yield return new WaitForSeconds(0.1f);
            damagedEnemies = new HashSet<GameObject>();
            currentSwingDamage = Mathf.Max(1, swingDamage / 2);
            SetSwingTint(PhantomTint);
            AudioManager.Instance.PlaySFX(AudioManager.Instance.phantomStrike);
            yield return StartCoroutine(AnimateSwingArc(shieldAngle, 0.025f, swingDuration * 0.5f));
            SetSwingTint(Color.white);
        }
        currentSwingDamage = swingDamage;

        yield return new WaitForSeconds(cooldownTime);
        canSwing = true;
    }

    // One full swing animation pass (sprites on -> arc -> hold -> sprites off);
    // shared by the real swing and its Phantom Blade echoes
    private IEnumerator AnimateSwingArc(float shieldAngle, float endHold, float arcDuration)
    {
        float startAngle = shieldAngle - 45f;
        float endAngle = shieldAngle + 45f;
        swordSpriteTransform.gameObject.SetActive(true);
        // Position slash sprite but keep it disabled for now
        float shieldAngleRad = shieldAngle * Mathf.Deg2Rad;
        Vector3 slashPosition = (new Vector3(Mathf.Cos(shieldAngleRad), Mathf.Sin(shieldAngleRad), 0) * 0.6f) + rotationOffset;
        slashSpriteTransform.localPosition = slashPosition;
        slashSpriteTransform.localRotation = Quaternion.Euler(0, 0, shieldAngle - 90f);
        // Position and rotate sword sprite at starting angle
        float startAngleRad = startAngle * Mathf.Deg2Rad;
        Vector3 swordStartPos = (new Vector3(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad), 0) * 0.8f) + rotationOffset;
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
            Vector3 swordCurrentPos = (new Vector3(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad), 0) * 1f) + rotationOffset;
            swordSpriteTransform.localPosition = swordCurrentPos;
            swordSpriteTransform.localRotation = Quaternion.Euler(0, 0, currentAngle - 90f);
            yield return null;
        }
        float endAngleRad = endAngle * Mathf.Deg2Rad;
        Vector3 swordEndPos = (new Vector3(Mathf.Cos(endAngleRad), Mathf.Sin(endAngleRad), 0) * .8f) + rotationOffset;
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

    private void TryHurlFirebrand()
    {
        if (owningKnight == null || shield == null) return;

        EmberBoost boost = owningKnight.GetComponent<EmberBoost>();
        if (boost == null || boost.FireballPrefab == null || !boost.ShouldHurlFirebrand()) return;

        float[] angles = boost.FirebrandCount >= 3
            ? new float[] { -20f, 0f, 20f }
            : new float[] { -15f, 15f };

        Vector2 facing = shield.Direction;
        Vector2 origin = (Vector2)owningKnight.transform.position + facing * 1.1f;
        int directDamage = Mathf.Max(1, Mathf.RoundToInt(swingDamage * FirebrandDirectDamageFactor));

        foreach (float angle in angles)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * facing;
            FireballProjectile.Spawn(boost.FireballPrefab, origin, direction, FirebrandSpeed,
                directDamage, swingDamage, boost.FireballBlastRadius, boost,
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

    public void OnEnemyHit(GameObject enemy)
    {
        if (damagedEnemies.Contains(enemy)) return;
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            GameObject tempProjectile = new GameObject("SwordHit");
            // Credit the OWNING knight: the sword object itself is untagged, so
            // using its own tag produced "UntaggedProjectile" and sword kills
            // fed the wrong knight's special
            tempProjectile.tag = (owningKnight != null ? owningKnight.tag : gameObject.tag) + "Projectile";
            enemyBase.TakeDamage(currentSwingDamage, tempProjectile);
            Destroy(tempProjectile);
            damagedEnemies.Add(enemy);
        }
    }

    void OnDestroy()
    {
        if (swordInputAction != null)
            swordInputAction.Disable();
    }
}

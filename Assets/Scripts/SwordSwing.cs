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
    private Transform swordSpriteTransform;
    private Transform slashSpriteTransform;
    private HashSet<GameObject> damagedEnemies;
    private ShieldOrbit shield;

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
        if (swordSpriteTransform == null || slashSpriteTransform == null)
        {
            canSwing = true;
            yield break;
        }
        float shieldAngle = shield.CurrentAngle;
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
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / swingDuration;
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
        yield return new WaitForSeconds(totalEffectDuration - swingDuration);
        swordSpriteTransform.gameObject.SetActive(false);
        slashSpriteTransform.gameObject.SetActive(false);
        yield return new WaitForSeconds(cooldownTime);
        canSwing = true;
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
            tempProjectile.tag = gameObject.tag + "Projectile";
            enemyBase.TakeDamage(swingDamage, tempProjectile);
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

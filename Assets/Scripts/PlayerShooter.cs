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

        // Create projectile
        Vector2 spawnPosition = shield.transform.position;
        Quaternion spawnRotation = Quaternion.Euler(0, 0, shield.CurrentAngle);
        GameObject projectile = Instantiate(playerProjectilePrefab, spawnPosition, spawnRotation);
        projectile.tag = gameObject.tag + "Projectile";

        // Set velocity
        projectile.GetComponent<Rigidbody2D>().linearVelocity = shield.Direction * projectileSpeed;

        // Start lifetime countdown
        StartCoroutine(DestroyProjectile(projectile));

        yield return new WaitForSeconds(cooldownTime);
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
}
using UnityEngine;

public class ProjectileSettings : MonoBehaviour
{
    [SerializeField] public int damage = 10;

    void Start()
    {
        // Register this projectile with the wave tracking system
        BaseWave.RegisterProjectile(gameObject);
    }

    void OnDestroy()
    {
        // Unregister this projectile when it's destroyed
        BaseWave.UnregisterProjectile(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check for shield collision
        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(1);
            }
            Destroy(gameObject);
            return;
        }

        // Check for player collision
        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.playerHurt);
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
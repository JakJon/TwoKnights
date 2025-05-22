using UnityEngine;

public class ProjectileSettings : MonoBehaviour
{
    [SerializeField] public int damage = 10;

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check for shield collision
        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(3);
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
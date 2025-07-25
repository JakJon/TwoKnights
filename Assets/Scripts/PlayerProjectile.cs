using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;

    private void Start()
    {
        // Apply damage bonus from the player that fired this projectile
        GameObject player = gameObject.CompareTag("PlayerLeftProjectile")
            ? GameObject.FindWithTag("PlayerLeft")
            : GameObject.FindWithTag("PlayerRight");

        if (player != null)
        {
            DamageBoost damageBoost = player.GetComponent<DamageBoost>();
            if (damageBoost != null)
            {
                damage += damageBoost.GetDamageBonus();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to get any enemy that inherits from EnemyBase
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, gameObject);
            Destroy(gameObject);
            return;
        }

        // Fallback for enemies that haven't been migrated to EnemyBase yet
        // Handle Slime damage
        EnemySlime slime = other.GetComponent<EnemySlime>();
        if (slime != null)
        {
            slime.TakeDamage(damage);
            GiveSpecialToPlayer(5);
            Destroy(gameObject);
            return;
        }
    }

    private void GiveSpecialToPlayer(int amount)
    {
        GameObject player = gameObject.CompareTag("PlayerLeftProjectile")
            ? GameObject.FindWithTag("PlayerLeft")
            : GameObject.FindWithTag("PlayerRight");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(amount);
            }
        }
    }
}
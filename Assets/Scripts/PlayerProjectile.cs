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

        // Handle Bat damage
        EnemyBat bat = other.GetComponent<EnemyBat>();
        if (bat != null)
        {
            bat.TakeDamage(damage);
            GiveSpecialToPlayer(5);
            Destroy(gameObject);
            return;
        }

        // Handle Rat damage
        EnemyRat rat = other.GetComponent<EnemyRat>();
        if (rat != null)
        {
            float oldHealth = rat.GetType().GetField("Health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(rat) as float? ?? 0f;
            
            // Apply damage using reflection since Health is private
            var healthField = rat.GetType().GetField("Health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (healthField != null)
            {
                float currentHealth = (float)healthField.GetValue(rat);
                float newHealth = currentHealth - damage;
                healthField.SetValue(rat, newHealth);

                // Give special based on whether enemy died
                if (newHealth <= 0)
                {
                    GiveSpecialToPlayer(20);
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.ratDeath);
                    Destroy(rat.gameObject);
                }
                else
                {
                    GiveSpecialToPlayer(10);
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.ratHurt);
                }
            }
            
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
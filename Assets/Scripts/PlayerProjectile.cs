using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;
    public NinjaBoost ownerNinjaBoost; // Set by PlayerShooter at spawn; drives Killing Blow

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to get any enemy that inherits from EnemyBase
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            int damageToDeal = damage;

            // Killing Blow (Shadow Order): finish weakened enemies outright.
            // Never fires on bosses — skipping a fraction of a boss bar would
            // trivialize the fight.
            if (ownerNinjaBoost != null && ownerNinjaBoost.ExecuteThreshold > 0f
                && !(enemy is EnemyRatKing) && !enemy.IsDead
                && enemy.GetHealth() <= enemy.GetMaxHealth() * ownerNinjaBoost.ExecuteThreshold)
            {
                damageToDeal = Mathf.Max(damage, Mathf.CeilToInt(enemy.GetHealth()));
                ShadowFx.ExecuteFlash(enemy.transform.position);
            }

            // Apply normal damage
            enemy.TakeDamage(damageToDeal, gameObject);
            
            // Check if this projectile has poison and apply it
            PoisonProjectile poisonComponent = GetComponent<PoisonProjectile>();
            if (poisonComponent != null)
            {
                poisonComponent.ApplyPoisonToEnemy(enemy, gameObject);
            }
            
            Destroy(gameObject);
            return;
        }

        // No more fallback code needed - all enemies have been migrated to EnemyBase
    }
}
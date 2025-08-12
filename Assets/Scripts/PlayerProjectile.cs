using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to get any enemy that inherits from EnemyBase
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            // Apply normal damage
            enemy.TakeDamage(damage, gameObject);
            
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
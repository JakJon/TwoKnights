using UnityEngine;

public class SwordDamageDetector : MonoBehaviour
{
    private SwordSwing swordSwing;
    private int damage;
    
    public void Initialize(SwordSwing swordSwing, int damage)
    {
        this.swordSwing = swordSwing;
        this.damage = damage;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider belongs to an enemy
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null && swordSwing != null)
        {
            swordSwing.OnEnemyHit(other.gameObject);
        }
    }
}

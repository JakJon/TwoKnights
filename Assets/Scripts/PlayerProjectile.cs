using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemySlime slime = other.GetComponent<EnemySlime>();
        if (slime != null)
        {
            slime.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
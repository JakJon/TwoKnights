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
            
            GameObject player = gameObject.CompareTag("PlayerLeftProjectile")
                ? GameObject.FindWithTag("PlayerLeft")
                : GameObject.FindWithTag("PlayerRight");

            if (player != null)
            {
                PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
                if (playerSpecial != null)
                {
                    playerSpecial.updateSpecial(5);
                }
            }
            
            Destroy(gameObject);
            return;
        }

        EnemyBat bat = other.GetComponent<EnemyBat>();
        if (bat != null)
        {
            bat.TakeDamage(damage);
            
            GameObject player = gameObject.CompareTag("PlayerLeftProjectile")
                ? GameObject.FindWithTag("PlayerLeft")
                : GameObject.FindWithTag("PlayerRight");

            if (player != null)
            {
                PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
                if (playerSpecial != null)
                {
                    playerSpecial.updateSpecial(5);
                }
            }
            
            Destroy(gameObject);
        }
    }
}
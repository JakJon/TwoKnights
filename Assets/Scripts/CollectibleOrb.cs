using UnityEngine;

public class CollectibleOrb : MonoBehaviour
{
    public enum OrbType
    {
        Health,
        Mana
    }

    [SerializeField] private OrbType orbType;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private int healthRestoreAmount = 20;
    [SerializeField] private int manaRestoreAmount = 10;

    private Vector2 startPos;
    private Vector2 endPos;
    private Vector2 moveDir;
    private float totalDistance;

    public void Initialize(Vector2 start, Vector2 end)
    {
        startPos = start;
        endPos = end;
        transform.position = startPos;
        moveDir = (endPos - startPos).normalized;
        totalDistance = Vector2.Distance(startPos, endPos);
    }

    private void Update()
    {
        float moveStep = moveSpeed * Time.deltaTime;
        transform.position += (Vector3)(moveDir * moveStep);
        if (Vector2.Distance(transform.position, startPos) >= totalDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            GameObject player = other.CompareTag("PlayerLeftProjectile")
                ? GameObject.FindWithTag("PlayerLeft")
                : GameObject.FindWithTag("PlayerRight");

            if (player != null)
            {
                if (orbType == OrbType.Health)
                {
                    PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(healthRestoreAmount);
                    }
                }
                else // Mana
                {
                    PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
                    if (playerSpecial != null)
                    {
                        playerSpecial.updateSpecial(manaRestoreAmount);
                    }
                }
            }

            Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }
}
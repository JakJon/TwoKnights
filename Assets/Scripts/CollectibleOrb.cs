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
        Debug.Log($"Initializing CollectibleOrb: Type={orbType}, Start={start}, End={end}");
        startPos = start;
        endPos = end;
        transform.position = startPos;
        moveDir = (endPos - startPos).normalized;
        totalDistance = Vector2.Distance(startPos, endPos);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.orbFlyBy);
    }

    private void Update()
    {
        float moveStep = moveSpeed * Time.deltaTime;
        transform.position += (Vector3)(moveDir * moveStep);
        if (Vector2.Distance(transform.position, startPos) >= totalDistance)
        {
            Destroy(gameObject);
            AudioManager.Instance.StopSFX();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"CollectibleOrb collided with: {other.gameObject.name}, Tag: {other.gameObject.tag}");
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            GameObject player = other.CompareTag("PlayerLeftProjectile")
                ? GameObject.FindWithTag("PlayerLeft")
                : GameObject.FindWithTag("PlayerRight");

            Destroy(gameObject);

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
                        playerSpecial.AddSpecialFromOrb(manaRestoreAmount);
                    }
                }
            }

            AudioManager.Instance.PlaySFX(AudioManager.Instance.orbCollect);

        }
    }
}
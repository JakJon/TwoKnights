using UnityEngine;

// The dark bat's sonar shot: a spinning spiral that flies straight at the
// knight it was aimed at. Blocking it with the shield, slashing it with the
// sword, or shooting it with an arrow destroys it harmlessly; if it reaches
// the knight they are Confused (reversed shield controls) — it deals no
// contact damage.
public class SonarWave : MonoBehaviour
{
    [SerializeField] private float speed = 1.25f;
    [SerializeField] private float spinDegreesPerSecond = 360f;
    [SerializeField] private float confusionDuration = 5f;
    [SerializeField] private Sprite confusionIconSprite;

    private Vector2 _direction = Vector2.left;

    public void Initialize(Vector2 origin, Transform target)
    {
        transform.position = origin;
        if (target != null)
        {
            // Aim at chest height like enemy arrows do
            Vector2 targetCenter = (Vector2)target.position + new Vector2(0f, 0.5f);
            _direction = (targetCenter - origin).normalized;
        }
    }

    private void Start()
    {
        BaseWave.RegisterProjectile(gameObject);
    }

    private void OnDestroy()
    {
        BaseWave.UnregisterProjectile(gameObject);
    }

    private void Update()
    {
        transform.Translate(_direction * speed * Time.deltaTime, Space.World);
        transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

        // Off-field cleanup so a missed sonar can't stall the wave forever
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) > 14f || Mathf.Abs(p.y) > 9f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Shield block: combo-safe, feeds special like any blocked projectile
        if (other.CompareTag("Shield"))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
            }
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(1);
            }
            Destroy(gameObject);
            return;
        }

        // Shot down by an arrow (both are spent)
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // Slashed by the sword (sword/slash hitboxes are untagged but carry
        // SwordDamageDetector; its own handler ignores non-EnemyBase objects)
        if (other.GetComponent<SwordDamageDetector>() != null)
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.playerHurt);
            }
            ConfusionEffect.Apply(other.gameObject, confusionDuration, confusionIconSprite);
            Destroy(gameObject);
        }
    }
}

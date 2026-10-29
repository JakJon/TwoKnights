using UnityEngine;

// The dark bat's sonar shot: a spinning spiral that flies straight at the
// knight it was aimed at. Blocking it with the shield or shooting it with an
// arrow destroys it harmlessly (the arrow flies on); slashing it with the sword
// bats it back down the line the swing faces, where it hits an enemy for
// returnedDamage. If it reaches the knight they are Confused (reversed shield
// controls) — it deals no contact damage.
public class SonarWave : MonoBehaviour
{
    [SerializeField] private float speed = 1.25f;
    [SerializeField] private float spinDegreesPerSecond = 360f;
    [SerializeField] private float confusionDuration = 5f;
    [SerializeField] private Sprite confusionIconSprite;

    [Tooltip("What it hits an enemy for once a sword has batted it back. It has no damage of its own on the way in, so this is a plain arrow's (owner, 2026-10-15). A Reflector rank multiplies it.")]
    [SerializeField] private int returnedDamage = 10;

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
        // Unity still sends trigger messages to a DISABLED behaviour, and switching
        // this one off is how a batted sonar becomes the knight's. Without this it
        // could still confuse a knight it passed on the way out.
        if (!enabled) return;

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

        // Shot down by an arrow. The arrow flies on through (owner, 2026-10-15): it
        // is spent on bodies, not on the enemy's ammunition.
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(gameObject);
            return;
        }

        // Slashed by the sword (sword/slash hitboxes are untagged but carry
        // SwordDamageDetector; its own handler ignores non-EnemyBase objects).
        // Batted back the way the swing faces. It stops spinning: ReflectedShot
        // flies along its rotation, so the spiral now points where it is going.
        GuardianReflect.Turn bat = GuardianReflect.TryBat(other, transform.position);
        if (bat.Happened)
        {
            enabled = false;
            ReflectedShot.Attach(gameObject, bat, speed, returnedDamage);
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            // Sonar confuses rather than damages — the confusion warble is the feedback
            ConfusionEffect.Apply(other.gameObject, confusionDuration, confusionIconSprite);
            Destroy(gameObject);
        }
    }
}

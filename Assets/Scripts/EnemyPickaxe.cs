using UnityEngine;

// A pickaxe hurled from a mine cart at the nearer knight, tumbling end over end.
//
// It flies a STOUT arc — enough loft to read as thrown rather than fired, not so
// much that it hangs. That shape comes from solving for the launch velocity that
// puts it on the knight in a fixed flight time under a fixed gravity, so the arc
// looks the same whether the cart is directly overhead or right at the edge of
// the frame. Tuning `flightTime` changes how lazy the throw is; tuning
// `gravity` changes how high it peaks on the way.
//
// One hit point, like the bomb: anything the knights hold knocks it out of the
// air. Unlike the bomb it has no blast — it either lands on a knight or it does
// not, so the answer is the shield as much as the bow.
public class EnemyPickaxe : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Seconds from the throw to arriving on target. Shorter = flatter, meaner.")]
    [SerializeField] private float flightTime = 1.1f;

    [Tooltip("Downward acceleration. Higher makes the same flight time peak higher.")]
    [SerializeField] private float gravity = 16f;

    [Tooltip("Degrees per second of tumble. Signed by the direction of travel.")]
    [SerializeField] private float spinSpeed = 540f;

    [Tooltip("Killed this long after the throw no matter what, so a miss cannot linger")]
    [SerializeField] private float maxLifetime = 6f;

    [Header("Damage")]
    [SerializeField] private int knightDamage = 12;

    [Tooltip("Shown on the death screen")]
    [SerializeField] private string sourceName = "a Gnome's Pickaxe";

    private Vector2 _velocity;
    private float _spin;
    private bool _spent;

    /// <summary>
    /// Throw one at <paramref name="target"/>. Solves the launch velocity rather
    /// than taking one, so callers only have to say where the cart is and who it
    /// is aiming at — the arc is the prefab's business.
    /// </summary>
    public static EnemyPickaxe Throw(GameObject prefab, Vector3 from, Vector3 target)
    {
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, from, Quaternion.identity);
        EnemyPickaxe axe = go.GetComponent<EnemyPickaxe>();
        if (axe == null) axe = go.AddComponent<EnemyPickaxe>();

        axe.Launch(target);
        return axe;
    }

    private void Launch(Vector3 target)
    {
        float t = Mathf.Max(0.05f, flightTime);
        Vector2 delta = target - transform.position;

        // Constant acceleration, solved backwards: horizontal is just distance over
        // time, and vertical adds back the half-g-t-squared the fall will eat. The
        // arc's height is therefore whatever `gravity` implies, and the pickaxe is
        // guaranteed to be AT the knight when the clock runs out.
        _velocity = new Vector2(delta.x / t, delta.y / t + 0.5f * gravity * t);

        // Tumbles the way it travels, so a throw to the left spins anticlockwise
        _spin = _velocity.x >= 0f ? -spinSpeed : spinSpeed;

        Destroy(gameObject, maxLifetime);
    }

    private void Start()
    {
        // A wave is not over while its ordnance is still in the air — the same
        // bookkeeping ProjectileSettings and EnemyFireball do
        BaseWave.RegisterProjectile(gameObject);
    }

    private void OnDestroy()
    {
        BaseWave.UnregisterProjectile(gameObject);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        _velocity.y -= gravity * dt;
        transform.position += (Vector3)(_velocity * dt);
        transform.Rotate(0f, 0f, _spin * dt);

        // Well below the knights and still going: nothing left to hit
        if (transform.position.y < -9f) Destroy(gameObject);
    }

    // Every tool the knights hold knocks it down, which is what "one hit point"
    // means here. Mirrors EnemyFireball.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_spent) return;

        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
            Spend();
            return;
        }

        // The sword object is untagged by design (see SwordSwing); the detector it
        // grows at swing time is the reliable handle
        if (other.GetComponent<SwordDamageDetector>() != null)
        {
            Spend();
            return;
        }

        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.projectileShield);
            other.GetComponentInParent<PlayerSpecial>()?.updateSpecial(1);
            Spend();
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            other.GetComponent<PlayerHealth>()?.TakeDamage(knightDamage, sourceName);
            Spend();
        }
    }

    private void Spend()
    {
        if (_spent) return;
        _spent = true;
        Destroy(gameObject);
    }
}

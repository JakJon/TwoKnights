using UnityEngine;

// Deliberately NOT IChillable: no rank of Glacial Ward slows a rock (owner,
// 2026-10-15). The ward still slows the other enemy ammunition — bombs,
// fireballs, pickaxes — which state that on themselves.
public class ProjectileMovement : MonoBehaviour, IIncoming
{
    private Transform _target;
    [SerializeField] private float _speed = .75f;

    // Bosses read this to pace volleys so overlapping fans stay blockable
    public float Speed => _speed;

    // Call this when spawning the projectile
    public void Initialize(Transform target, Vector2 spawnPosition)
    {
        Initialize(target, spawnPosition, 0f);
    }

    /// <summary>
    /// Spawn at <paramref name="speed"/> world units per second; 0 or less keeps
    /// the prefab's own. A per-wave override rather than a prefab change because
    /// the rock is shared by every wave that works a shaft — the time a rock
    /// spends in the air is a property of the WAVE that fired it, and Delivery in
    /// particular is asking the player to do arithmetic about carts while it is
    /// falling, so its rocks need to clear off sooner than the mine's do.
    /// </summary>
    public void Initialize(Transform target, Vector2 spawnPosition, float speed)
    {
        _target = target;
        if (speed > 0f) _speed = speed;
        transform.position = spawnPosition; // Set spawn position
        FaceTarget();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileSpawn);
    }

    /// <summary>
    /// Send this rock somewhere else, faster. Used by the Guardian Order's Reflector
    /// when a guard turns a rock around.
    ///
    /// Heading is a ROTATION write and nothing else, because Update translates along
    /// this transform's own local +X — there is no velocity to reverse. Same reason
    /// MirrorPane.WriteFlight sets the rotation for this family and only touches a
    /// Rigidbody2D when the projectile actually has one.
    /// </summary>
    public void Redirect(Vector2 direction, float speedMultiplier)
    {
        if (direction.sqrMagnitude < 1e-6f) return;

        float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(degrees, Vector3.forward);
        _speed *= Mathf.Max(0.01f, speedMultiplier);
    }

    // In the IncomingLedger for as long as it flies, so a fire ogre about to throw
    // can see that this rock is due at a knight and hold its fireball clear of it.
    void OnEnable() { IncomingLedger.Add(this); }
    void OnDisable() { IncomingLedger.Remove(this); }

    public Transform IncomingTarget => _target;

    public float SecondsToArrival
    {
        get
        {
            if (_target == null) return float.PositiveInfinity;
            Vector2 aim = _target.position + new Vector3(0f, 0.5f, 0f);
            return IncomingLedger.StraightEta(transform.position, transform.right, aim, _speed);
        }
    }

    void Update()
    {
        // Move in a straight line toward the initial target direction
        transform.Translate(Vector2.right * _speed * Time.deltaTime);
    }

    void FaceTarget()
    {
        if (_target == null) return;

        // Point the projectile at the target center (add y offset)
        Vector3 targetCenter = _target.position + new Vector3(0, 0.5f, 0);
        Vector2 direction = (targetCenter - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
using UnityEngine;

public class ProjectileMovement : MonoBehaviour
{
    private Transform _target;
    [SerializeField] private float _speed = .75f;

    // Call this when spawning the projectile
    public void Initialize(Transform target, Vector2 spawnPosition)
    {
        _target = target;
        transform.position = spawnPosition; // Set spawn position
        FaceTarget();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileSpawn);
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
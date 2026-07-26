using UnityEngine;

// Bolted onto a fireball at spawn (same pattern as PoisonProjectile): the prefab
// carries the sprite and collider identity, this carries the per-shot state.
//
// A fireball is a SANCTIONED IGNITION SOURCE — one of only two, alongside an arrow
// that rolled ignite. See Docs/Design/ember-order.md.
public class FireballProjectile : MonoBehaviour
{
    public float blastRadius = 1.5f;
    public int blastDamage = 10;
    public EmberBoost ownerBoost;
    public string ownerTag; // "PlayerLeft" / "PlayerRight"

    private bool _exploded;

    // Standalone spawn for fireballs that aren't the main shot — Firebrand's sword
    // toss. PlayerShooter configures its own in place, since there the fireball
    // replaces the arrow rather than being an extra projectile.
    public static void Spawn(GameObject prefab, Vector2 position, Vector2 direction, float speed,
        int directDamage, int blastDamage, float blastRadius, EmberBoost ownerBoost,
        string ownerTag, float lifetime)
    {
        if (prefab == null) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        go.tag = ownerTag + "Projectile";

        Rigidbody2D body = go.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = direction.normalized * speed;
        }

        PlayerProjectile projectile = go.GetComponent<PlayerProjectile>();
        if (projectile != null)
        {
            projectile.damage = directDamage;
            projectile.ignitesOnHit = true;
        }

        FireballProjectile fireball = go.AddComponent<FireballProjectile>();
        fireball.blastRadius = blastRadius;
        fireball.blastDamage = Mathf.Max(1, blastDamage);
        fireball.ownerBoost = ownerBoost;
        fireball.ownerTag = ownerTag;

        AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballLaunch);

        Destroy(go, lifetime);
    }

    // directHit is excluded from the blast: it already ate the direct-hit damage,
    // and paying it both would make a point-blank fireball land 2.5x instead of 1.5x
    public void Explode(EnemyBase directHit)
    {
        if (_exploded) return;
        _exploded = true;

        AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballExplode);

        Vector2 center = transform.position;

        // Damage covers a hair past the drawn blast so enemies at the visible edge
        // aren't skipped; FireFx.Burst below keeps the graphic at blastRadius.
        foreach (var col in Physics2D.OverlapCircleAll(center, blastRadius * FireField.HitboxScale))
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || enemy == directHit || enemy.IsDead) continue;

            enemy.TakeDamage(blastDamage, gameObject);
            enemy.Ignite(ownerTag);
        }

        // The crater: this is the fireball's real contribution to the Order
        if (ownerBoost != null)
        {
            ownerBoost.PlaceZone(center, EmberBoost.CraterRadius, EmberBoost.CraterDuration);
        }

        FireFx.Burst(center, blastRadius);
    }
}

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
    private bool _burnedOut;

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

        // Not Destroy(go, lifetime): a timed destroy is indistinguishable from any
        // other, and OnDestroy below has to be able to tell burning out from
        // hitting something
        fireball.StartCoroutine(fireball.BurnOutAfter(lifetime));
    }

    /// <summary>
    /// The fireball reached the end of its flight without touching anything. Say
    /// so before destroying it, or OnDestroy reads the expiry as a hit and bursts
    /// it wherever it happened to be — which is off-screen, four seconds after
    /// the shot, as a bang with nothing behind it.
    /// </summary>
    public void MarkBurnedOut()
    {
        _burnedOut = true;
    }

    private System.Collections.IEnumerator BurnOutAfter(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        MarkBurnedOut();
        Destroy(gameObject);
    }

    /// <summary>
    /// Anything that takes a fireball off the board sets it off. A fireball is
    /// not an arrow — it is a thing carrying a blast, a crater and an ignition,
    /// and every one of those is owed to the player whether the shot ended on a
    /// rat, a pickaxe knocked out of the air, a bomb, a slime's ward, or an orb.
    ///
    /// It lives in OnDestroy rather than in a collision handler because the
    /// fireball is almost never the thing that ends the collision: a pickaxe
    /// destroys the arrow that hit it, a bomb does, a cart does, a sonar wave
    /// does, and not one of them knows or should know what it just swallowed. The
    /// only place that can be sure of catching all of them — and the ones nobody
    /// has written yet — is the moment the object goes.
    ///
    /// The direct-hit path in PlayerProjectile still calls Explode() itself, so
    /// the enemy it landed on is correctly left out of its own blast; this only
    /// ever fires for the endings nothing else claimed.
    /// </summary>
    private void OnDestroy()
    {
        if (_exploded || _burnedOut) return;

        // Leaving play, changing scene, or the editor stopping: the whole board is
        // being torn down, and a blast that spawns a crater and a particle burst
        // in the middle of it is at best noise and at worst an error per fireball
        if (!Application.isPlaying || !gameObject.scene.isLoaded) return;

        Explode(null);
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

            // Splash, so iron shrugs it off — a cart is only answered by an aimed
            // shot, and the direct hit above is exempt from this check
            if (enemy.ImmuneToAreaDamage) continue;

            enemy.TakeDamage(EquipmentBoost.ScaleHit(blastDamage, enemy, ownerTag), gameObject);
            enemy.Ignite(ownerTag);
        }

        // The crater: this is the fireball's real contribution to the Order. Burning
        // ground for twice as long as it used to burn, and ground that lights NOTHING.
        // The blast above still ignites everything it catches, because that is the
        // fireball itself landing on them; what the mark it leaves does is deal damage
        // to whatever stands in it, for twelve seconds, or forever under Scorched
        // Earth.
        if (ownerBoost != null)
        {
            ownerBoost.PlaceZone(center, EmberBoost.CraterRadius, EmberBoost.CraterDuration);
        }

        FireFx.Burst(center, blastRadius);
    }
}

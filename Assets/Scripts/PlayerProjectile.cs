using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;
    public NinjaBoost ownerNinjaBoost; // Set by PlayerShooter at spawn; drives Killing Blow

    // Ember: set at spawn when this shot's ignite roll succeeded. Together with
    // FireballProjectile these are the ONLY ways an enemy can be lit — burning
    // ground never ignites (see Docs/Design/ember-order.md).
    public bool ignitesOnHit;

    // A main arrow flying through fire BECOMES an ignited arrow, and through a poison
    // cloud a poisoned arrow — picking up that carrier just as if it had rolled it.
    // Shadow arrows and shurikens set this false so only the knight's main shot
    // absorbs the field (see PlayerShooter).
    public bool absorbsFieldEffects = true;

    // "PlayerLeftProjectile" -> "PlayerLeft"
    private string OwnerTag
    {
        get
        {
            if (CompareTag("PlayerLeftProjectile")) return "PlayerLeft";
            if (CompareTag("PlayerRightProjectile")) return "PlayerRight";
            return null;
        }
    }

    // Field pickup: the arrow gains fire/poison from any zone or cloud it passes
    // through, so it lands as an ignited/poisoned arrow. Polls the same fields
    // enemies sample — clouds and fire zones have no colliders to trigger on.
    private void Update()
    {
        if (!absorbsFieldEffects) return;

        Vector2 pos = transform.position;

        // Fire zone -> ignited arrow (nothing to do if it already carries ignite)
        if (!ignitesOnHit)
        {
            float dps;
            string zoneOwner;
            if (FireField.Sample(pos, out dps, out zoneOwner))
            {
                ignitesOnHit = true;
                FireFx.AttachArrowTrail(gameObject);
            }
        }

        // Poison cloud -> poisoned arrow (skip if it already carries poison). It
        // becomes the poisoned arrow this knight would fire, inheriting Virulence.
        if (PoisonCloud.SampleAt(pos) && GetComponent<PoisonProjectile>() == null)
        {
            PoisonProjectile poison = gameObject.AddComponent<PoisonProjectile>();
            GameObject owner = string.IsNullOrEmpty(OwnerTag) ? null : GameObject.FindWithTag(OwnerTag);
            PoisonTipBoost boost = owner != null ? owner.GetComponent<PoisonTipBoost>() : null;
            if (boost != null)
            {
                poison.ConfigureFromBoost(boost);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to get any enemy that inherits from EnemyBase
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            int damageToDeal = damage;

            // Killing Blow (Shadow Order): finish weakened enemies outright.
            // Never fires on bosses — skipping a fraction of a boss bar would
            // trivialize the fight.
            if (ownerNinjaBoost != null && ownerNinjaBoost.ExecuteThreshold > 0f
                && !(enemy is EnemyRatKing) && !enemy.IsDead
                && enemy.GetHealth() <= enemy.GetMaxHealth() * ownerNinjaBoost.ExecuteThreshold)
            {
                damageToDeal = Mathf.Max(damage, Mathf.CeilToInt(enemy.GetHealth()));
                ShadowFx.ExecuteFlash(enemy.transform.position);
            }

            // Apply normal damage
            enemy.TakeDamage(damageToDeal, gameObject);
            
            // Check if this projectile has poison and apply it
            PoisonProjectile poisonComponent = GetComponent<PoisonProjectile>();
            if (poisonComponent != null)
            {
                poisonComponent.ApplyPoisonToEnemy(enemy, gameObject);
            }

            // Ember: light the target, then detonate if this was a fireball. The
            // direct-hit enemy is passed through so the blast doesn't pay it twice.
            if (ignitesOnHit)
            {
                enemy.Ignite(OwnerTag);
            }

            FireballProjectile fireball = GetComponent<FireballProjectile>();
            if (fireball != null)
            {
                fireball.Explode(enemy);
            }

            Destroy(gameObject);
            return;
        }

        // No more fallback code needed - all enemies have been migrated to EnemyBase
    }
}
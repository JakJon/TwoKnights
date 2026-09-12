using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;
    public NinjaBoost ownerNinjaBoost; // Set by PlayerShooter at spawn; drives Killing Blow

    // Ember: set at spawn when this shot's ignite roll succeeded. Together with
    // FireballProjectile these are the ONLY ways an enemy can be lit — burning
    // ground never ignites (see Docs/Design/ember-order.md).
    public bool ignitesOnHit;

    // Frigid: the firing knight's sheet, set at spawn like ownerNinjaBoost. Non-null
    // means this shot can carry cold and can shatter a statue; whether it actually
    // chills is the sheet's own question (the ward and the blade chill without the
    // arrow chain, arrows need Frost Tip).
    public FrigidBoost ownerFrigidBoost;

    // A main arrow flying through fire BECOMES an ignited arrow, and through a poison
    // cloud a poisoned arrow — picking up that carrier just as if it had rolled it.
    // Shadow arrows and shurikens set this false so only the knight's main shot
    // absorbs the field (see PlayerShooter).
    public bool absorbsFieldEffects = true;

    // Feat tagging, set by PlayerShooter at spawn. A shadow arrow counts toward
    // the Shadow Order's landing tally; a shuriken reports into the volley it was
    // thrown with. Both count at most once per projectile.
    public bool isShadowArrow;
    public ShurikenVolley volley;
    private bool _countedHit;

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
            bool zoneIgnites;
            // excludeEnemyId 0: an arrow has no drippings of its own to skip
            if (FireField.Sample(pos, 0, out dps, out zoneOwner, out zoneIgnites))
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
            // Equipment that bites one kinship group harder (The Gnawed Crown,
            // Wolfsbane Pendant). Before Killing Blow, so an execute still lands
            // as exactly lethal instead of an inflated number.
            int damageToDeal = EquipmentBoost.ScaleHit(damage, enemy, OwnerTag);

            // Killing Blow (Shadow Order): finish weakened enemies outright.
            // Never fires on bosses — skipping a fraction of a boss bar would
            // trivialize the fight. Asked of the enemy rather than type-checked
            // here: this line used to name EnemyRatKing outright, and every boss
            // written after him (the Overseer, the Crimson Twins) quietly fell
            // through it. See EnemyBase.IsBoss.
            if (ownerNinjaBoost != null && ownerNinjaBoost.ExecuteThreshold > 0f
                && !enemy.IsBoss && !enemy.IsDead
                && enemy.GetHealth() <= enemy.GetMaxHealth() * ownerNinjaBoost.ExecuteThreshold)
            {
                damageToDeal = Mathf.Max(damageToDeal, Mathf.CeilToInt(enemy.GetHealth()));
                ShadowFx.ExecuteFlash(enemy.transform.position);
                PlayerStats.Increment("kills.executed");
            }

            // Shatter (Frigid Order): every blow landed on a body held in ice is
            // multiplied. Asked BEFORE the damage lands, because landing it is what
            // wears the ice down - EnemyBase.TakeDamage counts every hit against the
            // ice, whether or not the knight owns Shatter.
            bool wasFrozen = enemy.IsFrozen;
            bool shatterOwned = ownerFrigidBoost != null && ownerFrigidBoost.ShatterMultiplier > 1f;
            if (wasFrozen && shatterOwned)
            {
                damageToDeal = Mathf.CeilToInt(damageToDeal * ownerFrigidBoost.ShatterMultiplier);
            }

            // Apply normal damage
            enemy.TakeDamage(damageToDeal, gameObject);

            // The burst, sound, splinters and tally belong to the hit that actually
            // BREAKS the ice. With Deep Freeze the ice can take several hits, and a
            // shatter effect on a body still standing in ice would read as a bug.
            bool shattered = shatterOwned && wasFrozen && !enemy.IsFrozen;
            if (shattered)
            {
                Vector2 breakPoint = enemy.transform.position;
                FrostFx.Burst(breakPoint, FrigidBoost.SplinterRadius);
                PlayerStats.Increment("frigid.shattered");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.frostShatter);
                }

                // Shatter II throws splinters. They CHILL and never freeze - the
                // Order's only way to reach more than one body at a time, and it
                // stays inside pillar 2 by doing it with cold rather than a blow.
                if (ownerFrigidBoost.ShatterSplinters)
                {
                    SpreadSplinters(breakPoint, enemy);
                }
            }

            if (!_countedHit)
            {
                _countedHit = true;
                if (isShadowArrow) Feats.Record(Feats.ShadowArrowHits);
                if (volley != null) volley.NoteHit();
            }
            
            // Check if this projectile has poison and apply it
            PoisonProjectile poisonComponent = GetComponent<PoisonProjectile>();
            if (poisonComponent != null)
            {
                poisonComponent.ApplyPoisonToEnemy(enemy, gameObject);
            }

            // Sleeping Dart: put it under. Before the fireball block below only
            // so the ordering reads shot-first, blast-last; a dart is never a
            // fireball, so the two can't both be on one projectile.
            SleepDartProjectile sleepDart = GetComponent<SleepDartProjectile>();
            if (sleepDart != null)
            {
                sleepDart.ApplySleepToEnemy(enemy);
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

            // Frigid, last: the damage above has already broken any ice this shot
            // landed on, and a thawed body carries nothing away with it. So a frost
            // arrow into a statue shatters it and leaves it chilled again in the
            // same instant, ready for the next arrow to stop it - which is the loop
            // the whole Order is built to run.
            //
            // isBlow: true. This is a hit the knight aimed and landed, so it is one
            // of the two things in the game allowed to freeze.
            if (ownerFrigidBoost != null && ownerFrigidBoost.ArrowsChill)
            {
                ownerFrigidBoost.TouchWithCold(enemy, true);
            }

            Destroy(gameObject);
            return;
        }

        // No more fallback code needed - all enemies have been migrated to EnemyBase
    }

    // Cold thrown off a body coming apart. Chill only, and never onto the thing
    // that was shattered - it has just been chilled by the arrow that broke it,
    // and paying it twice would let one shot do the Order's whole cycle alone.
    private void SpreadSplinters(Vector2 center, EnemyBase shatteredEnemy)
    {
        Collider2D[] caught = Physics2D.OverlapCircleAll(center, FrigidBoost.SplinterRadius);
        for (int i = 0; i < caught.Length; i++)
        {
            EnemyBase other = caught[i] != null ? caught[i].GetComponent<EnemyBase>() : null;
            if (other == null || other == shatteredEnemy || other.IsDead) continue;
            ownerFrigidBoost.TouchWithCold(other, false);
        }
    }
}
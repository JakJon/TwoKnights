using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 10;

    // How much of `damage` above is Dawn's holy light, for DISPLAY only. It is
    // already counted inside `damage` — this is not a second pool of damage and
    // adding it anywhere would pay the Order twice. It exists so the number that
    // prints over the body can be split into the red the shot did and the white
    // the light did, which is the only way a player can see what Dawn is actually
    // worth on a hit. See EnemyBase.TakeDamage.
    public int holyDamage;
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

    /// <summary>
    /// An ECHO of the knight's shot rather than the shot itself — a shadow arrow
    /// or a shuriken out of the fan.
    ///
    /// Echoes pay no special (see EnemyBase.GiveSpecialToPlayer). A fan at rank two
    /// puts five projectiles on the board for one press of the trigger, and every
    /// one of them was handing over a full arrow's worth of bar; the knight who
    /// bought the Shadow Order was filling the meter five times as fast as the one
    /// who didn't, which made the Order a special-charge upgrade that happened to
    /// throw shurikens. The bar is meant to count SHOTS the player took.
    /// </summary>
    public bool IsEcho => isShadowArrow || volley != null;

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
        // becomes the poisoned arrow this knight would fire, inheriting its tick bonus.
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

            // Everything landed into a body held in ice, and the share of it the
            // Shatter multiplier is responsible for. Two quests, two questions:
            // one asks how hard you hit the ice, the other how much of that was
            // the Order's doing.
            if (wasFrozen)
            {
                QuestTally.Total(OrderStats.FrozenTargetDamage, damageToDeal);
                if (shatterOwned) QuestTally.Wave(OrderStats.ShatterDamageWaveMax, damageToDeal);
            }

            // The holy share of what is about to land, in the same proportion it
            // held in the arrow. Taken proportionally rather than passed through
            // flat because everything above this line scales the WHOLE hit —
            // Gnawed Crown, Killing Blow, Shatter — and holy that stayed at its
            // authored 4 while the rest of the number tripled would be a lie about
            // where the damage came from. Rounding is absorbed by the red half, so
            // the two numbers always add back up to the hit that was dealt.
            int holyShare = (holyDamage > 0 && damage > 0)
                ? Mathf.Clamp(Mathf.RoundToInt(damageToDeal * (float)holyDamage / damage), 0, damageToDeal)
                : 0;

            // Apply normal damage
            enemy.TakeDamage(damageToDeal, gameObject, holyShare);

            // The burst, sound, splinters and tally belong to the hit that actually
            // BREAKS the ice. With Deep Freeze the ice can take several hits, and a
            // shatter effect on a body still standing in ice would read as a bug.
            bool shattered = shatterOwned && wasFrozen && !enemy.IsFrozen;
            if (shattered)
            {
                Vector2 breakPoint = enemy.transform.position;
                PlayerStats.Increment("frigid.shattered");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.frostShatter);
                }

                // Every rank of Shatter throws a frost blast off the break (owner,
                // 2026-10-05). It CHILLS and never freezes, and deals no damage -
                // see FrigidBoost.ReleaseShatterBlast.
                ownerFrigidBoost.ReleaseShatterBlast(breakPoint, enemy);
            }

            if (!_countedHit)
            {
                _countedHit = true;
                if (isShadowArrow)
                {
                    Feats.Record(Feats.ShadowArrowHits);
                    QuestTally.Wave(OrderStats.ShadowArrowWaveMax);
                }
                if (volley != null) volley.NoteHit();
                // A guided shot that arrived. Resolved when the projectile dies,
                // not here — this only records that it had something to resolve.
                GetComponent<GuidedShot>()?.NoteConnected();
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

            // Frigid, last.
            //
            // wasFrozen gates it (owner's call, 2026-09-16). An arrow into a body
            // held in ice ENDS the freeze and leaves it free - it does not also
            // leave it chilled. The damage above already broke the ice, so without
            // this check the touch would land on a thawed body and put fresh cold
            // straight back on, which reads as a shot that shatters and re-chills in
            // one frame. A frozen target costs an arrow to free; the next arrow is
            // the one that chills it again.
            //
            // isBlow: true. This is a hit the knight aimed and landed, so it is one
            // of the two things in the game allowed to freeze.
            //
            // Shadow arrows are excluded. Frost Tip chills on every arrow that
            // lands, and a shadow volley is several arrows landing on the same body
            // inside a few frames - enough cold to freeze whatever it touched on the
            // first shot of the fight, every shot of the fight. They still SHATTER
            // what is already frozen, above; they just do not carry the cold that
            // froze it.
            if (!wasFrozen && !isShadowArrow && ownerFrigidBoost != null && ownerFrigidBoost.ArrowsChill)
            {
                ownerFrigidBoost.TouchWithCold(enemy, true);
            }

            Destroy(gameObject);
            return;
        }

        // No more fallback code needed - all enemies have been migrated to EnemyBase
    }

}
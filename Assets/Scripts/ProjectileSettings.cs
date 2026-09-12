using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An enemy rock. One prefab covers all three kinds the shafts throw — see
/// <see cref="RockVariant"/> — because which one leaves the shaft is decided at
/// the moment of the throw by the wave's count, not by what a wave authored.
/// The prefab therefore carries the art for all three and puts on whichever it
/// is told to be.
///
/// A rock can also change sides. The Guardian Order's Reflector turns one around on
/// the guard, and from that moment it is the knight's ammunition: it carries its own
/// payload to whatever it lands on, and it can no longer touch a knight.
/// </summary>
public class ProjectileSettings : MonoBehaviour
{
    [SerializeField] public int damage = 10;
    [Tooltip("Name shown on the death screen when this projectile lands the killing blow.")]
    [SerializeField] private string sourceName = "an Arrow";

    [Header("Variants")]
    [Tooltip("Worn by a poisoned rock. Leave empty and a poisoned rock still poisons, it just looks ordinary.")]
    [SerializeField] private Sprite poisonSprite;

    [Tooltip("Worn by a powder-packed rock.")]
    [SerializeField] private Sprite explosiveSprite;

    [Tooltip("What a powder rock deals instead of the plain rock's damage.")]
    [SerializeField] private int explosiveDamage = 25;

    [Tooltip("How wide the burst reads when a powder rock lands. Cosmetic — the damage is the direct hit, so this never catches the far knight by surprise.")]
    [SerializeField] private float explosiveBlastRadius = 1.1f;

    [Tooltip("Death-screen name for a poisoned rock")]
    [SerializeField] private string poisonSourceName = "a Poisoned Rock";

    [Tooltip("Death-screen name for a powder rock")]
    [SerializeField] private string explosiveSourceName = "a Powder Rock";

    [Tooltip("Size of the bubble and ember trails RELATIVE TO THE ROCK, which draws at a quarter scale. Two reference points: 4 is world size, which is twice what a player's poisoned arrow wears and far too big on a sprite half the arrow's size; 1 is exactly proportional to the rock. 1.5 sits just above proportional so the trail reads without swallowing the rock.")]
    [SerializeField] private float trailScale = 1.5f;

    [Tooltip("Bubble trail size for a POISONED rock, which wants to be quieter than the powder rock's flame. Below the shared trailScale on purpose: green on a small fast sprite reads at a glance, and the arrow's full-size trail turned the rock into a travelling cloud.")]
    [SerializeField] private float poisonTrailScale = 0.9f;

    [Tooltip("Fraction of the standard projectile bubble rate a poisoned rock emits. The rate is authored for a player's arrow, which is bigger and on screen for less time.")]
    [SerializeField] private float poisonTrailRateScale = 0.3f;

    [Header("Reflected (Guardian Order)")]
    [Tooltip("Damage per tick a reflected GREEN rock leaves on an enemy. Deliberately the poisoned arrow's own number — the rock is carrying the mine's venom, not the knight's.")]
    [SerializeField] private int reflectedPoisonDamage = 2;

    [Tooltip("How long that rot runs. Half the poisoned arrow's, because a rock is one blow rather than a chain a Serpent knight is building.")]
    [SerializeField] private float reflectedPoisonDuration = 15f;

    [Tooltip("Seconds between ticks of a reflected rock's poison.")]
    [SerializeField] private float reflectedPoisonTickRate = 1f;

    public RockVariant Variant { get; private set; } = RockVariant.Plain;

    // ---- Reflector state. All three are written once, at the block. ----

    /// <summary>Whether the guard has turned this rock around. Once true the rock has
    /// changed sides for good: it cannot hurt a knight, cannot be blocked again, and
    /// looks only for a body.</summary>
    public bool Reflected { get; private set; }

    // Which knight is paid for what this rock does now. TakeDamage and the tag-based
    // AoE doors both want a "PlayerLeft"/"PlayerRight" string.
    private string _ownerTag;

    // What the rock's authored damage is multiplied by, from the blocking knight's
    // Reflector rank.
    private float _damageMultiplier = 1f;

    void Start()
    {
        // Register this projectile with the wave tracking system
        BaseWave.RegisterProjectile(gameObject);
    }

    void OnDestroy()
    {
        // Unregister this projectile when it's destroyed
        BaseWave.UnregisterProjectile(gameObject);
    }

    /// <summary>
    /// Dresses this rock as one of the three kinds. Called by the Spawner right
    /// after the rock is instantiated and before it is aimed, so the trail is
    /// already alight by the time the player first sees it.
    /// </summary>
    public void Become(RockVariant variant)
    {
        Variant = variant;
        if (variant == RockVariant.Plain) return;

        var renderer = GetComponent<SpriteRenderer>();

        if (variant == RockVariant.Poison)
        {
            if (renderer != null && poisonSprite != null) renderer.sprite = poisonSprite;
            sourceName = poisonSourceName;
            AttachPoisonTrail();
            return;
        }

        if (renderer != null && explosiveSprite != null) renderer.sprite = explosiveSprite;
        sourceName = explosiveSourceName;
        damage = explosiveDamage;
        AttachFireTrail();
    }

    // Ember's own arrow trail, borrowed. The rock is drawn at a quarter scale, so
    // the trail is scaled back up to world size — parented art inherits the
    // shrink and a quarter-size flame is not readable at this sprite's size.
    private void AttachFireTrail()
    {
        ParticleSystem trail = FireFx.AttachArrowTrail(gameObject);
        if (trail != null) SizeTrail(trail.transform);
    }

    // The same bubble trail a poisoned arrow wears, so a poisoned rock reads as
    // poison on sight rather than only once it has landed.
    private void AttachPoisonTrail()
    {
        var resources = PoisonResourceManager.Instance;
        GameObject prefab = resources != null ? resources.GetPoisonBubblePrefab() : null;

        GameObject host;
        PoisonBubbleEffect bubbles;
        if (prefab != null)
        {
            host = Instantiate(prefab, transform.position, Quaternion.identity);
            host.transform.SetParent(transform);
            host.transform.localPosition = Vector3.zero;
            bubbles = host.GetComponent<PoisonBubbleEffect>();
            if (bubbles == null) bubbles = host.AddComponent<PoisonBubbleEffect>();
        }
        else
        {
            host = new GameObject("PoisonRockTrail");
            host.transform.SetParent(transform);
            host.transform.localPosition = Vector3.zero;
            bubbles = host.AddComponent<PoisonBubbleEffect>();
            Sprite bubble = resources != null ? resources.GetPoisonBubbleSprite() : null;
            if (bubble != null) bubbles.SetBubbleSprite(bubble);
        }

        SizeTrail(host.transform, poisonTrailScale);
        float rate = (resources != null ? resources.projectileBubbleRate : 5f)
            * Mathf.Clamp01(poisonTrailRateScale);
        bubbles.SetBubbleRate(rate);
        bubbles.StartBubbles();
    }

    // Trails hang off the rock and inherit its quarter scale, which is the right
    // starting point — the effects are authored for a player's arrow, and the
    // rock is half an arrow's size. This used to divide the scale back out to
    // world size, which made the bubbles four times bigger than the rock they
    // were coming off.
    private void SizeTrail(Transform effect)
    {
        SizeTrail(effect, trailScale);
    }

    private void SizeTrail(Transform effect, float scale)
    {
        float s = Mathf.Max(0.01f, scale);
        effect.localScale = new Vector3(s, s, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // A rock the guard sent back belongs to the knight now. It is looking for a
        // body and nothing else: it cannot hurt a knight it drifts over, and it
        // cannot be blocked a second time by the other guard.
        if (Reflected)
        {
            EnemyBase struck = other.GetComponent<EnemyBase>();
            if (struck != null && !struck.IsDead)
            {
                StrikeEnemy(struck);
            }
            return;
        }

        // Check for shield collision
        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
            PlayerStats.Increment("guardian.blocked");
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(1);
            }

            // Reflector (Guardian): the guard turns it around instead of eating it.
            // After the special charge, deliberately — a block is a block whether or
            // not the rock came back, and routing the reward through the reflect
            // would have quietly deleted a charge source on every rebound.
            if (TryReflect(other)) return;

            // A powder rock stopped on the guard still goes off — the guard is
            // what makes it harmless, and the burst is the reward for reading it.
            BurstIfExplosive();
            Destroy(gameObject);
            return;
        }

        // Check for player collision
        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage, sourceName);
            }

            // The poison is the whole difference on a green rock: the hit is a
            // plain rock's, and the rot afterwards is what it really cost.
            if (Variant == RockVariant.Poison)
            {
                KnightPoison.Apply(other.gameObject, sourceName);
            }

            BurstIfExplosive();
            Destroy(gameObject);
        }
    }

    private void BurstIfExplosive()
    {
        if (Variant != RockVariant.Explosive) return;
        BombFx.Explode(transform.position, Mathf.Max(0.2f, explosiveBlastRadius));
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.fireballExplode);
    }

    /// <summary>
    /// Reflector (Guardian Order): the guard turns the rock around rather than
    /// stopping it. Returns true if it did, in which case the rock lives on and the
    /// caller must not destroy it.
    /// </summary>
    private bool TryReflect(Collider2D shield)
    {
        // Asked before the roll, not after: TryTurn spends the dice and plays the
        // tell, and a rock with no mover cannot be sent anywhere whatever it says.
        ProjectileMovement mover = GetComponent<ProjectileMovement>();
        if (mover == null) return false;

        // The rock's heading IS its rotation - ProjectileMovement translates along
        // its own local +X - so transform.right is the direction of travel.
        GuardianReflect.Turn turn = GuardianReflect.TryTurn(shield, transform.right);
        if (!turn.Happened) return false;

        Reflected = true;
        _damageMultiplier = turn.DamageMultiplier;
        _ownerTag = turn.OwnerTag;

        mover.Redirect(turn.Heading, GuardianBoost.ReflectedSpeedMultiplier);

        // NO TELL HERE, deliberately (owner's call, 2026-09-08). The steel is the
        // HOMING tell and nothing else, so it appears the moment a shot commits to a
        // target and never before — see GuidedShot.Acquire. A rebound from a knight
        // without Guided Reflections therefore wears nothing, and is read by its
        // speed and its heading instead: it leaves at two and a half times the pace
        // it arrived at, going the other way, which is not a shot anything on the
        // field could have thrown.
        //
        // Guided Reflections: the rebound steers, too. Bodies only — a rock cannot
        // collect an orb (CollectibleOrb answers to arrows and the blade, never to
        // this), so letting one chase an orb would be sending the Order's payout
        // somewhere it cannot land.
        if (turn.GuideRadius > 0f)
        {
            GuidedShot guide = gameObject.AddComponent<GuidedShot>();
            guide.Configure(turn.GuideRadius, false, _ownerTag);
        }

        // NOT optional. A wave is not complete while a tracked projectile is alive
        // (BaseWave.IsWaveComplete), and every rock until now was guaranteed to die
        // because it always ended on a knight or on a guard. A reflected one can fly
        // into open sky forever, and without this the wave would never end.
        Destroy(gameObject, GuardianBoost.ReflectedLifetimeSeconds);
        return true;
    }

    /// <summary>What a reflected rock does to the body it finds. The rock keeps the
    /// payload it was thrown with — a green one rots, a powder one goes off — so the
    /// mine's own escalation is what makes the Order scale.</summary>
    private void StrikeEnemy(EnemyBase enemy)
    {
        int dealt = EquipmentBoost.ScaleHit(
            Mathf.Max(1, Mathf.CeilToInt(damage * _damageMultiplier)), enemy, _ownerTag);

        // TakeDamage reads the projectile only for its TAG, to credit a knight, so
        // this borrows SwordSwing's carrier idiom rather than retagging the rock.
        // The rock must STAY untagged: EnemyBase destroys anything wearing a
        // Player*Projectile tag on contact, and its handler racing this one could
        // eat the rock before the damage landed — and a tagged rock would also start
        // swallowing health orbs on its way past.
        GameObject carrier = null;
        if (!string.IsNullOrEmpty(_ownerTag))
        {
            carrier = new GameObject("ReflectedRock");
            carrier.tag = _ownerTag + "Projectile";
        }

        enemy.TakeDamage(dealt, carrier);
        if (carrier != null) Destroy(carrier);

        if (Variant == RockVariant.Poison)
        {
            enemy.ApplyPoisonFromTag(
                reflectedPoisonDamage, reflectedPoisonDuration, reflectedPoisonTickRate, _ownerTag);
        }

        BurstOnEnemies(enemy);
        BurstIfExplosive();
        Destroy(gameObject);
    }

    /// <summary>
    /// A reflected powder rock actually goes off. This is the ONE place a rock's blast
    /// deals damage: the enemy's own powder rock stays cosmetic, because a gnome's
    /// blast clearing the gnome's own carts off the track would be doing the player's
    /// work for them (see EnemyBomb.Explode, which makes the same call).
    /// </summary>
    private void BurstOnEnemies(EnemyBase directHit)
    {
        if (Variant != RockVariant.Explosive) return;

        float radius = Mathf.Max(0.2f, explosiveBlastRadius);

        // Collected first and paid second: an enemy dying inside the loop can recurse
        // through OnDeath and mutate what the query handed back — the trap EnemyKegCart
        // documents at its own detonation.
        var victims = new List<EnemyBase>();
        foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, radius))
        {
            EnemyBase other = col != null ? col.GetComponent<EnemyBase>() : null;
            if (other == null || other == directHit || other.IsDead) continue;
            if (!victims.Contains(other)) victims.Add(other);
        }

        // The direct hit already paid; the blast is what the rest of the pack gets.
        // ApplyBlastDamage self-guards IsDead and ImmuneToAreaDamage, so carts and
        // kegs shrug it off without anything being written here.
        int blast = Mathf.Max(1, Mathf.CeilToInt(damage * _damageMultiplier));
        for (int i = 0; i < victims.Count; i++)
        {
            victims[i].ApplyBlastDamage(EquipmentBoost.ScaleHit(blast, victims[i], _ownerTag), _ownerTag);
        }

        // Scoped to THIS blast rather than to a running tally, the same way one
        // poison cloud counts its own four: the question is whether a single
        // returned rock caught three bodies, not whether three were ever hit.
        // Counts the body it struck directly alongside the ones the burst reached.
        if (victims.Count + 1 >= 3) Feats.Record(Feats.ReflectThree);
    }
}

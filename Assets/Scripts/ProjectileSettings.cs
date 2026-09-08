using UnityEngine;

/// <summary>
/// An enemy rock. One prefab covers all three kinds the shafts throw — see
/// <see cref="RockVariant"/> — because which one leaves the shaft is decided at
/// the moment of the throw by the wave's count, not by what a wave authored.
/// The prefab therefore carries the art for all three and puts on whichever it
/// is told to be.
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

    public RockVariant Variant { get; private set; } = RockVariant.Plain;

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
        // Check for shield collision
        if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
            PlayerSpecial playerSpecial = other.GetComponentInParent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                playerSpecial.updateSpecial(1);
            }
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
}

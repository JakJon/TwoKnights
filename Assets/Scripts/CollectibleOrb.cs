using UnityEngine;

public class CollectibleOrb : MonoBehaviour, IChillable
{
    public enum OrbType
    {
        Health,
        Mana
    }

    [SerializeField] private OrbType orbType;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private int healthRestoreAmount = 20;
    [SerializeField] private int manaRestoreAmount = 10;

    /// <summary>
    /// The prefab's own speed. Order trials read it to throw their orbs at "twice
    /// the speed of a normal orb" and "half", so retuning the orb retunes them too.
    /// </summary>
    public float MoveSpeed => moveSpeed;

    private Vector2 startPos;
    private Vector2 endPos;
    private Vector2 moveDir;
    private float totalDistance;

    public void Initialize(Vector2 start, Vector2 end)
    {
        startPos = start;
        endPos = end;
        transform.position = startPos;
        moveDir = (endPos - startPos).normalized;
        totalDistance = Vector2.Distance(startPos, endPos);

        // Sunwell III (Dawn): an orb is shared ground, so one knight buying the
        // slow widens the window for both. Asked once here rather than per frame.
        // The glow is how the player READS that it happened — a slowed orb has
        // to look different, or the upgrade is invisible until you do the maths.
        if (DawnBoost.AnyKnightSlowsOrbs())
        {
            moveSpeed *= DawnBoost.SlowedOrbSpeedMultiplier;
            DawnFx.AttachOrbGlow(gameObject);
        }

        AudioManager.Instance.PlaySFX(AudioManager.Instance.orbFlyBy);
    }

    // ---- cold (the Frigid Order) ----
    //
    // Frost that is not an arrow slows an orb by the same amount it would slow a
    // body. Deliberately not arrows, and not the blade either: anything of the
    // knight's that REACHES an orb collects it, so there would be nothing left to
    // slow - which is what makes this a gift from the ward rather than something a
    // Frigid knight has to aim for.
    //
    // A slowed orb is a wider window for BOTH knights, exactly as Sunwell III's is.
    private float _chillMultiplier = 1f;
    private float _chillUntil = -1f;
    private bool _chillGlow;

    public void ApplyChill(float speedMultiplier, float seconds)
    {
        if (seconds <= 0f) return;
        _chillMultiplier = Mathf.Clamp(Mathf.Min(_chillMultiplier, speedMultiplier), 0.05f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + seconds);

        // The orb has to LOOK slowed or the effect is invisible until you do the
        // arithmetic - the same reason Sunwell III attaches a glow where it slows.
        if (!_chillGlow)
        {
            _chillGlow = true;
            FrostFx.AttachEnemyChill(gameObject);
        }
    }

    private float ChillScale => Time.time < _chillUntil ? _chillMultiplier : 1f;

    private void Update()
    {
        float moveStep = moveSpeed * ChillScale * Time.deltaTime;
        transform.position += (Vector3)(moveDir * moveStep);
        if (Vector2.Distance(transform.position, startPos) >= totalDistance)
        {
            Destroy(gameObject);
            AudioManager.Instance.StopSFX();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        GameObject player = CollectorFor(other);
        if (player != null)
        {
            Destroy(gameObject);

            // Sunwell (Dawn) pays off the knight who actually REACHED the orb,
            // which is the whole point of the chain — it rewards the aim, not the
            // standing around
            DawnBoost dawn = player.GetComponent<DawnBoost>();

            if (orbType == OrbType.Health)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    int amount = dawn != null ? dawn.ScaleOrbHeal(healthRestoreAmount) : healthRestoreAmount;
                    // Tagged as an orb so the Dawn chime waits out orbCollect
                    // below instead of firing on the same frame
                    playerHealth.Heal(amount, true, HealSource.Orb);
                }

                // Sunwell II: a health orb also fills some of the special bar
                if (dawn != null && dawn.HealthOrbSpecial > 0)
                {
                    player.GetComponent<PlayerSpecial>()?.AddSpecialFromOrb(dawn.HealthOrbSpecial);
                }
            }
            else // Mana
            {
                PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
                if (playerSpecial != null)
                {
                    int amount = dawn != null ? dawn.ScaleOrbMana(manaRestoreAmount) : manaRestoreAmount;
                    playerSpecial.AddSpecialFromOrb(amount);
                }

                // Sunwell II: a mana orb also heals
                if (dawn != null && dawn.ManaOrbHeal > 0)
                {
                    player.GetComponent<PlayerHealth>()?.Heal(dawn.ManaOrbHeal, true, HealSource.Orb);
                }
            }

            AudioManager.Instance.PlaySFX(AudioManager.Instance.orbCollect);
        }
    }

    /// <summary>
    /// Which knight, if any, just took this orb. An arrow answers by its tag — the
    /// shot carries the tag of the knight who fired it, shurikens and shadow arrows
    /// included. A SWING answers through the detector the sword grows at swing time,
    /// because the sword object itself is untagged by design (see SwordSwing).
    ///
    /// The blade counts (owner's call): an orb crossing a knight's own reach and
    /// being ignored because they happened to be mid-swing rather than mid-shot is
    /// not a decision anyone made, and the sword is already the answer to everything
    /// else that comes that close.
    ///
    /// Public for TrialOrb and the ninja trial, which are collected — or hit — by
    /// exactly the same things.
    /// </summary>
    public static GameObject CollectorFor(Collider2D other)
    {
        if (other.CompareTag("PlayerLeftProjectile")) return GameObject.FindWithTag("PlayerLeft");
        if (other.CompareTag("PlayerRightProjectile")) return GameObject.FindWithTag("PlayerRight");

        SwordDamageDetector blade = other.GetComponent<SwordDamageDetector>();
        return blade != null ? blade.OwningKnight : null;
    }
}
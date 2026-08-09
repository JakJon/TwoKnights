using UnityEngine;

public class CollectibleOrb : MonoBehaviour
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

    private void Update()
    {
        float moveStep = moveSpeed * Time.deltaTime;
        transform.position += (Vector3)(moveDir * moveStep);
        if (Vector2.Distance(transform.position, startPos) >= totalDistance)
        {
            Destroy(gameObject);
            AudioManager.Instance.StopSFX();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            GameObject player = other.CompareTag("PlayerLeftProjectile")
                ? GameObject.FindWithTag("PlayerLeft")
                : GameObject.FindWithTag("PlayerRight");

            Destroy(gameObject);

            if (player != null)
            {
                // Sunwell (Dawn) pays off the knight who actually SHOT the orb,
                // which is the whole point of the chain — it rewards the aim,
                // not the standing around
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
                }
                else // Mana
                {
                    PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
                    if (playerSpecial != null)
                    {
                        playerSpecial.AddSpecialFromOrb(manaRestoreAmount);
                    }

                    // Sunwell II: even the mana orbs carry a little light
                    if (dawn != null && dawn.ManaOrbMend > 0)
                    {
                        player.GetComponent<PlayerHealth>()?.Heal(dawn.ManaOrbMend, true, HealSource.Orb);
                    }
                }
            }

            AudioManager.Instance.PlaySFX(AudioManager.Instance.orbCollect);

        }
    }
}
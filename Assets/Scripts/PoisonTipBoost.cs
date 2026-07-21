using UnityEngine;

// The knight's poison stat sheet: every Serpent upgrade writes into this component,
// and PlayerShooter / EnemyBase read from it when arrows fire and poisoned enemies die.
public class PoisonTipBoost : MonoBehaviour
{
    private float poisonChance = 0f; // Percentage chance (0-100)
    private int tickDamageBonus = 0; // Virulence: added to each poison tick
    private float tickRateMultiplier = 1f; // Virulence: <1 = faster ticks
    private int miasmaLevel = 0; // Miasma: 0 = off, 1-2 = death-cloud size/duration
    private bool plaguebringer = false; // Capstone: poisoned deaths burst onto neighbors

    // Serpent's Breath: sword swings can exhale a venom cloud along the shield facing
    private float swordCloudChance = 0f; // Percentage chance (0-100) per swing
    private float swordCloudDuration = 3f; // Seconds the cloud travels before fading
    private bool swordCloudLarge = false; // Tier III: bigger cloud

    public const int MaxMiasmaLevel = 2;

    public void IncreasePoisonChance(float amount)
    {
        poisonChance = Mathf.Clamp(poisonChance + amount, 0f, 100f);
    }

    public float GetPoisonChance()
    {
        return poisonChance;
    }

    // Check if this shot should be poisoned based on chance
    public bool ShouldApplyPoison()
    {
        return Random.Range(0f, 100f) < poisonChance;
    }

    public void AddTickDamage(int amount)
    {
        tickDamageBonus += amount;
    }

    public void MultiplyTickRate(float multiplier)
    {
        if (multiplier > 0f)
        {
            tickRateMultiplier *= multiplier;
        }
    }

    public void IncreaseMiasmaLevel()
    {
        miasmaLevel = Mathf.Min(miasmaLevel + 1, MaxMiasmaLevel);
    }

    public void EnablePlaguebringer()
    {
        plaguebringer = true;
    }

    // Each Serpent's Breath tier sets absolute values (33/3s -> 50/5s -> 100/15s+large)
    public void SetSwordCloud(float chance, float duration, bool large)
    {
        swordCloudChance = Mathf.Clamp(chance, 0f, 100f);
        swordCloudDuration = Mathf.Max(0.5f, duration);
        swordCloudLarge = large;
    }

    public bool ShouldExhaleSwordCloud()
    {
        return swordCloudChance > 0f && Random.Range(0f, 100f) < swordCloudChance;
    }

    public int TickDamageBonus => tickDamageBonus;
    public float TickRateMultiplier => tickRateMultiplier;
    public int MiasmaLevel => miasmaLevel;
    public bool Plaguebringer => plaguebringer;
    public float SwordCloudChance => swordCloudChance;
    public float SwordCloudDuration => swordCloudDuration;
    public bool SwordCloudLarge => swordCloudLarge;
}

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

    public int TickDamageBonus => tickDamageBonus;
    public float TickRateMultiplier => tickRateMultiplier;
    public int MiasmaLevel => miasmaLevel;
    public bool Plaguebringer => plaguebringer;
}

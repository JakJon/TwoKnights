using UnityEngine;

// Serpent discipline: stronger and/or faster poison ticks.
[CreateAssetMenu(fileName = "VirulenceUpgrade", menuName = "Upgrades/Virulence")]
public class VirulenceUpgrade : BaseUpgrade
{
    [SerializeField] private int tickDamageBonus = 0; // Added to each poison tick
    [SerializeField] private float tickRateMultiplier = 1f; // <1 = faster ticks

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Virulence";
        if (weight == 0f)
            weight = 50f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        if (tickDamageBonus != 0)
        {
            boost.AddTickDamage(tickDamageBonus);
        }
        boost.MultiplyTickRate(tickRateMultiplier);

        Debug.Log($"Applied Virulence to {targetKnight.name}. Tick bonus: {boost.TickDamageBonus}, rate multiplier: {boost.TickRateMultiplier}");
    }
}

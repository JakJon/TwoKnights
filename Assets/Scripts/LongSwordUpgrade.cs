using UnityEngine;

// Guardian-shaped trade: a longer, heavier blade that covers ground the stock
// sword never reaches and hits far harder, at the cost of being slower to bring
// round. Two ranks, each taking the same trade again.
[CreateAssetMenu(fileName = "LongSwordUpgrade", menuName = "Upgrades/Long Sword")]
public class LongSwordUpgrade : BaseUpgrade
{
    [Tooltip("Blade length and swing reach are multiplied by this. Compounds with any rank already owned.")]
    [SerializeField] private float lengthMultiplier = 2.129f;

    [Tooltip("Swing arc duration is multiplied by this. Above 1 is slower. Compounds.")]
    [SerializeField] private float slowMultiplier = 1.25f;

    [Tooltip("Base damage per swing, replacing the sword's own.")]
    [SerializeField] private int baseDamage = 15;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Long Sword";
        if (weight == 0f)
            weight = 30f; // Epic
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        LongSwordBoost boost = targetKnight.GetComponent<LongSwordBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<LongSwordBoost>();
        }

        boost.AddRank(lengthMultiplier, slowMultiplier, baseDamage);

        Debug.Log($"Applied Long Sword to {targetKnight.name}. Length x{boost.LengthMultiplier:F2}, " +
                  $"swing x{boost.SlowMultiplier:F2} slower, {boost.BaseDamage} damage.");
    }
}

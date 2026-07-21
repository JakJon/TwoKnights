using UnityEngine;

// Shadow discipline: arrows instantly execute weakened enemies (never bosses).
[CreateAssetMenu(fileName = "KillingBlowUpgrade", menuName = "Upgrades/Killing Blow")]
public class KillingBlowUpgrade : BaseUpgrade
{
    [SerializeField] private float executeThreshold = 0.15f; // Fraction of max HP

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Killing Blow";
        if (weight == 0f)
            weight = 50f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        NinjaBoost boost = targetKnight.GetComponent<NinjaBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<NinjaBoost>();
        }

        boost.SetExecuteThreshold(executeThreshold);

        Debug.Log($"Applied Killing Blow to {targetKnight.name}. Threshold: {boost.ExecuteThreshold:P0}");
    }
}

using UnityEngine;

// Serpent capstone: enemies that die poisoned burst, spreading their full poison
// stacks to nearby enemies and feeding the knight's special bar. Availability is
// gated by requiresOrderCount on the asset (owned Serpent upgrades).
[CreateAssetMenu(fileName = "PlaguebringerUpgrade", menuName = "Upgrades/Plaguebringer")]
public class PlaguebringerUpgrade : BaseUpgrade
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Plaguebringer";
        if (weight == 0f)
            weight = 10f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.EnablePlaguebringer();

        Debug.Log($"Applied Plaguebringer to {targetKnight.name}.");
    }
}

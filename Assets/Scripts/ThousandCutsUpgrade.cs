using UnityEngine;

// Shadow capstone: every kill credited to this knight erases their firing
// cooldown for 2 seconds — kills chain into rapid-fire windows. Availability
// gated by requiresOrderCount on the asset (owned Shadow upgrades).
[CreateAssetMenu(fileName = "ThousandCutsUpgrade", menuName = "Upgrades/Thousand Cuts")]
public class ThousandCutsUpgrade : BaseUpgrade
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Thousand Cuts";
        if (weight == 0f)
            weight = 10f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        NinjaBoost boost = targetKnight.GetComponent<NinjaBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<NinjaBoost>();
        }

        boost.EnableThousandCuts();

        Debug.Log($"Applied Thousand Cuts to {targetKnight.name}.");
    }
}

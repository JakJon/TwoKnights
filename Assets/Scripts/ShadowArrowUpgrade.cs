using UnityEngine;

[CreateAssetMenu(fileName = "ShadowArrowUpgrade", menuName = "Upgrades/Shadow Arrow")]
public class ShadowArrowUpgrade : BaseUpgrade
{
    [Header("Shadow Arrow Settings")]
    [SerializeField] private GameObject shadowArrowPrefab;
    [SerializeField] private int shadowArrowAmount = 1; // How many shadow arrows to spawn
    private float damageMultiplier = 0.2f; // 20% of main projectile damage
    private float trailDistance = 0.35f; // Distance behind main projectile
    private float positionVariance = 0.2f; // Random variance in position

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Add or get the ShadowArrowBoost component
        ShadowArrowBoost shadowArrowBoost = targetKnight.GetComponent<ShadowArrowBoost>();
        if (shadowArrowBoost == null)
        {
            shadowArrowBoost = targetKnight.AddComponent<ShadowArrowBoost>();
        }
        
        // Configure the shadow arrow settings
        shadowArrowBoost.SetShadowArrowSettings(shadowArrowPrefab, shadowArrowAmount, damageMultiplier, trailDistance, positionVariance);        
    }
}

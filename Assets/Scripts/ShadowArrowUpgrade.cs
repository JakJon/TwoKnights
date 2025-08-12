using UnityEngine;

[CreateAssetMenu(fileName = "ShadowArrowUpgrade", menuName = "Upgrades/Shadow Arrow")]
public class ShadowArrowUpgrade : BaseUpgrade
{
    [Header("Shadow Arrow Settings")]
    [SerializeField] private GameObject shadowArrowPrefab;
    [SerializeField] private float damageMultiplier = 0.33f; // 33% of main projectile damage
    [SerializeField] private float trailDistance = 0.5f; // Distance behind main projectile
    [SerializeField] private float positionVariance = 0.3f; // Random variance in position
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Add or get the ShadowArrowBoost component
        ShadowArrowBoost shadowArrowBoost = targetKnight.GetComponent<ShadowArrowBoost>();
        if (shadowArrowBoost == null)
        {
            shadowArrowBoost = targetKnight.AddComponent<ShadowArrowBoost>();
        }
        
        // Configure the shadow arrow settings
        shadowArrowBoost.SetShadowArrowSettings(shadowArrowPrefab, damageMultiplier, trailDistance, positionVariance);        
    }
}

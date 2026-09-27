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

    // Shadow is the tempo Order — Thousand Cuts already collapses the cooldown and
    // the whole chain is about volume — so the reload that used to be a classless
    // stat chain lives here. Every tier is another 10% off, COMPOUNDING, so the
    // five ranks come to 0.9^5 and a knight who takes the chain to the end is
    // firing a little under twice as often. Serialized so the balance pass is the
    // asset rather than this file.
    [SerializeField] private float reloadMultiplier = 0.9f;

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

        PlayerShooter shooter = targetKnight.GetComponent<PlayerShooter>();
        if (shooter != null) shooter.MultiplyCooldown(reloadMultiplier);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: {shadowArrowAmount} shadow arrows, " +
                  $"reload x{reloadMultiplier:F2}");
    }
}

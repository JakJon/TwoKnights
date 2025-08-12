using UnityEngine;

[CreateAssetMenu(fileName = "DamageUpgrade", menuName = "Upgrades/Damage")]
public class DamageUpgrade : BaseUpgrade
{
    [SerializeField] private int damageIncrease = 5;
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Damage Boost";
        if (string.IsNullOrEmpty(description))
            description = $"+{damageIncrease} Damage";
        if (weight == 0f)
            weight = 110f; // Common rarity weight
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Apply damage bonus directly to the PlayerShooter component
        PlayerShooter playerShooter = targetKnight.GetComponent<PlayerShooter>();
        if (playerShooter != null)
        {
            playerShooter.IncreaseDamage(damageIncrease);
        }
    }
}
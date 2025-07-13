using UnityEngine;

[CreateAssetMenu(fileName = "FiringRateUpgrade", menuName = "Upgrades/Firing Rate")]
public class FiringRateUpgrade : BaseUpgrade
{
    [SerializeField] private float rateMultiplier = 0.7f; // Lower cooldown = faster rate
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Firing Rate";
        if (string.IsNullOrEmpty(description))
            description = $"+{(1f - rateMultiplier) * 100:F0}% Fire Rate";
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        var playerShooter = targetKnight.GetComponent("PlayerShooter");
        if (playerShooter != null)
        {
            var cooldownField = playerShooter.GetType().GetField("cooldownTime");
            if (cooldownField != null)
            {
                float currentCooldown = (float)cooldownField.GetValue(playerShooter);
                cooldownField.SetValue(playerShooter, currentCooldown * rateMultiplier);
            }
        }
    }
}